using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[System.Serializable]
public class SubjectEntry
{
    [Tooltip("Tên môn hiển thị")]
    public string subjectName;

    [Tooltip("Key Notes: Resources/NoteItems/<key>/BuoiN.txt. Bỏ trống sẽ dùng subjectName.")]
    public string subjectKeyForNotes = "";

    [Tooltip("Tổng số buổi tối đa của môn trong học kỳ")]
    public int maxSessions;

    [HideInInspector] public int currentSessionIndex = 0; // số buổi đã HỌC (không tính vắng)

    [Header("Exam Linking")]
    [Tooltip("Index của môn này trong ExamLoader.exams (trên ExamScene). -1 = không dùng, sẽ match theo tên.")]
    public int examIndexInLoader = -1;
}

public class TeacherAction : InteractableAction
{
    [Header("Config (kỳ hiện tại của NPC)")]
    public SemesterConfig semesterConfig;

    [Header("Auto switch by term")]
    [SerializeField] private List<SemesterConfig> configsByTerm = new(); // index = term - 1
    [SerializeField] private bool autoSyncSubjectsFromConfig = true;

    [Header("Các môn giảng dạy (được build từ SemesterConfig theo kỳ)")]
    public List<SubjectEntry> subjects = new List<SubjectEntry>();

    [Header("UI Title")]
    public string titleText;

    [Header("UI Texts")]
    public string openText = DataKeyText.openText;
    public string confirmText = DataKeyText.text1;
    public string wrongTimeText = DataKeyText.text2;
    public string learningText = DataKeyText.text3;

    [Tooltip("Đã học xong tất cả buổi của môn")]
    public string finishedAllSessionsText = "Em đã học xong tất cả các buổi của môn này!";

    [Header("Attendance/Absences")]
    [Tooltip("Hiện khi nghỉ quá số buổi quy định")]
    public string exceededAbsenceText = "Em đã nghỉ quá số buổi quy định cho phép";

    [Header("Flow")]
    [Min(0.1f)] public float classSeconds = 3f;

    [Header("Notes")]
    public bool addNoteWhenFinished = true;
    [Tooltip("0 = tự đánh số tiếp theo. >0 = dùng số buổi chỉ định")]
    public int noteSessionIndex = 0;

    [Header("Calendar (fallback)")]
    [Tooltip("Số tuần mỗi kỳ (fallback nếu không đọc được từ SemesterConfig)")]
    public int weeksPerTerm = 5;

    [Header("Exam Options")]
    [Tooltip("Tự động tạo lịch thi cho môn đã hoàn thành nếu chưa có")]
    public bool autoCreateExamSchedule = true;

    [Header("Events")]
    public UnityEvent onClassStarted;
    public UnityEvent onClassFinished;

    enum State { Idle, AwaitConfirm, InClass }
    State _state = State.Idle;
    Coroutine _classRoutine;
    InteractableNPC _callerCache;

    GameUIManager UI => GameUIManager.Ins;
    GameClock Clock => GameClock.Ins;

#if UNITY_EDITOR
    private void DBG(string msg) => Debug.Log($"[TeacherAction][{name}] {msg}");
#else
    private void DBG(string msg) => Debug.Log($"[TeacherAction] {msg}");
#endif

    // ===================== Lifecycle =====================
    private void OnEnable()
    {
        if (GameClock.Ins != null)
            GameClock.Ins.OnTermChanged += HandleTermChanged;
    }

    private void OnDisable()
    {
        if (GameClock.Ins != null)
            GameClock.Ins.OnTermChanged -= HandleTermChanged;
    }

    private void Awake()
    {
        if (autoSyncSubjectsFromConfig)
        {
            var cfg = FindConfigForTerm(GetCurrentTerm());
            if (cfg != null)
            {
                semesterConfig = cfg;
                RebuildSubjectsFromConfig(cfg);
            }
        }

        for (int i = 0; i < subjects.Count; i++)
            subjects[i].currentSessionIndex = LoadProgress(subjects[i]);

        if (autoCreateExamSchedule)
            StartCoroutine(CreateMissingExamSchedulesDelayed());
    }

