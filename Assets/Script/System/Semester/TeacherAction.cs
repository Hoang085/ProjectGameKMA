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

    [HideInInspector] public int currentSessionIndex = 0;

    [Header("Exam Linking")]
    [Tooltip("Index của môn này trong ExamLoader.exams (trên ExamScene). -1 = không dùng, sẽ match theo tên.")]
    public int examIndexInLoader = -1;
}

public class TeacherAction : InteractableAction
{
    [Header("Config (kỳ hiện tại của NPC)")]
    public SemesterConfig semesterConfig;

    [Header("Auto switch by term")]
    [SerializeField] private List<SemesterConfig> configsByTerm = new(); 
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
    public string exceededAbsenceText = "Em đã nghỉ quá số buổi quy định hoặc không vượt qua điểm quá trình cho phép";

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

    private int GetCurrentTerm() => Clock ? Clock.Term : 1;

    private int GetWeeksPerTerm()
    {
        if (semesterConfig != null && semesterConfig.Weeks > 0) return semesterConfig.Weeks;

        return Mathf.Max(1, weeksPerTerm);
    }

    private SemesterConfig FindConfigForTerm(int term)
    {
        if (term >= 1 && term <= configsByTerm.Count && configsByTerm[term - 1] != null)
            return configsByTerm[term - 1];

        var res = Resources.Load<SemesterConfig>($"Semester{term}Config");
        if (res != null) return res;

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

        for (int i = 0; i < subjects.Count; i++)
            subjects[i].currentSessionIndex = LoadProgress(subjects[i]);
    }

    private int GetTeacherSemester()
        => (semesterConfig != null && semesterConfig.Semester > 0) ? semesterConfig.Semester : GetCurrentTerm();

    private bool IsSubjectActiveNow() => Clock && GetTeacherSemester() == Clock.Term;

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

    private string ExamKeyPrefix(SubjectEntry s)
    {
        int term = GetTeacherSemester();
        return $"T{term}_EXAM_{GetStableSubjectKey(s)}";
    }
    
    // **MỚI: Retake exam key prefix**
    private string RetakeExamKeyPrefix(SubjectEntry s)
    {
        int term = GetTeacherSemester();
        return $"T{term}_RETAKE_{GetStableSubjectKey(s)}";
    }
    
    private void SaveExamAssignment(SubjectEntry s, int assignedTerm, int assignedWeek, Weekday day, int slot1Based)
    {
        string p = ExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_term", assignedTerm);
        PlayerPrefs.SetInt(p + "_week", assignedWeek);
        PlayerPrefs.SetInt(p + "_day", (int)day);
        PlayerPrefs.SetInt(p + "_slot1Based", slot1Based);
        PlayerPrefs.DeleteKey(p + "_missed");
        PlayerPrefs.DeleteKey(p + "_taken"); // **MỚI: Track xem đã thi chưa**
        PlayerPrefs.Save();
    }
    
    // **MỚI: Save retake exam assignment - lấy ca gần nhất SAU lần thi đầu**
    private void SaveRetakeExamAssignment(SubjectEntry s)
    {
        // Lấy lịch thi lần đầu để tìm ca sau đó
        if (!TryLoadExamAssignment(s, out int originalTerm, out int originalWeek, out Weekday originalDay, out int originalSlot, out _, out _))
        {
            Debug.LogWarning($"[TeacherAction] Không tìm thấy lịch thi lần đầu cho {s.subjectName}");
            return;
        }
        
        // Tìm ca gần nhất sau lần thi đầu
        if (!TryGetNearestSessionAfter(s, originalTerm, originalWeek, originalDay, originalSlot, 
            out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot))
        {
            Debug.LogWarning($"[TeacherAction] Không tìm thấy ca nào sau lần thi đầu cho {s.subjectName}");
            return;
        }
        
        string p = RetakeExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_term", retakeTerm);
        PlayerPrefs.SetInt(p + "_week", retakeWeek);
        PlayerPrefs.SetInt(p + "_day", (int)retakeDay);
        PlayerPrefs.SetInt(p + "_slot1Based", retakeSlot);
        PlayerPrefs.DeleteKey(p + "_missed");
        PlayerPrefs.DeleteKey(p + "_taken");
        
        PlayerPrefs.Save();
        
        string dayVN = DataKeyText.VN_Weekday(retakeDay);
        int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(retakeSlot));
        string timeStr = DataKeyText.FormatHM(startMin);
        
