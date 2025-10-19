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
    [Header("Config")]
    public SemesterConfig semesterConfig;

    [Header("Các môn giảng dạy")]
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

    [Header("Calendar")]
    [Tooltip("Số tuần mỗi kỳ (fallback nếu không đọc được từ GameClock/CalendarConfig).")]
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

    // ==== DEBUG HELPERS ====
#if UNITY_EDITOR
    private void DBG(string msg) => Debug.Log($"[TeacherAction][{name}] {msg}");
#else
    private void DBG(string msg) => Debug.Log($"[TeacherAction] {msg}");
#endif

    [ContextMenu("DEV: Clear current subject exam state")]
    private void Dev_ClearExamStateForNow()
    {
        if (TryFindSubjectForNow(out var s))
        {
            ClearExamAssignment(s);
            DBG($"DEV clear exam state for subject '{s.subjectName}'");
        }
        else
        {
            DBG("DEV clear exam state: no subject found for NOW.");
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
                        
                        string dayVN = DataKeyText.VN_Weekday(examDayNextWeek);
                        int startMin = DataKeyText.TryGetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slotIdx1Based));
                        string timeStr = DataKeyText.FormatHM(startMin);
                        
                        DBG($"Tạo lịch thi cho môn '{subj.subjectName}': {dayVN} lúc {timeStr}");
                    }
                }
            }
        }
        DBG($"Đã tạo {created} lịch thi bị thiếu.");
    }

    // ---------- Key helpers ----------
    private static string NormalizeKey(string s) => (s ?? "").Trim().ToLowerInvariant();

    private string GetStableSubjectKey(SubjectEntry s)
    {
        var baseKey = !string.IsNullOrWhiteSpace(s.subjectKeyForNotes) ? s.subjectKeyForNotes : s.subjectName;
        return NormalizeKey(baseKey);
    }

    private int GetCurrentTerm() => Clock ? Clock.Term : 1;
    private int GetCurrentWeek() => Clock ? Mathf.Max(1, Clock.Week) : 1;
    private int GetWeeksPerTerm()
    {
        // Nếu GameClock có WeeksPerTerm thì ưu tiên (nhiều project đã có).
        try
        {
            if (Clock != null)
            {
                var prop = Clock.GetType().GetProperty("WeeksPerTerm");
                if (prop != null)
                {
                    object v = prop.GetValue(Clock, null);
                    if (v is int i && i > 0) return i;
                }
            }
        }
        catch { }
        // Fallback inspector
        return Mathf.Max(1, weeksPerTerm);
    }

    private string NewProgressKey(SubjectEntry s)
    {
        int term = GetCurrentTerm();
        return $"T{term}_SUBJ_{GetStableSubjectKey(s)}_session";
    }
    private string LegacyProgressKey_Term_Name(SubjectEntry s)
    {
        int term = GetCurrentTerm();
        return $"T{term}_SUBJ_{NormalizeKey(s.subjectName)}_session";
    }
    private string LegacyProgressKey_NoTerm_Name(SubjectEntry s)
    {
        return $"SUBJ_{NormalizeKey(s.subjectName)}_session";
    }

    // ---------- EXAM ASSIGNMENT ----------
    // Lưu: term, week, day, slot1Based, và cờ missed
    private string ExamKeyPrefix(SubjectEntry s)
    {
        int term = GetCurrentTerm();
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
        Debug.Log($"[TeacherAction] SaveExamAssignment: {s.subjectName} -> T{assignedTerm} W{assignedWeek} {day} slot{slot1Based}");
    }
    private bool TryLoadExamAssignment(SubjectEntry s, out int term, out int week, out Weekday day, out int slot1Based, out bool missed)
    {
        string p = ExamKeyPrefix(s);
        term = week = slot1Based = 0;
        day = Weekday.Mon;
        missed = false;

        if (!PlayerPrefs.HasKey(p + "_day") || !PlayerPrefs.HasKey(p + "_slot1Based"))
            return false;

        // term/week có thể chưa có ở bản cũ → coi như 0
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
        Debug.Log($"[TeacherAction] ClearExamAssignment: {s.subjectName}");
    }

    // ---------- EXAM SCHEDULE AUTO-CREATION ----------
    /// <summary>
    /// Tự động tạo lịch thi nếu môn đã hoàn thành nhưng chưa có lịch thi
    /// </summary>
    private void EnsureExamScheduleExists(SubjectEntry subj)
    {
        if (!autoCreateExamSchedule) return;

        // Kiểm tra xem đã có lịch thi chưa
        if (TryLoadExamAssignment(subj, out _, out _, out _, out _, out _))
        {
            DBG($"Môn '{subj.subjectName}' đã có lịch thi.");
            return; // Đã có lịch thi rồi
        }

        // Nếu môn đã hoàn thành (học + vắng >= maxSessions) thì tạo lịch thi
        if (IsCourseFinished(subj))
        {
            if (TryGetNearestExamSlotNextWeek(subj, out var examTerm, out var examWeek, out var examDayNextWeek, out var slotIdx1Based))
            {
                SaveExamAssignment(subj, examTerm, examWeek, examDayNextWeek, slotIdx1Based);
                
                string dayVN = DataKeyText.VN_Weekday(examDayNextWeek);
                int startMin = DataKeyText.TryGetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slotIdx1Based));
                string timeStr = DataKeyText.FormatHM(startMin);
                
                DBG($"Tự động tạo lịch thi cho môn '{subj.subjectName}': {dayVN} lúc {timeStr}");
            }
            else
            {
                DBG($"Không thể tạo lịch thi cho môn '{subj.subjectName}' - không tìm thấy ca phù hợp.");
            }
        }
    }

    private string TitleText() => string.IsNullOrWhiteSpace(titleText) ? "No Title" : titleText;

    // ---------- Load/Save + migrate ----------
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

    private void Awake()
    {
        for (int i = 0; i < subjects.Count; i++)
            subjects[i].currentSessionIndex = LoadProgress(subjects[i]);
            
        // Tự động tạo lịch thi cho những môn đã hoàn thành nhưng chưa có lịch
        if (autoCreateExamSchedule)
        {
            StartCoroutine(CreateMissingExamSchedulesDelayed());
        }
    }

    private IEnumerator CreateMissingExamSchedulesDelayed()
    {
        yield return new WaitForSeconds(1f); // Đợi hệ thống khởi tạo xong
        
        foreach (var subj in subjects)
        {
            if (IsCourseFinished(subj))
            {
                EnsureExamScheduleExists(subj);
            }
        }
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

    // ---------- Absences ----------
    private int GetAbsencesFor(SubjectEntry subj)
    {
        var att = AttendanceManager.Instance;
        int term = GetCurrentTerm();
        return att ? Mathf.Max(0, att.GetAbsences(subj.subjectName, term)) : 0;
    }
    private bool IsCourseFinished(SubjectEntry subj)
    {
        int attended = Mathf.Max(0, subj.currentSessionIndex);
        int abs = GetAbsencesFor(subj);
        int cap = Mathf.Max(1, subj.maxSessions);
        return (attended + abs) >= cap;
    }

    // ---------- Find subject now ----------
    private bool TryFindSubjectForNow(out SubjectEntry subj)
    {
        subj = null;
        if (!Clock || !semesterConfig || subjects == null || subjects.Count == 0) return false;
        var today = Clock.Weekday;
        var slot1Based = Clock.GetSlotIndex1Based();

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

    // ---------- Notes ----------
    private void AddNoteIfNeeded(SubjectEntry subj)
    {
        if (!addNoteWhenFinished || NotesService.Instance == null) return;

        string key = !string.IsNullOrWhiteSpace(subj.subjectKeyForNotes) ? subj.subjectKeyForNotes : subj.subjectName;

        int attendedSoFar = Mathf.Max(0, subj.currentSessionIndex);
        int absencesSoFar = GetAbsencesFor(subj);
        int realSessionIndex = attendedSoFar + absencesSoFar;

        int sessionIndex = noteSessionIndex > 0 ? noteSessionIndex : realSessionIndex;

        NotesService.Instance.AddNoteRef(key, sessionIndex, subj.subjectName);

        var b = Object.FindFirstObjectByType<BackpackUIManager>();
        if (b && b.gameObject.activeInHierarchy) b.RefreshNoteButtons();
    }

    // ---------- InteractableAction ----------
    public override void OnPlayerExit()
    {
        if (_state == State.AwaitConfirm)
        {
            _state = State.Idle;
            UI?.CloseDialogue();
            UI?.UnbindTeacher(this);
        }
    }
    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI || _state == State.InClass) return;
        _state = State.AwaitConfirm;
        _callerCache = caller;

        UI.BindTeacher(this);
        UI.OpenDialogue(TitleText(), openText);
    }
    public void UI_Close()
    {
        UI?.CloseDialogue();
        UI?.UnbindTeacher(this);
        _state = State.Idle;
    }

    // ---------- Start class ----------
    public void UI_StartClass()
    {
        if (_state == State.InClass) return;

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

        StartClass(subj);
    }

    private void StartClass(SubjectEntry subj)
    {
        var runner = UI != null ? (MonoBehaviour)UI : this;
        if (!runner || !runner.isActiveAndEnabled) return;

        if (_classRoutine != null) runner.StopCoroutine(_classRoutine);
        _classRoutine = runner.StartCoroutine(ClassRoutine(subj));
    }

    private IEnumerator ClassRoutine(SubjectEntry subj)
    {
        _state = State.InClass;
        onClassStarted?.Invoke();

        UI?.OpenDialogue(TitleText(), confirmText);
        yield return new WaitForSeconds(1.0f);

        UI?.OpenDialogue(TitleText(), learningText);
        yield return new WaitForSeconds(classSeconds);

        UI?.CloseDialogue();
        onClassFinished?.Invoke();

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
                int startMin = DataKeyText.TryGetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slotIdx1Based));
                string timeStr = DataKeyText.FormatHM(startMin);

                string examMsg = $"Chúc mừng em đã học xong môn '{subj.subjectName}'. Tuần sau em sẽ thi vào {dayVN} lúc {timeStr}. Chỉ được phép thi đúng ca này.";
                UI?.OpenDialogue(TitleText(), examMsg);
            }
            else
            {
                UI?.OpenDialogue(TitleText(), "Không tìm thấy lịch thi tuần sau cho môn này. Kiểm tra SemesterConfig / ScheduleResolver.");
            }

            yield return new WaitForSeconds(2.0f);
        }

        AddNoteIfNeeded(subj);

        var clockUI = Object.FindFirstObjectByType<ClockUI>();
        if (clockUI != null) clockUI.JumpToNextSessionNow();
        else if (Clock) Clock.NextSlot();

        _state = State.Idle;
        UI?.HideInteractPrompt();
    }

    // Lấy ca sớm nhất của TUẦN SAU (giữ nguyên kỳ hiện tại)
    private bool TryGetNearestExamSlotNextWeek(
        SubjectEntry subj,
        out int examTerm,
        out int examWeek,
        out Weekday day,
        out int slotIdx1Based)
    {
        int curTerm = GetCurrentTerm();
        int curWeek = GetCurrentWeek();
        int wpt = GetWeeksPerTerm();

        // Luôn giữ nguyên kỳ hiện tại
        examTerm = curTerm;
        int nextWeek = curWeek + 1;
        if (nextWeek > wpt)
        {
            // Nếu đã là tuần cuối kỳ -> giữ tuần cuối (tránh cuộn kỳ)
            nextWeek = wpt;
        }
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

    // So sánh vị trí thời gian trong lịch học: trả về
    //  -1 nếu (A) < (B), 0 nếu bằng, +1 nếu (A) > (B)
    private int CompareCalendarPos(int termA, int weekA, Weekday dayA, int slotA, int termB, int weekB, Weekday dayB, int slotB)
    {
        if (termA != termB) return termA < termB ? -1 : 1;
        if (weekA != weekB) return weekA < weekB ? -1 : 1;
        if (dayA != dayB) return ((int)dayA) < ((int)dayB) ? -1 : 1;
        if (slotA != slotB) return slotA < slotB ? -1 : 1;
        return 0;
    }

    // ---------- Vào thi ----------
    public void UI_TakeExam()
    {
        if (!TryFindSubjectForNow(out var subj))
        {
            UI?.OpenDialogue(TitleText(), "Không đúng ca thi hoặc môn học.");
            DBG("UI_TakeExam: Not subject time now.");
            return;
        }

        subj.currentSessionIndex = LoadProgress(subj);

        // 1) Phải hoàn thành môn
        if (!IsCourseFinished(subj))
        {
            var att = AttendanceManager.Instance;
            if (att != null && att.HasExceededAbsences(subj.subjectName, GetCurrentTerm()))
            {
                UI?.OpenDialogue(TitleText(), "Em vắng quá số buổi cho phép nên bị cấm thi");
                DBG($"UI_TakeExam: Blocked due to exceeded absences. subject={subj.subjectName}");
                return;
            }
            UI?.OpenDialogue(TitleText(), "Em chưa hoàn thành đủ số buổi môn này");
            DBG($"UI_TakeExam: Course not finished. subject={subj.subjectName}");
            return;
        }

        // **THÊM: Tự động tạo lịch thi nếu chưa có**
        EnsureExamScheduleExists(subj);

        // 2) Lấy lịch thi đã chốt
        if (!TryLoadExamAssignment(subj, out int aTerm, out int aWeek, out Weekday aDay, out int aSlot1, out bool missed))
        {
            UI?.OpenDialogue(TitleText(), "Không thể tạo lịch thi cho môn này. Kiểm tra cấu hình SemesterConfig.");
            DBG($"UI_TakeExam: Cannot create exam schedule for '{subj.subjectName}'.");
            return;
        }

        // ---- NOW (tính 1 lần) ----
        if (Clock == null)
        {
            UI?.OpenDialogue(TitleText(), "Đồng hồ chưa sẵn sàng.");
            DBG("UI_TakeExam: GameClock is null.");
            return;
        }
        int nTerm = GetCurrentTerm();
        int nWeek = GetCurrentWeek();
        Weekday nDay = Clock.Weekday;
        int nSlot1 = Clock.GetSlotIndex1Based();

        DBG($"[ExamCheck] NOW  T{nTerm} W{nWeek} {nDay} s{nSlot1} | " +
            $"ASSIGNED T{aTerm} W{aWeek} {aDay} s{aSlot1} | missed(pre)={missed}");

        // ----- Re-check missed -----
        // So sánh (Term, Week, Day, Slot) giữa NOW và lịch đã lưu
        int cmpForMissedRecheck = CompareCalendarPos(nTerm, nWeek, nDay, nSlot1, aTerm, aWeek, aDay, aSlot1);
        if (missed)
        {
            if (cmpForMissedRecheck <= 0)
            {
                // Lịch chưa vượt qua NOW -> clear missed
                SaveExamAssignment(subj, aTerm, aWeek, aDay, aSlot1);
                missed = false;
                DBG("[MissedFix] Cleared missed flag because exam is not past now.");
            }
            else
            {
                // Lịch đã ở quá khứ. Thử SELF-HEAL:
                // Nếu MÔN hiện tại trùng môn thi và ca hiện tại trùng (Thứ/Slot) với lịch đã lưu
                bool sameDaySlotNow = (nDay == aDay) && (nSlot1 == aSlot1);
                bool sameSubjectNow = true; // vì đã TryFindSubjectForNow(subj) phía trên
                if (sameSubjectNow && sameDaySlotNow)
                {
                    // Ghi đè lịch thi sang tuần/kỳ hiện tại, clear missed
                    SaveExamAssignment(subj, nTerm, nWeek, nDay, nSlot1);
                    aTerm = nTerm; aWeek = nWeek; aDay = nDay; aSlot1 = nSlot1;
                    missed = false;
                    DBG("[MissedFix] Past assignment but same day/slot NOW -> rescheduled to NOW's week/term and cleared missed.");
                }
                else
                {
                    AnnounceExamEnded(aDay, aSlot1);
                    DBG("[MissedFix] Still past exam -> ended.");
                    return;
                }
            }
        }

        // 3) So sánh thời gian (lại) sau khi có thể đã self-heal ở trên
        // ===== ĐÚNG CA THI =====
        var attMgr = AttendanceManager.Instance;
        if (attMgr == null)
        {
            UI?.OpenDialogue(TitleText(), "Không tìm thấy AttendanceManager.");
            DBG("UI_TakeExam: AttendanceManager is null.");
            return;
        }

        int slotStart = attMgr.GetSlotStart(Clock.Slot); // phút bắt đầu ca
        int examStart = slotStart + 5;
        int examEnd = slotStart + 15;

        var clockUI = Object.FindFirstObjectByType<ClockUI>();
        if (clockUI == null)
        {
            UI?.OpenDialogue(TitleText(), "ClockUI chưa sẵn sàng.");
            DBG("UI_TakeExam: ClockUI is null.");
            return;
        }
        int now = clockUI.GetMinuteOfDay();

        DBG($"[ExamWindow] start={slotStart}({DataKeyText.FormatHM(slotStart)}), window=[{examStart},{examEnd}) => [{DataKeyText.FormatHM(examStart)}..{DataKeyText.FormatHM(examEnd)}), now={now}({DataKeyText.FormatHM(now)})");

        if (now < examStart)
        {
            UI?.OpenDialogue(TitleText(),
                $"Chưa đúng giờ bắt đầu thi. Em chỉ được phép vào thi từ {DataKeyText.FormatHM(examStart)} đến {DataKeyText.FormatHM(examEnd)}.");
            DBG("UI_TakeExam: Too early for exam window.");
            return;
        }
        if (now >= examEnd)
        {
            MarkExamMissed(subj);
            UI?.OpenDialogue(TitleText(),
                "Bạn đã quá giờ điểm danh vào thi, không thể vào để thi được nữa.");
            DBG("UI_TakeExam: Too late -> marked missed.");
            return;
        }

        // Trong cửa sổ 07:05–07:15 → cho vào thi
        DBG("UI_TakeExam: Inside exam window -> ProceedEnterExam.");
        ProceedEnterExam(subj);
    }

    private void AnnounceExamEnded(Weekday day, int slot1Based)
    {
        int start = DataKeyText.TryGetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slot1Based));
        int end = start + 15;
        string dayVN = DataKeyText.VN_Weekday(day);
        UI?.OpenDialogue(TitleText(),
            $"Kì thi cuối kì môn vào {dayVN} từ {DataKeyText.FormatHM(start + 5)} đến {DataKeyText.FormatHM(end)} đã kết thúc, em không thể vào thi được nữa");
    }

    private void ProceedEnterExam(SubjectEntry subj)
    {
        UI?.OpenDialogue(TitleText(), $"Em đủ điều kiện để thi môn '{subj.subjectName}'. Chúc may mắn!");
        
        // **THÊM: Lưu trạng thái game trước khi vào thi**
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
}