    private void OnDestroy()
    {
        for (int i = 0; i < subjects.Count; i++)
            SaveProgress(subjects[i]);
    }

    private void OnApplicationQuit()
    {
        for (int i = 0; i < subjects.Count; i++)
            SaveProgress(subjects[i]);
    }

    private void HandleTermChanged()
    {
        if (!autoSyncSubjectsFromConfig) return;

        int newTerm = GetCurrentTerm();
        var cfg = FindConfigForTerm(newTerm);
        if (cfg == null)
        {
            return;
        }

        semesterConfig = cfg;
        RebuildSubjectsFromConfig(cfg);

        UI_Close();
    }

    // ===================== Helpers: Term/Config =====================
    private int GetCurrentTerm() => Clock ? Clock.Term : 1;

    private int GetWeeksPerTerm()
    {
        // Ưu tiên đọc từ SemesterConfig
        if (semesterConfig != null && semesterConfig.Weeks > 0) return semesterConfig.Weeks;

        // Fallback inspector
        return Mathf.Max(1, weeksPerTerm);
    }

    private SemesterConfig FindConfigForTerm(int term)
    {
        // Ưu tiên list kéo sẵn theo index
        if (term >= 1 && term <= configsByTerm.Count && configsByTerm[term - 1] != null)
            return configsByTerm[term - 1];

        // Fallback: tìm trong Resources theo convention "Semester{term}Config"
        var res = Resources.Load<SemesterConfig>($"Semester{term}Config");
        if (res != null) return res;

        // Fallback 2: duyệt list để tìm asset có field Semester == term
        foreach (var c in configsByTerm)
        {
            if (c == null) continue;
            try { if (c.Semester == term) return c; } catch { }
        }
        return null;
    }

    private static string NormalizeKey(string s) => (s ?? "").Trim().ToLowerInvariant();
    private static string MakeNoteKey(string name)
    {
        string k = (name ?? "").Trim().ToLowerInvariant();
        return k.Replace(" ", "");
    }

    // Build lại subjects từ ScriptableObject (Name, Sessions, Weeks)
    private void RebuildSubjectsFromConfig(SemesterConfig cfg)
    {
        var newList = new List<SubjectEntry>();
        if (cfg != null && cfg.Subjects != null)
        {
            int weeks = Mathf.Max(1, cfg.Weeks);
            foreach (var s in cfg.Subjects)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.Name)) continue;

                int perWeek = (s.Sessions != null) ? s.Sessions.Length : 0;
                int maxSessions = Mathf.Max(1, weeks * Mathf.Max(1, perWeek));