        Debug.Log($"[TeacherAction] Đã tạo lịch thi lại cho {s.subjectName}: {dayVN} ca {retakeSlot} ({timeStr}) - Tuần {retakeWeek}, Kỳ {retakeTerm}");
    }
    
    private bool TryLoadExamAssignment(SubjectEntry s, out int term, out int week, out Weekday day, out int slot1Based, out bool missed, out bool taken)
    {
        string p = ExamKeyPrefix(s);
        term = week = slot1Based = 0;
        day = Weekday.Mon;
        missed = false;
        taken = false;

        if (!PlayerPrefs.HasKey(p + "_day") || !PlayerPrefs.HasKey(p + "_slot1Based"))
            return false;

        term = PlayerPrefs.GetInt(p + "_term", 0);
        week = PlayerPrefs.GetInt(p + "_week", 0);
        day = (Weekday)PlayerPrefs.GetInt(p + "_day");
        slot1Based = PlayerPrefs.GetInt(p + "_slot1Based");
        missed = PlayerPrefs.GetInt(p + "_missed", 0) == 1;
        taken = PlayerPrefs.GetInt(p + "_taken", 0) == 1;
        return true;
    }
    
    // **MỚI: Try load retake exam assignment**
    private bool TryLoadRetakeExamAssignment(SubjectEntry s, out int term, out int week, out Weekday day, out int slot1Based, out bool missed, out bool taken)
    {
        string p = RetakeExamKeyPrefix(s);
        term = week = slot1Based = 0;
        day = Weekday.Mon;
        missed = false;
        taken = false;

        if (!PlayerPrefs.HasKey(p + "_day") || !PlayerPrefs.HasKey(p + "_slot1Based"))
            return false;

        term = PlayerPrefs.GetInt(p + "_term", 0);
        week = PlayerPrefs.GetInt(p + "_week", 0);
        day = (Weekday)PlayerPrefs.GetInt(p + "_day");
        slot1Based = PlayerPrefs.GetInt(p + "_slot1Based");
        missed = PlayerPrefs.GetInt(p + "_missed", 0) == 1;
        taken = PlayerPrefs.GetInt(p + "_taken", 0) == 1;
        return true;
    }
    
    private void MarkExamMissed(SubjectEntry s)
    {
        string p = ExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_missed", 1);
        PlayerPrefs.Save();
    }
    
    // **MỚI: Mark exam as taken (đã thi rồi)**
    private void MarkExamTaken(SubjectEntry s)
    {
        string p = ExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_taken", 1);
        PlayerPrefs.Save();
    }
    
    // **MỚI: Mark retake exam as missed**
    private void MarkRetakeExamMissed(SubjectEntry s)
    {
        string p = RetakeExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_missed", 1);
        PlayerPrefs.Save();
    }
    
    // **MỚI: Mark retake exam as taken**
    private void MarkRetakeExamTaken(SubjectEntry s)
    {
        string p = RetakeExamKeyPrefix(s);
        PlayerPrefs.SetInt(p + "_taken", 1);
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
        PlayerPrefs.DeleteKey(p + "_taken");
        PlayerPrefs.Save();
    }
    
    // **MỚI: Clear retake exam assignment**
    private void ClearRetakeExamAssignment(SubjectEntry s)
    {
        string p = RetakeExamKeyPrefix(s);
        PlayerPrefs.DeleteKey(p + "_term");
        PlayerPrefs.DeleteKey(p + "_week");
        PlayerPrefs.DeleteKey(p + "_day");
        PlayerPrefs.DeleteKey(p + "_slot1Based");
        PlayerPrefs.DeleteKey(p + "_missed");
        PlayerPrefs.DeleteKey(p + "_taken");
        PlayerPrefs.Save();
    }

    private IEnumerator CreateMissingExamSchedulesDelayed()
    {
        yield return new WaitForSeconds(1f); 

        if (!IsSubjectActiveNow()) yield break;

        foreach (var subj in subjects)
        {
            // **CẢI THIỆN: Kiểm tra và tạo lịch thi cho MỌI môn đã hoàn thành (kể cả nghỉ)**
            CheckAndCreateExamScheduleIfFinished(subj);
            
            // **MỚI: Kiểm tra xem có cần tạo lịch thi lại không**
            CheckAndCreateRetakeSchedule(subj);
        }
    }

    /// <summary>
    /// **CẢI THIỆN: Tự động kiểm tra và tạo lịch thi nếu môn đã hoàn thành (bao gồm cả nghỉ)**
    /// </summary>
    private void CheckAndCreateExamScheduleIfFinished(SubjectEntry subj)
    {
        if (!autoCreateExamSchedule) return;
        if (!IsSubjectActiveNow()) return;

        // **QUAN TRỌNG: Kiểm tra IsCourseFinished bao gồm cả nghỉ**
        if (!IsCourseFinished(subj))
        {
            return; // Chưa hoàn thành đủ số buổi (học + nghỉ)
        }

        // **MỚI: Kiểm tra xem đã có lịch thi chưa**
        if (TryLoadExamAssignment(subj, out _, out _, out _, out _, out _, out _))
        {
            Debug.Log($"[TeacherAction] Môn {subj.subjectName} đã có lịch thi");
            return;
        }

        // **TẠO LỊCH THI CHO MÔN ĐÃ HOÀN THÀNH**
        if (TryGetNearestExamSlotNextWeek(subj, out var examTerm, out var examWeek, out var examDayNextWeek, out var slotIdx1Based))
        {
            SaveExamAssignment(subj, examTerm, examWeek, examDayNextWeek, slotIdx1Based);
            
            string dayVN = DataKeyText.VN_Weekday(examDayNextWeek);
            int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slotIdx1Based));
            string timeStr = DataKeyText.FormatHM(startMin);
            
            Debug.Log($"[TeacherAction] ✓ Đã tạo lịch thi tự động cho {subj.subjectName}: {dayVN} ca {slotIdx1Based} ({timeStr}) - Tuần {examWeek}, Kỳ {examTerm}");
        }
        else
        {
            Debug.LogWarning($"[TeacherAction] Không tìm thấy ca thi cho {subj.subjectName}");
        }
    }

    /// <summary>
    /// Tự động tạo lịch thi nếu môn đã hoàn thành nhưng chưa có
    /// </summary>
    private void EnsureExamScheduleExists(SubjectEntry subj)
    {
        if (!autoCreateExamSchedule) return;
        if (!IsSubjectActiveNow()) return; 

        if (TryLoadExamAssignment(subj, out _, out _, out _, out _, out _, out _))
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
    
    /// <summary>
    /// **MỚI: Kiểm tra và tạo lịch thi lại nếu cần**
    /// </summary>
    private void CheckAndCreateRetakeSchedule(SubjectEntry subj)
    {
        string subjectKey = GetStableSubjectKey(subj);
        
        // Kiểm tra xem có flag yêu cầu tạo lịch thi lại không
        if (PlayerPrefs.GetInt($"NEEDS_RETAKE_SCHEDULE_{subjectKey}", 0) != 1)
        {
            return;
        }
        
        // **KHÔNG XÓA FLAG Ở ĐÂY** - chỉ xóa khi thi lại xong trong GameUIManager
        
        // Kiểm tra xem đã có lịch thi lại chưa
        if (TryLoadRetakeExamAssignment(subj, out _, out _, out _, out _, out _, out _))
        {
            Debug.Log($"[TeacherAction] Đã có lịch thi lại cho {subj.subjectName}");
            return;
        }
        
        // **MỚI: Tạo lịch thi lại dựa trên lịch thi đầu tiên**
        if (!TryLoadExamAssignment(subj, out int examTerm, out int examWeek, out Weekday examDay, out int examSlot, out _, out _))
        {
            Debug.LogError($"[TeacherAction] Không tìm thấy lịch thi đầu cho {subj.subjectName}");
            return;
        }
        
        Debug.Log($"[TeacherAction] Lịch thi đầu: Term={examTerm}, Week={examWeek}, Day={examDay}, Slot={examSlot}");
        
        // Tìm ca gần nhất sau lịch thi đầu
        if (TryGetNearestSessionAfter(subj, examTerm, examWeek, examDay, examSlot,
            out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot))
        {
            // Lưu lịch thi lại
            string p = RetakeExamKeyPrefix(subj);
            PlayerPrefs.SetInt(p + "_term", retakeTerm);
            PlayerPrefs.SetInt(p + "_week", retakeWeek);
            PlayerPrefs.SetInt(p + "_day", (int)retakeDay);
            PlayerPrefs.SetInt(p + "_slot1Based", retakeSlot);
            PlayerPrefs.DeleteKey(p + "_missed");
            PlayerPrefs.DeleteKey(p + "_taken");
            PlayerPrefs.Save();
            
            string dayVN = DataKeyText.VN_Weekday(retakeDay);
            int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(retakeSlot));
            string timeStr = DataKeyText.FormatHM(startMin);
            
            Debug.Log($"[TeacherAction] ✓ Đã tạo lịch thi lại cho {subj.subjectName}: {dayVN} ca {retakeSlot} ({timeStr}) - Tuần {retakeWeek}, Kỳ {retakeTerm}");
        }
        else
        {
            Debug.LogError($"[TeacherAction] ✗ Không thể tìm ca thi lại cho {subj.subjectName}");
        }
    }
    
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

    private bool TryFindSubjectForNow(out SubjectEntry subj)
    {
        subj = null;
        if (!Clock || !semesterConfig || subjects == null || subjects.Count == 0) return false;

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
        
        // **MỚI: Kiểm tra và cập nhật trạng thái buttons**
        UpdateExamButtonsState();
        
        UI.OpenDialogue(TitleText(), openText);
    }
    
    /// <summary>
    /// **MỚI: Cập nhật trạng thái hiển thị các nút thi dựa trên tình trạng môn học**
    /// </summary>
    private void UpdateExamButtonsState()
    {
        if (!TryFindSubjectForNow(out var subj))
        {
            // Không có môn học hiện tại -> ẩn nút "Thi lại"
            UI?.UpdateExamButtonsVisibility(showRetake: false);
            return;
        }
        
        subj.currentSessionIndex = LoadProgress(subj);
        
        // Chỉ hiển thị nút thi khi đã hoàn thành môn học
        if (!IsCourseFinished(subj))
        {
            UI?.UpdateExamButtonsVisibility(showRetake: false);
            return;
        }
        
        string subjectKey = GetStableSubjectKey(subj);
        
        // **MỚI: Kiểm tra xem có flag NEEDS_RETAKE_SCHEDULE không**
        bool needsRetakeSchedule = PlayerPrefs.GetInt($"NEEDS_RETAKE_SCHEDULE_{subjectKey}", 0) == 1;
        
        bool showRetakeButton = false;
        
        // **LOGIC ĐƠN GIẢN HƠN:**
        // 1. Nếu có flag NEEDS_RETAKE_SCHEDULE -> tự động tạo lịch (nếu chưa có) và hiện nút
        // 2. Nếu không có flag -> kiểm tra lịch thi lại có tồn tại và chưa thi/bỏ
        
        if (needsRetakeSchedule)
        {
            // **QUAN TRỌNG: Có flag -> đảm bảo có lịch thi lại bằng cách gọi CheckAndCreateRetakeSchedule**
            CheckAndCreateRetakeSchedule(subj);
            
            // Kiểm tra xem đã tạo thành công chưa
            if (TryLoadRetakeExamAssignment(subj, out _, out _, out _, out _, out _, out _))
            {
                showRetakeButton = true;
                Debug.Log($"[TeacherAction] ✓ Hiện nút 'Thi lại' cho {subj.subjectName} (có flag NEEDS_RETAKE_SCHEDULE)");
            }
            else
            {
                Debug.LogWarning($"[TeacherAction] Không thể tạo lịch thi lại cho {subj.subjectName}");
            }
        }
        else
        {
            // Không có flag -> kiểm tra lịch thi lại có tồn tại và chưa thi/bỏ
            if (TryLoadRetakeExamAssignment(subj, 
                out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot, 
                out bool retakeMissed, out bool retakeTaken))
            {
                if (!retakeTaken && !retakeMissed)
                {
                    showRetakeButton = true;
                    Debug.Log($"[TeacherAction] ✓ Hiện nút 'Thi lại' cho {subj.subjectName} (có lịch thi lại)");
                }
            }
        }
        
        UI?.UpdateExamButtonsVisibility(showRetake: showRetakeButton);
    }

    public void UI_Close()
    {
        UI?.CloseDialogue(unbindTeacher: true);
        _state = State.Idle;
    }

    private string TitleText() => string.IsNullOrWhiteSpace(titleText) ? "No Title" : titleText;

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
        
        Debug.Log($"[TeacherAction] ========================================");
        Debug.Log($"[TeacherAction][{name}] UI_StartClass for subject: {subj.subjectName}");
        Debug.Log($"[TeacherAction][{name}] onClassStarted is NULL? {(onClassStarted == null ? "YES" : "NO")}");
        
        // **FALLBACK: Gọi trực tiếp PlayerStatsUI nếu event không hoạt động**
        var playerStatsUI = Object.FindFirstObjectByType<PlayerStatsUI>();
        if (playerStatsUI != null)
        {
            Debug.Log($"[TeacherAction][{name}] ✓ Found PlayerStatsUI, calling ConsumeStaminaForClass directly");
            playerStatsUI.ConsumeStaminaForClass();
        }
        else
        {
            Debug.LogWarning($"[TeacherAction][{name}] ✗ PlayerStatsUI NOT FOUND in scene!");
        }
        
        if (onClassStarted != null)
        {
            int listenerCount = onClassStarted.GetPersistentEventCount();
            Debug.Log($"[TeacherAction][{name}] Invoking onClassStarted event");
            Debug.Log($"[TeacherAction][{name}] Persistent listeners count: {listenerCount}");
            
            // Log persistent listener info
            for (int i = 0; i < listenerCount; i++)
            {
                var target = onClassStarted.GetPersistentTarget(i);
                var methodName = onClassStarted.GetPersistentMethodName(i);
                Debug.Log($"[TeacherAction][{name}]   Persistent[{i}]: {target?.GetType().Name}.{methodName}");
            }
            
            onClassStarted.Invoke();
            Debug.Log($"[TeacherAction][{name}] ✓ onClassStarted.Invoke() completed");
        }
        else
        {
            Debug.LogError($"[TeacherAction][{name}] ✗ onClassStarted is NULL! Cannot invoke!");
        }
        
        Debug.Log($"[TeacherAction] ========================================");
        
        UI?.StartQuizForSubject(quizKey);
    }
    
    
    private void AddNoteIfNeeded(SubjectEntry subj)
    {
        if (!addNoteWhenFinished || NotesService.Instance == null) return;
        if (!IsSubjectActiveNow()) return; 

        string key = !string.IsNullOrWhiteSpace(subj.subjectKeyForNotes) ? subj.subjectKeyForNotes : subj.subjectName;

        int attendedSoFar = Mathf.Max(0, subj.currentSessionIndex);
        int absencesSoFar = GetAbsencesFor(subj);
        int realSessionIndex = attendedSoFar + absencesSoFar;

        int sessionIndex = noteSessionIndex > 0 ? noteSessionIndex : (realSessionIndex + 1);

        NotesService.Instance.AddNoteRef(key, sessionIndex, subj.subjectName);

        var b = Object.FindFirstObjectByType<BackpackUIManager>();
        if (b && b.gameObject.activeInHierarchy) b.RefreshNoteButtons();
    }

    public void CompleteClassAfterQuiz(bool quizPassed = true)
    {
        if (!TryFindSubjectForNow(out var subj))
        {
            return;
        }

        StartCoroutine(CompleteClassRoutine(subj, quizPassed));
    }

    private IEnumerator CompleteClassRoutine(SubjectEntry subj, bool quizPassed)
    {
        _state = State.InClass;
        var att = AttendanceManager.Instance;

        AddNoteIfNeeded(subj);

        if (!quizPassed)
        {
            if (att != null)
            {
                att.CancelAttendanceAndMarkAbsent(subj.subjectName);
            }

            string failMsg = $"Chưa đạt yêu cầu kiểm tra! Em chưa đạt điểm tối thiểu trong bài kiểm tra quá trình học. Buổi học này sẽ được tính là vắng mặt. Tài liệu buổi học đã được thêm vào Túi đồ. Lưu ý: Nếu vắng quá số buổi quy định, em sẽ bị cấm thi!";

            UI?.OpenDialogue(TitleText(), failMsg);
            yield return new WaitForSecondsRealtime(4.5f);

            // **MỚI: Kiểm tra xem môn có vừa hoàn thành không (sau khi nghỉ)**
            bool justFinishedByAbsence = IsCourseFinished(subj);
            
            if (att != null && att.HasExceededAbsences(subj.subjectName, GetTeacherSemester()))
            {
                string warnMsg = $"Em đã vắng {att.GetAbsences(subj.subjectName, GetTeacherSemester())} hoặc không vượt điểm quá trình buổi học môn {subj.subjectName}. Em đã bị cấm thi môn này do nghỉ quá số buổi quy định!";

                UI?.OpenDialogue(TitleText(), warnMsg);
                yield return new WaitForSecondsRealtime(5f);
            }
            else if (justFinishedByAbsence)
            {
                // **MỚI: Tạo lịch thi nếu vừa hoàn thành bằng việc nghỉ (nhưng chưa quá giới hạn)**
                CheckAndCreateExamScheduleIfFinished(subj);
                
                // Kiểm tra xem có lịch thi không để thông báo
                if (TryLoadExamAssignment(subj, out int examTerm, out int examWeek, out Weekday examDay, out int examSlot, out _, out _))
                {
                    string dayVN = DataKeyText.VN_Weekday(examDay);
                    int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(examSlot));
                    string timeStr = DataKeyText.FormatHM(startMin);
                    
                    string examNoticeMsg = $"Lưu ý: Em đã hoàn thành môn {subj.subjectName} (tính cả nghỉ). Lịch thi: {dayVN} - Ca {examSlot} ({timeStr}). Xem chi tiết trong Bảng lịch thi!";
                    
                    UI?.OpenDialogue(TitleText(), examNoticeMsg);
                    yield return new WaitForSecondsRealtime(4.5f);
                }
            }

            // **QUAN TRỌNG: Đóng dialogue VÀ unbind teacher**
            UI?.CloseDialogue(unbindTeacher: true);
            yield return new WaitForSecondsRealtime(0.5f);

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

        // **LOGIC CŨ: Đạt quiz -> confirm attendance và tiếp tục**
        if (att != null)
        {
            att.ConfirmAttendance(subj.subjectName);
        }
        
        UI?.OpenDialogue(TitleText(), "Hoàn thành buổi học! Tài liệu đã được thêm vào Túi đồ. Đang xử lý kết quả...");
        yield return new WaitForSecondsRealtime(1.5f);

        int abs = GetAbsencesFor(subj);
        int cap = Mathf.Max(1, subj.maxSessions);
        int attended = Mathf.Max(0, subj.currentSessionIndex);
        int attendedCap = Mathf.Max(0, cap - abs);

        // **SỬA: Tăng attended TRƯỚC khi kiểm tra justFinished**
        int newAttended = attended + 1;
        
        if (attended < attendedCap)
        {
            subj.currentSessionIndex = newAttended;
            SaveProgress(subj);
            Debug.Log($"[TeacherAction] Tăng progress: {attended} -> {newAttended} / {attendedCap} (cap: {cap}, abs: {abs})");
        }

        // **SỬA: Kiểm tra justFinished dựa trên giá trị MỚI**
        bool justFinished = (newAttended >= attendedCap);
        
        Debug.Log($"[TeacherAction] justFinished check: newAttended={newAttended}, attendedCap={attendedCap}, justFinished={justFinished}");
        
        if (justFinished)
        {
            // **CẢI THIỆN: Sử dụng hàm CheckAndCreateExamScheduleIfFinished thống nhất**
            CheckAndCreateExamScheduleIfFinished(subj);
            
            // Kiểm tra và hiển thị thông báo lịch thi
            if (TryLoadExamAssignment(subj, out var examTerm, out var examWeek, out var examDayNextWeek, out var slotIdx1Based, out _, out _))
            {
                string dayVN = DataKeyText.VN_Weekday(examDayNextWeek);
                int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slotIdx1Based));
                string timeStr = DataKeyText.FormatHM(startMin);

                string examMsg = $"Chúc mừng! Em đã hoàn thành tất cả buổi học môn {subj.subjectName}. Lịch thi: Ngày: {dayVN} Giờ: {timeStr}. Lưu ý: Em chỉ được phép thi vào đúng ca này!";
                UI?.OpenDialogue(TitleText(), examMsg);
                
                Debug.Log($"[TeacherAction] ✓ Hiển thị thông báo lịch thi cho {subj.subjectName}: {dayVN} - Ca {slotIdx1Based} ({timeStr})");
            }
            else
            {
                Debug.LogWarning($"[TeacherAction] Không tìm thấy lịch thi tuần sau cho {subj.subjectName}");
                UI?.OpenDialogue(TitleText(), "Không tìm thấy lịch thi tuần sau cho môn này. Kiểm tra SemesterConfig / ScheduleResolver.");
            }

            yield return new WaitForSecondsRealtime(5.0f); // Tăng thời gian để đọc message
        }

        UI?.CloseDialogue(unbindTeacher: true);
        yield return new WaitForSecondsRealtime(0.5f);

        Debug.Log($"[TeacherAction] Chuyển sang ca tiếp theo");
        if (Clock) 
        {
            Clock.JumpToNextSessionStart();
            Debug.Log($"[TeacherAction] Đã gọi Clock.JumpToNextSessionStart() - Ca mới: {Clock.Slot}");
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

    private int GetNearestExamTerm(int startTerm, int maxOffset = 3)
    {
        int curTerm = startTerm;
        int wpt = GetWeeksPerTerm();

        for (int offset = 0; offset < maxOffset; offset++)
        {
            curTerm += offset;
            if (curTerm < 1) continue;

            // Tìm kỳ có tuần 1 vào thứ 2 trong semester config
            if (configsByTerm.Count >= curTerm && configsByTerm[curTerm - 1]?.Weeks >= 1)
            {
                return curTerm;
            }
        }

        return startTerm;
    }
    
    private bool TryGetNearestExamSlotNextWeek(
        SubjectEntry subj,
        out int examTerm,
        out int examWeek,
        out Weekday day,
        out int slotIdx1Based)
    {
        int curTerm = GetTeacherSemester(); 
        int curWeek = Clock ? Mathf.Max(1, Clock.Week) : 1;
        int wpt = GetWeeksPerTerm();

        examTerm = curTerm;
        int nextWeek = curWeek + 1;
        if (nextWeek > wpt) nextWeek = wpt;
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
    
    /// <summary>
    /// **MỚI: Tìm ca gần nhất SAU một thời điểm cho trước (để thi lại)**
    /// </summary>
    private bool TryGetNearestSessionAfter(
        SubjectEntry subj,
        int afterTerm,
        int afterWeek,
        Weekday afterDay,
        int afterSlot,
        out int foundTerm,
        out int foundWeek,
        out Weekday foundDay,
        out int foundSlot)
    {
        foundTerm = afterTerm;
        foundWeek = afterWeek;
        foundDay = Weekday.Mon;
        foundSlot = 1;

        if (semesterConfig == null || string.IsNullOrWhiteSpace(subj.subjectName))
            return false;

        int wpt = GetWeeksPerTerm();

        // Tìm kiếm từ ca tiếp theo cho đến hết kỳ
        for (int w = afterWeek; w <= wpt; w++)
        {
            int startDay = (w == afterWeek) ? ((int)afterDay) : (int)Weekday.Mon;
            
            for (int d = startDay; d <= (int)Weekday.Sun; d++)
            {
                int startSlot = (w == afterWeek && d == (int)afterDay) ? (afterSlot + 1) : 1;
                
                for (int sIdx = startSlot; sIdx <= 5; sIdx++)
                {
                    if (ScheduleResolver.IsSessionMatch(semesterConfig, subj.subjectName, (Weekday)d, sIdx))
                    {
                        foundWeek = w;
                        foundDay = (Weekday)d;
                        foundSlot = sIdx;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private int CompareCalendarPos(int termA, int weekA, Weekday dayA, int slotA, int termB, int weekB, Weekday dayB, int slotB)
    {
        if (termA != termB) return termA < termB ? -1 : 1;
        if (weekA != weekB) return weekA < weekB ? -1 : 1;
        if (dayA != dayB) return ((int)dayA) < ((int)dayB) ? -1 : 1; 
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

        EnsureExamScheduleExists(subj);
        
        // **SỬA: Chỉ xử lý thi lần đầu, không xử lý thi lại ở đây nữa**
        if (!TryLoadExamAssignment(subj, out int aTerm, out int aWeek, out Weekday aDay, out int aSlot1, out bool missed, out bool taken))
        {
            UI?.OpenDialogue(TitleText(), "Không thể tạo lịch thi cho môn này. Kiểm tra cấu hình SemesterConfig.");
            return;
        }
        
        // **KIỂM TRA: Đã thi rồi -> KHÔNG CHO THI NỮA**
        if (taken)
        {
            UI?.OpenDialogue(TitleText(), "Em đã hoàn thành kỳ thi này rồi. Không thể thi lại.");
            return;
        }
        
        // **KIỂM TRA: Bỏ thi -> KHÔNG CHO THI NỮA**
        if (missed)
        {
            UI?.OpenDialogue(TitleText(), "Em đã bỏ lỡ kỳ thi này. Không thể thi được nữa.");
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

        // **KIỂM TRA ĐÚNG THỜI GIAN**
        bool correctTime = (nTerm == aTerm && nWeek == aWeek && nDay == aDay && nSlot1 == aSlot1);
        
        if (!correctTime)
        {
            string dayVN = DataKeyText.VN_Weekday(aDay);
            int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(aSlot1));
            string timeStr = DataKeyText.FormatHM(startMin);
            
            UI?.OpenDialogue(TitleText(), 
                $"Chưa đúng giờ thi!\nLịch thi: {dayVN} - Ca {aSlot1} ({timeStr})");
            return;
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
    
    /// <summary>
    /// **MỚI: Xử lý thi lại - riêng biệt với thi lần đầu**
    /// Được gọi từ nút "Thi lại" trong GameUIManager
    /// </summary>
    public void UI_TakeRetakeExam()
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

        if (!IsCourseFinished(subj))
        {
            UI?.OpenDialogue(TitleText(), "Em chưa hoàn thành đủ số buổi môn này");
            return;
        }
        
        // **KIỂM TRA: Có lịch thi lại không**
        if (!TryLoadRetakeExamAssignment(subj, 
            out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot, 
            out bool retakeMissed, out bool retakeTaken))
        {
            UI?.OpenDialogue(TitleText(), "Em chưa có lịch thi lại cho môn này.");
            return;
        }
        
        // **KIỂM TRA: Đã thi lại rồi**
        if (retakeTaken)
        {
            UI?.OpenDialogue(TitleText(), "Em đã hoàn thành kỳ thi lại này rồi.");
            return;
        }
        
        // **KIỂM TRA: Đã bỏ thi lại**
        if (retakeMissed)
        {
            UI?.OpenDialogue(TitleText(), "Em đã bỏ lỡ kỳ thi lại này. Đây là cơ hội cuối cùng mà em đã không tận dụng.");
            return;
        }
        
        HandleRetakeExam(subj, retakeTerm, retakeWeek, retakeDay, retakeSlot);
    }
    
    /// <summary>
    /// **MỚI: Xử lý thi lại - đã được tách riêng**
    /// </summary>
    private void HandleRetakeExam(SubjectEntry subj, int retakeTerm, int retakeWeek, Weekday retakeDay, int retakeSlot)
    {
        Debug.Log($"[TeacherAction] Chế độ thi lại cho {subj.subjectName}");
        
        if (Clock == null)
        {
            UI?.OpenDialogue(TitleText(), "Đồng hồ chưa sẵn sàng.");
            return;
        }

        int currentTerm = GetCurrentTerm();
        int currentWeek = Mathf.Max(1, Clock.Week);
        Weekday currentDay = Clock.Weekday;
        int currentSlot = Clock.SlotIndex1Based;
        
        // Kiểm tra đúng thời gian thi lại
        bool correctTime = (currentTerm == retakeTerm && currentWeek == retakeWeek && currentDay == retakeDay && currentSlot == retakeSlot);
        
        if (!correctTime)
        {
            string dayVN = DataKeyText.VN_Weekday(retakeDay);
            int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(retakeSlot));
            string timeStr = DataKeyText.FormatHM(startMin);
            
            UI?.OpenDialogue(TitleText(), 
                $"Chưa đúng giờ thi lại!\nLịch thi lại: {dayVN} - Ca {retakeSlot} ({timeStr})");
            return;
        }
        
        // Kiểm tra thời gian vào thi
        var retakeAttMgr = AttendanceManager.Instance;
        if (retakeAttMgr == null)
        {
            UI?.OpenDialogue(TitleText(), "Không tìm thấy AttendanceManager.");
            return;
        }

        int retakeSlotStart = retakeAttMgr.GetSlotStart(Clock.Slot);
        int retakeExamStart = retakeSlotStart + 5;
        int retakeExamEnd = retakeSlotStart + 15;
        int retakeNow = Clock.MinuteOfDay;

        if (retakeNow < retakeExamStart)
        {
            UI?.OpenDialogue(TitleText(),
                $"Chưa đúng giờ bắt đầu thi lại. Em chỉ được phép vào thi từ {DataKeyText.FormatHM(retakeExamStart)} đến {DataKeyText.FormatHM(retakeExamEnd)}.");
            return;
        }
        if (retakeNow >= retakeExamEnd)
        {
            MarkRetakeExamMissed(subj);
            UI?.OpenDialogue(TitleText(),
                "Em đã quá giờ điểm danh vào thi lại. Đây là cơ hội cuối cùng mà em đã bỏ lỡ!");
            return;
        }
        
        // Cho phép vào thi lại
        ProceedEnterRetakeExam(subj);
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

        GameStateManager.SavePreExamState(subj.subjectName);

        ExamRouteData.Set(subj.subjectName, subj.subjectKeyForNotes);
        
        // **MỚI: Xóa flag retake (nếu có) vì đây là lần thi đầu**
        PlayerPrefs.DeleteKey("EXAM_IS_RETAKE");
        PlayerPrefs.Save();
        
        // **QUAN TRỌNG: Đánh dấu đã thi để không cho thi lại lần thứ 2**
        MarkExamTaken(subj);
        
        StartCoroutine(LoadExamSceneDelayed("ExamScene", 2.5f));
    }
    
    /// <summary>
    /// **MỚI: Proceed vào thi lại**
    /// </summary>
    private void ProceedEnterRetakeExam(SubjectEntry subj)
    {
        UI?.OpenDialogue(TitleText(), $"Đây là cơ hội cuối cùng! Em đủ điều kiện thi lại môn '{subj.subjectName}'. Chúc may mắn!");

        GameStateManager.SavePreExamState(subj.subjectName);

        ExamRouteData.Set(subj.subjectName, subj.subjectKeyForNotes);
        
        // **MỚI: Set flag để ExamUIManager biết đây là lần thi lại**
        PlayerPrefs.SetInt("EXAM_IS_RETAKE", 1);
        PlayerPrefs.Save();
        
        // **MỚI: Đánh dấu đã thi lại**
        MarkRetakeExamTaken(subj);
        
        StartCoroutine(LoadExamSceneDelayed("ExamScene", 2.5f));
    }

    private IEnumerator LoadExamSceneDelayed(string sceneName, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        SceneLoader.Load(sceneName);
    }
}