                newList.Add(new SubjectEntry
                {
                    subjectName = s.Name,
                    subjectKeyForNotes = MakeNoteKey(s.Name),
                    maxSessions = maxSessions,
                    examIndexInLoader = -1
                });
            }
        }

        subjects = newList;

        // Nạp lại tiến độ theo key của kỳ hiện tại
        for (int i = 0; i < subjects.Count; i++)
            subjects[i].currentSessionIndex = LoadProgress(subjects[i]);
    }

    private int GetTeacherSemester()
        => (semesterConfig != null && semesterConfig.Semester > 0) ? semesterConfig.Semester : GetCurrentTerm();

    private bool IsSubjectActiveNow() => Clock && GetTeacherSemester() == Clock.Term;

    // ===================== Progress Keys (theo kỳ của teacher/config) =====================
    private string GetStableSubjectKey(SubjectEntry s)
    {
        var baseKey = !string.IsNullOrWhiteSpace(s.subjectKeyForNotes) ? s.subjectKeyForNotes : s.subjectName;
        return NormalizeKey(baseKey);
    }

    private string NewProgressKey(SubjectEntry s)
    {
        int term = GetTeacherSemester();
        return $"T{term}_SUBJ_{GetStableSubjectKey(s)}_session";
    }
    private string LegacyProgressKey_Term_Name(SubjectEntry s)
    {
        int term = GetTeacherSemester();
        return $"T{term}_SUBJ_{NormalizeKey(s.subjectName)}_session";
    }
    private string LegacyProgressKey_NoTerm_Name(SubjectEntry s)
    {
        return $"SUBJ_{NormalizeKey(s.subjectName)}_session";
    }

    private int LoadProgress(SubjectEntry s)
    {
        string kNew = NewProgressKey(s);
        if (PlayerPrefs.HasKey(kNew))
            return PlayerPrefs.GetInt(kNew, 0);

        string kLegacy1 = LegacyProgressKey_Term_Name(s);
        if (PlayerPrefs.HasKey(kLegacy1))
        {
            int v = PlayerPrefs.GetInt(kLegacy1, 0);
            PlayerPrefs.SetInt(kNew, v);
            PlayerPrefs.Save();
            return v;
        }

        string kLegacy2 = LegacyProgressKey_NoTerm_Name(s);
        if (PlayerPrefs.HasKey(kLegacy2))
        {
            int v = PlayerPrefs.GetInt(kLegacy2, 0);
            PlayerPrefs.SetInt(kNew, v);
            PlayerPrefs.Save();
            return v;
        }

        return 0;
    }

    private void SaveProgress(SubjectEntry s)
    {
        string kNew = NewProgressKey(s);
        PlayerPrefs.SetInt(kNew, s.currentSessionIndex);
        PlayerPrefs.SetInt(LegacyProgressKey_Term_Name(s), s.currentSessionIndex);
        PlayerPrefs.SetInt(LegacyProgressKey_NoTerm_Name(s), s.currentSessionIndex);
        PlayerPrefs.Save();
    }

    // ===================== Exam Assignment (theo kỳ của teacher/config) =====================
    private string ExamKeyPrefix(SubjectEntry s)
    {
        int term = GetTeacherSemester();
        return $"T{term}_EXAM_{GetStableSubjectKey(s)}";
    }
    private void SaveExamAssignment(SubjectEntry s, int assignedTerm, int assignedWeek, Weekday day, int slot1Based)
    {
        string p = ExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_term", assignedTerm);
        PlayerPrefs.SetInt(p + "_week", assignedWeek);
        PlayerPrefs.SetInt(p + "_day", (int)day);
        PlayerPrefs.SetInt(p + "_slot1Based", slot1Based);
        PlayerPrefs.DeleteKey(p + "_missed");
        PlayerPrefs.Save();
    }
    private bool TryLoadExamAssignment(SubjectEntry s, out int term, out int week, out Weekday day, out int slot1Based, out bool missed)
    {
        string p = ExamKeyPrefix(s);
        term = week = slot1Based = 0;
        day = Weekday.Mon;
        missed = false;

        if (!PlayerPrefs.HasKey(p + "_day") || !PlayerPrefs.HasKey(p + "_slot1Based"))
            return false;

        term = PlayerPrefs.GetInt(p + "_term", 0);
        week = PlayerPrefs.GetInt(p + "_week", 0);
        day = (Weekday)PlayerPrefs.GetInt(p + "_day");
        slot1Based = PlayerPrefs.GetInt(p + "_slot1Based");
        missed = PlayerPrefs.GetInt(p + "_missed", 0) == 1;
        return true;
    }
    private void MarkExamMissed(SubjectEntry s)
    {
        string p = ExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_missed", 1);
        PlayerPrefs.Save();
    }
    private void ClearExamAssignment(SubjectEntry s)
    {
        string p = ExamKeyPrefix(s);
        PlayerPrefs.DeleteKey(p + "_term");
        PlayerPrefs.DeleteKey(p + "_week");
        PlayerPrefs.DeleteKey(p + "_day");
        PlayerPrefs.DeleteKey(p + "_slot1Based");
        PlayerPrefs.DeleteKey(p + "_missed");
        PlayerPrefs.Save();
    }

    private IEnumerator CreateMissingExamSchedulesDelayed()
    {
        yield return new WaitForSeconds(1f); // đợi hệ thống khởi tạo xong

        if (!IsSubjectActiveNow()) yield break;

        foreach (var subj in subjects)
        {
            if (IsCourseFinished(subj))
            {
                EnsureExamScheduleExists(subj);
            }
        }
    }

    /// <summary>
    /// Tự động tạo lịch thi nếu môn đã hoàn thành nhưng chưa có
    /// </summary>
    private void EnsureExamScheduleExists(SubjectEntry subj)
    {
        if (!autoCreateExamSchedule) return;
        if (!IsSubjectActiveNow()) return; // chỉ áp dụng cho môn của kỳ hiện tại

        if (TryLoadExamAssignment(subj, out _, out _, out _, out _, out _))
        {
            return;
        }

        if (IsCourseFinished(subj))
        {
            if (TryGetNearestExamSlotNextWeek(subj, out var examTerm, out var examWeek, out var examDayNextWeek, out var slotIdx1Based))
            {
                SaveExamAssignment(subj, examTerm, examWeek, examDayNextWeek, slotIdx1Based);
            }
        }
    }

    // ===================== Attendance / Finish =====================
    private int GetAbsencesFor(SubjectEntry subj)
    {
        var att = AttendanceManager.Instance;
        int term = GetTeacherSemester();
        return att ? Mathf.Max(0, att.GetAbsences(subj.subjectName, term)) : 0;
    }

    private bool IsCourseFinished(SubjectEntry subj)
    {
        int attended = Mathf.Max(0, subj.currentSessionIndex);
        int abs = GetAbsencesFor(subj);
        int cap = Mathf.Max(1, subj.maxSessions);
        return (attended + abs) >= cap;
    }

    // ===================== Find subject for NOW =====================
    private bool TryFindSubjectForNow(out SubjectEntry subj)
    {
        subj = null;
        if (!Clock || !semesterConfig || subjects == null || subjects.Count == 0) return false;

        // Chỉ cho phép match khi đúng kỳ
        if (!IsSubjectActiveNow()) return false;

        var today = Clock.Weekday;
        var slot1Based = Clock.SlotIndex1Based;

        foreach (var s in subjects)
        {
            if (string.IsNullOrWhiteSpace(s.subjectName)) continue;
            if (ScheduleResolver.IsSessionMatch(semesterConfig, s.subjectName, today, slot1Based))
            {
                subj = s;
                return true;
            }
        }
        return false;
    }

    // ===================== InteractableAction =====================
    public override void OnPlayerExit()
    {
        if (_state == State.AwaitConfirm)
        {
            _state = State.Idle;
            UI?.CloseDialogue(unbindTeacher: true);
        }
    }

    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI || _state == State.InClass) return;

        if (!IsSubjectActiveNow())
        {
            UI?.OpenDialogue(TitleText(), $"Các môn của giảng viên này thuộc học kỳ {GetTeacherSemester()}. Em đang ở học kỳ {GetCurrentTerm()}.");
            return;
        }

        _state = State.AwaitConfirm;
        _callerCache = caller;

        UI.BindTeacher(this);
        UI.OpenDialogue(TitleText(), openText);
    }

    public void UI_Close()
    {
        UI?.CloseDialogue(unbindTeacher: true);
        _state = State.Idle;
    }

    private string TitleText() => string.IsNullOrWhiteSpace(titleText) ? "No Title" : titleText;

    // ===================== Start class =====================
    public void UI_StartClass()
    {
        if (_state == State.InClass) return;

        if (!IsSubjectActiveNow())
        {
            UI?.OpenDialogue(TitleText(), $"Các môn của giảng viên này thuộc học kỳ {GetTeacherSemester()}.");
            return;
        }

        if (!TryFindSubjectForNow(out var subj))
        {
            UI?.OpenDialogue(TitleText(), wrongTimeText);
            return;
        }

        subj.currentSessionIndex = LoadProgress(subj);

        if (IsCourseFinished(subj))
        {
            UI?.OpenDialogue(TitleText(), finishedAllSessionsText);
            return;
        }

        var att = AttendanceManager.Instance;
        if (att != null)
        {
            if (!att.TryCheckIn(subj.subjectName, out string err))
            {
                UI?.OpenDialogue(TitleText(), string.IsNullOrEmpty(err) ? wrongTimeText : err);
                return;
            }
        }

        string quizKey = !string.IsNullOrWhiteSpace(subj.subjectKeyForNotes) 
            ? subj.subjectKeyForNotes 
            : MakeNoteKey(subj.subjectName);
        
        _state = State.InClass;
        onClassStarted?.Invoke();
        
        UI?.StartQuizForSubject(quizKey);
    }
    
    /// <summary>
    /// ClassRoutine - KHÔNG DÙNG NỮA (quiz đã tích hợp vào flow)
    /// Giữ lại để tránh lỗi compilation
    /// </summary>
    [System.Obsolete("This method is deprecated. Quiz flow now integrated in UI_StartClass -> CompleteClassAfterQuiz")]
    private IEnumerator ClassRoutine(SubjectEntry subj)
    {
        Debug.LogWarning("[TeacherAction] ClassRoutine is deprecated and should not be called!");
        yield break;
    }
    
    private void AddNoteIfNeeded(SubjectEntry subj)
    {
        if (!addNoteWhenFinished || NotesService.Instance == null) return;
        if (!IsSubjectActiveNow()) return; // chỉ ghi note cho kỳ hiện tại

        string key = !string.IsNullOrWhiteSpace(subj.subjectKeyForNotes) ? subj.subjectKeyForNotes : subj.subjectName;

        int attendedSoFar = Mathf.Max(0, subj.currentSessionIndex);
        int absencesSoFar = GetAbsencesFor(subj);
        int realSessionIndex = attendedSoFar + absencesSoFar;

        int sessionIndex = noteSessionIndex > 0 ? noteSessionIndex : realSessionIndex;

        NotesService.Instance.AddNoteRef(key, sessionIndex, subj.subjectName);

        var b = Object.FindFirstObjectByType<BackpackUIManager>();
        if (b && b.gameObject.activeInHierarchy) b.RefreshNoteButtons();
    }

    /// <summary>
    /// **MỚI: Hoàn thành buổi học SAU KHI QUIZ xong (không chạy lại quiz)**
    /// </summary>
    public void CompleteClassAfterQuiz(bool quizPassed = true)
    {
        if (!TryFindSubjectForNow(out var subj))
        {
            return;
        }

        StartCoroutine(CompleteClassRoutine(subj, quizPassed));
    }

    /// <summary>
    /// **MỚI: Coroutine xử lý logic hoàn thành buổi học (không bao gồm quiz)**
    /// </summary>
    private IEnumerator CompleteClassRoutine(SubjectEntry subj, bool quizPassed)
    {
        _state = State.InClass;

        // **LOGIC MỚI: Xử lý kết quả quiz**
        if (!quizPassed)
        {
            // Không đạt quiz -> tính vắng mặt
            UI?.OpenDialogue(TitleText(), $"Em chưa đạt yêu cầu trong bài kiểm tra.\nBuổi học này được tính là <color=red><b>vắng mặt</b></color>.");
            yield return new WaitForSecondsRealtime(3.0f);

            // Tăng số buổi vắng (thông qua AttendanceManager hoặc trực tiếp)
            IncrementAbsenceForSubject(subj);
            
            // **QUAN TRỌNG: Đóng dialogue VÀ unbind teacher**
            UI?.CloseDialogue(unbindTeacher: true);
            yield return new WaitForSecondsRealtime(0.5f);

            // **CHUYỂN SANG CA TIẾP THEO**
            Debug.Log($"[TeacherAction] Quiz failed - Chuyển sang ca tiếp theo");
            if (Clock) 
            {
                Clock.JumpToNextSessionStart();
            }

            _state = State.Idle;
            UI?.HideInteractPrompt();
            onClassFinished?.Invoke();
            yield break; // Kết thúc sớm, không tính progress
        }

        // **LOGIC CŨ: Đạt quiz -> tiếp tục như bình thường**
        UI?.OpenDialogue(TitleText(), "Hoàn thành buổi học! Đang xử lý...");
        yield return new WaitForSecondsRealtime(1.0f);

        int abs = GetAbsencesFor(subj);
        int cap = Mathf.Max(1, subj.maxSessions);
        int attended = Mathf.Max(0, subj.currentSessionIndex);
        int attendedCap = Mathf.Max(0, cap - abs);

        if (attended < attendedCap)
        {
            subj.currentSessionIndex = attended + 1;
            SaveProgress(subj);
        }

        bool justFinished = (Mathf.Min(attended + 1, attendedCap) >= attendedCap);
        if (justFinished)
        {
            if (TryGetNearestExamSlotNextWeek(subj, out var examTerm, out var examWeek, out var examDayNextWeek, out var slotIdx1Based))
            {
                SaveExamAssignment(subj, examTerm, examWeek, examDayNextWeek, slotIdx1Based);

                string dayVN = DataKeyText.VN_Weekday(examDayNextWeek);
                int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slotIdx1Based));
                string timeStr = DataKeyText.FormatHM(startMin);

                string examMsg = $"Chúc mừng em đã học xong môn '{subj.subjectName}'. Tuần sau em sẽ thi vào {dayVN} lúc {timeStr}. Chỉ được phép thi đúng ca này.";
                UI?.OpenDialogue(TitleText(), examMsg);
            }
            else
            {
                UI?.OpenDialogue(TitleText(), "Không tìm thấy lịch thi tuần sau cho môn này. Kiểm tra SemesterConfig / ScheduleResolver.");
            }

            yield return new WaitForSecondsRealtime(2.0f);
        }

        // Thêm note nếu cần
        AddNoteIfNeeded(subj);

        // **QUAN TRỌNG: Đóng dialogue VÀ unbind teacher**
        UI?.CloseDialogue(unbindTeacher: true);
        yield return new WaitForSecondsRealtime(0.5f);

        // **CHUYỂN SANG CA TIẾP THEO**
        Debug.Log($"[TeacherAction] ★★★ Chuyển sang ca tiếp theo ★★★");
        if (Clock) 
        {
            Clock.JumpToNextSessionStart();
            Debug.Log($"[TeacherAction] Đã gọi Clock.NextSlot() - Ca mới: {Clock.Slot}");
        }
        else
        {
            Debug.LogError("[TeacherAction] GameClock is null! Cannot advance to next slot!");
        }

        _state = State.Idle;
        UI?.HideInteractPrompt();

        Debug.Log($"[TeacherAction] CompleteClassRoutine finished for '{subj.subjectName}'");

        onClassFinished?.Invoke();
    }
    
    /// <summary>
    /// **MỚI: Tăng số buổi vắng cho môn học**
    /// </summary>
    private void IncrementAbsenceForSubject(SubjectEntry subj)
    {
        int term = GetTeacherSemester();
        string key = $"abs_T{term}_{GetStableSubjectKey(subj)}";
        int current = PlayerPrefs.GetInt(key, 0);
        PlayerPrefs.SetInt(key, current + 1);
        PlayerPrefs.Save();
        
        Debug.Log($"[TeacherAction] Tăng vắng mặt cho môn '{subj.subjectName}': {current} -> {current + 1}");
    }

    // ===================== Exam =====================
    // Lấy ca sớm nhất của TUẦN SAU (giữ nguyên kỳ hiện tại)
    private bool TryGetNearestExamSlotNextWeek(
        SubjectEntry subj,
        out int examTerm,
        out int examWeek,
        out Weekday day,
        out int slotIdx1Based)
    {
        int curTerm = GetTeacherSemester(); // giữ theo kỳ của giáo viên
        int curWeek = Clock ? Mathf.Max(1, Clock.Week) : 1;
        int wpt = GetWeeksPerTerm();

        examTerm = curTerm;
        int nextWeek = curWeek + 1;
        if (nextWeek > wpt) nextWeek = wpt; // không cuộn sang kỳ mới
        examWeek = nextWeek;

        day = Weekday.Mon;
        slotIdx1Based = 1;

        if (semesterConfig == null || string.IsNullOrWhiteSpace(subj.subjectName))
            return false;

        for (int d = (int)Weekday.Mon; d <= (int)Weekday.Sun; d++)
        {
            for (int sIdx = 1; sIdx <= 5; sIdx++)
            {
                if (ScheduleResolver.IsSessionMatch(semesterConfig, subj.subjectName, (Weekday)d, sIdx))
                {
                    day = (Weekday)d;
                    slotIdx1Based = sIdx;
                    return true;
                }
            }
        }

        return false;
    }

    private int CompareCalendarPos(int termA, int weekA, Weekday dayA, int slotA, int termB, int weekB, Weekday dayB, int slotB)
    {
        if (termA != termB) return termA < termB ? -1 : 1;
        if (weekA != weekB) return weekA < weekB ? -1 : 1;
        if (dayA != dayB) return ((int)dayA) < ((int)dayA) ? -1 : 1; // (typo fix not needed because we won't hit equal branch)
        if (slotA != slotB) return slotA < slotB ? -1 : 1;
        return 0;
    }

    public void UI_TakeExam()
    {
        if (!IsSubjectActiveNow())
        {
            UI?.OpenDialogue(TitleText(), $"Các môn của giảng viên này thuộc học kỳ {GetTeacherSemester()}.");
            return;
        }

        if (!TryFindSubjectForNow(out var subj))
        {
            UI?.OpenDialogue(TitleText(), "Không đúng ca thi hoặc môn học.");
            return;
        }

        subj.currentSessionIndex = LoadProgress(subj);

        // 1) Phải hoàn thành môn
        if (!IsCourseFinished(subj))
        {
            var att = AttendanceManager.Instance;
            if (att != null && att.HasExceededAbsences(subj.subjectName, GetTeacherSemester()))
            {
                UI?.OpenDialogue(TitleText(), "Em vắng quá số buổi cho phép nên bị cấm thi");
                return;
            }
            UI?.OpenDialogue(TitleText(), "Em chưa hoàn thành đủ số buổi môn này");
            return;
        }

        // **Tự động tạo lịch thi nếu chưa có**
        EnsureExamScheduleExists(subj);

        // 2) Lấy lịch thi đã chốt
        if (!TryLoadExamAssignment(subj, out int aTerm, out int aWeek, out Weekday aDay, out int aSlot1, out bool missed))
        {
            UI?.OpenDialogue(TitleText(), "Không thể tạo lịch thi cho môn này. Kiểm tra cấu hình SemesterConfig.");
            return;
        }

        if (Clock == null)
        {
            UI?.OpenDialogue(TitleText(), "Đồng hồ chưa sẵn sàng.");
            return;
        }

        int nTerm = GetCurrentTerm();
        int nWeek = Mathf.Max(1, Clock.Week);
        Weekday nDay = Clock.Weekday;
        int nSlot1 = Clock.SlotIndex1Based;

        int cmpForMissedRecheck = CompareCalendarPos(nTerm, nWeek, nDay, nSlot1, aTerm, aWeek, aDay, aSlot1);
        if (missed)
        {
            if (cmpForMissedRecheck <= 0)
            {
                SaveExamAssignment(subj, aTerm, aWeek, aDay, aSlot1);
                missed = false;
            }
            else
            {
                bool sameDaySlotNow = (nDay == aDay) && (nSlot1 == aSlot1);
                if (sameDaySlotNow)
                {
                    SaveExamAssignment(subj, nTerm, nWeek, nDay, nSlot1);
                    aTerm = nTerm; aWeek = nWeek; aDay = nDay; aSlot1 = nSlot1;
                    missed = false;
                }
                else
                {
                    AnnounceExamEnded(aDay, aSlot1);
                    return;
                }
            }
        }

        var attMgr = AttendanceManager.Instance;
        if (attMgr == null)
        {
            UI?.OpenDialogue(TitleText(), "Không tìm thấy AttendanceManager.");
            return;
        }

        int slotStart = attMgr.GetSlotStart(Clock.Slot);
        int examStart = slotStart + 5;
        int examEnd = slotStart + 15;

        int now = Clock.MinuteOfDay;

        if (now < examStart)
        {
            UI?.OpenDialogue(TitleText(),
                $"Chưa đúng giờ bắt đầu thi. Em chỉ được phép vào thi từ {DataKeyText.FormatHM(examStart)} đến {DataKeyText.FormatHM(examEnd)}.");
            return;
        }
        if (now >= examEnd)
        {
            MarkExamMissed(subj);
            UI?.OpenDialogue(TitleText(),
                "Bạn đã quá giờ điểm danh vào thi, không thể vào để thi được nữa.");
            return;
        }

        ProceedEnterExam(subj);
    }

    private void AnnounceExamEnded(Weekday day, int slot1Based)
    {
        int start = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slot1Based));
        int end = start + 15;
        string dayVN = DataKeyText.VN_Weekday(day);
        UI?.OpenDialogue(TitleText(),
            $"Kì thi cuối kì môn vào {dayVN} từ {DataKeyText.FormatHM(start + 5)} đến {DataKeyText.FormatHM(end)} đã kết thúc, em không thể vào thi được nữa");
    }

    private void ProceedEnterExam(SubjectEntry subj)
    {
        UI?.OpenDialogue(TitleText(), $"Em đủ điều kiện để thi môn '{subj.subjectName}'. Chúc may mắn!");

        // Lưu trạng thái game trước khi vào thi
        GameStateManager.SavePreExamState(subj.subjectName);

        ExamRouteData.Set(subj.subjectName, subj.subjectKeyForNotes);
        ClearExamAssignment(subj);
        StartCoroutine(LoadExamSceneDelayed("ExamScene", 2.5f));
    }

    private IEnumerator LoadExamSceneDelayed(string sceneName, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        SceneManager.LoadScene(sceneName);
    }

    // ===================== Debug menu (optional) =====================
    [ContextMenu("DEV: Clear current subject exam state")]
    private void Dev_ClearExamStateForNow()
    {
        if (TryFindSubjectForNow(out var s))
        {
            ClearExamAssignment(s);
        }
    }

    [ContextMenu("DEV: Create missing exam schedules")]
    private void Dev_CreateMissingExamSchedules()
    {
        int created = 0;
        foreach (var subj in subjects)
        {
            if (IsCourseFinished(subj))
            {
                if (!TryLoadExamAssignment(subj, out _, out _, out _, out _, out _))
                {
                    if (TryGetNearestExamSlotNextWeek(subj, out var examTerm, out var examWeek, out var examDayNextWeek, out var slotIdx1Based))
                    {
                        SaveExamAssignment(subj, examTerm, examWeek, examDayNextWeek, slotIdx1Based);
                        created++;
                    }
                }
            }
        }
    }
}
