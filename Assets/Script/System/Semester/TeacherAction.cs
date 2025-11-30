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
        
        // **MỚI: Đăng ký event OnSlotChanged để check lịch thi mỗi khi đổi ca**
        if (GameClock.Ins != null)
            GameClock.Ins.OnSlotChanged += HandleSlotChanged;
        
        // **QUAN TRỌNG: Đảm bảo semesterConfig được load cho đúng kỳ hiện tại**
        int currentTerm = GetCurrentTerm();
        if (semesterConfig == null || semesterConfig.Semester != currentTerm)
        {
            Debug.LogWarning($"[TeacherAction][{name}] OnEnable: semesterConfig null hoặc sai kỳ, tìm config cho kỳ {currentTerm}...");
            var cfg = FindConfigForTerm(currentTerm);
            if (cfg != null)
            {
                semesterConfig = cfg;
                Debug.Log($"[TeacherAction][{name}] OnEnable: Loaded semesterConfig cho kỳ {currentTerm}");
            }
        }
        
        // **QUAN TRỌNG: Load progress cho tất cả subjects TRƯỚC khi check exam schedules**
        if (subjects != null && subjects.Count > 0)
        {
            Debug.Log($"[TeacherAction][{name}] OnEnable: Loading progress for {subjects.Count} subjects...");
            foreach (var subj in subjects)
            {
                if (subj != null && !string.IsNullOrWhiteSpace(subj.subjectName))
                {
                    subj.currentSessionIndex = LoadProgress(subj);
                    Debug.Log($"[TeacherAction][{name}] Loaded {subj.subjectName}: {subj.currentSessionIndex}");
                }
            }
        }
        
        // **MỚI: Kiểm tra và tạo lịch thi ngay khi TeacherAction được enable**
        if (autoCreateExamSchedule && subjects != null && subjects.Count > 0)
        {
            Debug.Log($"[TeacherAction][{name}] OnEnable: Checking for missing exam schedules...");
            foreach (var subj in subjects)
            {
                if (subj != null && !string.IsNullOrWhiteSpace(subj.subjectName))
                {
                    CheckAndCreateExamScheduleIfFinished(subj);
                    CheckAndCreateRetakeSchedule(subj);
                }
            }
        }
    }

    private void OnDisable()
    {
        if (GameClock.Ins != null)
        {
            GameClock.Ins.OnTermChanged -= HandleTermChanged;
            // **MỚI: Hủy đăng ký OnSlotChanged**
            GameClock.Ins.OnSlotChanged -= HandleSlotChanged;
        }
        
        // **THÊM: Lưu progress khi disable để đảm bảo không mất dữ liệu**
        if (subjects != null && subjects.Count > 0)
        {
            Debug.Log($"[TeacherAction] OnDisable: Saving progress for {subjects.Count} subjects...");
            for (int i = 0; i < subjects.Count; i++)
            {
                if (subjects[i] != null)
                {
                    SaveProgress(subjects[i]);
                }
            }
        }
    }

    /// <summary>
    /// **MỚI: Xử lý mỗi khi slot thay đổi (mỗi ca học)**
    /// Kiểm tra và tạo lịch thi cho các môn đã hoàn thành
    /// </summary>
    private void HandleSlotChanged()
    {
        if (!autoCreateExamSchedule || subjects == null || subjects.Count == 0) return;

        if (GetTeacherSemester() != Clock.Term) return;

        foreach (var subj in subjects)
        {
            if (subj != null && !string.IsNullOrWhiteSpace(subj.subjectName))
            {
                // Load progress trước khi check
                subj.currentSessionIndex = LoadProgress(subj);

                // Kiểm tra và tạo lịch thi nếu đã hoàn thành (dù attended hay absence)
                CheckAndCreateExamScheduleIfFinished(subj);

                // Check thi lại
                CheckAndCreateRetakeSchedule(subj);
            }
        }
    }

    private void Awake()
    {
        for (int i = 0; i < subjects.Count; i++)
            subjects[i].currentSessionIndex = LoadProgress(subjects[i]);

        if (autoCreateExamSchedule)
            StartCoroutine(CreateMissingExamSchedulesDelayed());
    }
    
    /// <summary>
    /// **MỚI: Kiểm tra và xử lý chuyển ca sau khi thi lại xong**
    /// </summary>
    private void Start()
    {
        // Kiểm tra xem có flag "vừa thi lại xong" không
        if (PlayerPrefs.GetInt("JUST_FINISHED_RETAKE_EXAM", 0) == 1)
        {
            // Xóa flag
            PlayerPrefs.DeleteKey("JUST_FINISHED_RETAKE_EXAM");
            PlayerPrefs.Save();
            
            Debug.Log("[TeacherAction] ✓ Phát hiện vừa thi lại xong - Tự động chuyển ca");
            
            // Đợi 1 frame để scene load xong hoàn toàn
            StartCoroutine(AutoAdvanceSessionAfterRetake());
        }
    }
    
    /// <summary>
    /// **MỚI: Tự động chuyển ca sau khi thi lại xong**
    /// </summary>
    private IEnumerator AutoAdvanceSessionAfterRetake()
    {
        // Đợi 1 frame
        yield return null;
        
        // Kiểm tra GameClock
        if (Clock != null)
        {
            Debug.Log("[TeacherAction] Chuyển sang ca tiếp theo sau khi thi lại");
            Clock.JumpToNextSessionStart();
            Debug.Log($"[TeacherAction] Đã chuyển sang ca mới: {Clock.Slot}");
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

    private void HandleTermChanged()
    {
        int newTerm = GetCurrentTerm();
        var cfg = FindConfigForTerm(newTerm);
        if (cfg == null) return;

        semesterConfig = cfg;
        RebuildSubjectsFromConfig(cfg, true);

        // UI_Close();  <-- Nguyên nhân khiến bảng điểm bị tắt đột ngột

        Debug.Log($"[TeacherAction] Đã cập nhật dữ liệu sang Kỳ {newTerm} (Không đóng UI)");
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
    
    /// <summary>
    /// **CẢI THIỆN: Sử dụng KeyUtil.MakeKey() thay vì MakeNoteKey() cũ**
    /// KeyUtil.MakeKey() xử lý đúng dấu tiếng Việt, đảm bảo khớp với tên thư mục Quiz
    /// </summary>
    private static string MakeNoteKey(string name)
    {
        return KeyUtil.MakeKey(name);
    }

    private void RebuildSubjectsFromConfig(SemesterConfig cfg, bool saveOldProgress)
    {
        Dictionary<string, int> preservedExamIndices = new Dictionary<string, int>();

        if (subjects != null)
        {
            foreach (var existingSubj in subjects)
            {
                if (existingSubj != null && !string.IsNullOrEmpty(existingSubj.subjectName))
                {
                    if (existingSubj.examIndexInLoader != -1)
                    {
                        string key = NormalizeKey(existingSubj.subjectName);
                        if (!preservedExamIndices.ContainsKey(key))
                        {
                            preservedExamIndices.Add(key, existingSubj.examIndexInLoader);
                        }
                    }
                }
            }
        }
        if (saveOldProgress && subjects != null && subjects.Count > 0)
        {
            for (int i = 0; i < subjects.Count; i++)
            {
                if (subjects[i] != null) SaveProgress(subjects[i]);
            }
        }

        var newList = new List<SubjectEntry>();
        if (cfg != null && cfg.Subjects != null)
        {
            int weeks = Mathf.Max(1, cfg.Weeks);
            foreach (var s in cfg.Subjects)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.Name)) continue;

                int perWeek = (s.Sessions != null) ? s.Sessions.Length : 0;
                int maxSessions = Mathf.Max(1, weeks * Mathf.Max(1, perWeek));

                string checkKey = NormalizeKey(s.Name);
                int savedIndex = -1;

                if (preservedExamIndices.ContainsKey(checkKey))
                {
                    savedIndex = preservedExamIndices[checkKey];
                }
                else
                {
                    return;
                }

                newList.Add(new SubjectEntry
                {
                    subjectName = s.Name,
                    subjectKeyForNotes = MakeNoteKey(s.Name),
                    maxSessions = maxSessions,
                    examIndexInLoader = savedIndex
                });
            }
        }
        subjects = newList;
        for (int i = 0; i < subjects.Count; i++)
        {
            subjects[i].currentSessionIndex = LoadProgress(subjects[i]);
        }
        Debug.Log($"[TeacherAction] Rebuild thành công {subjects.Count} môn học với đầy đủ Index.");
    }



    private int GetTeacherSemester()
    {
        return Clock ? Mathf.Max(1, Clock.Term) : 1;
    }

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
        int best = 0;

        string kNew = NewProgressKey(s);
        if (PlayerPrefs.HasKey(kNew))
        {
            int vNew = PlayerPrefs.GetInt(kNew, 0);
            if (vNew > best) best = vNew;
        }

        string kLegacy1 = LegacyProgressKey_Term_Name(s);
        if (PlayerPrefs.HasKey(kLegacy1))
        {
            int v = PlayerPrefs.GetInt(kLegacy1, 0);
            if (v > best) best = v;
        }

        string kLegacy2 = LegacyProgressKey_NoTerm_Name(s);
        if (PlayerPrefs.HasKey(kLegacy2))
        {
            int v = PlayerPrefs.GetInt(kLegacy2, 0);
            if (v > best) best = v;
        }

        // Đồng bộ lại về key mới nếu cần
        PlayerPrefs.SetInt(kNew, best);
        PlayerPrefs.Save();

        Debug.Log($"[TeacherAction] LoadProgress {s.subjectName} => {best}");
        return best;
    }


    private void SaveProgress(SubjectEntry s)
    {
        if (s == null) return;

        // Key chính đang dùng
        string kNew = NewProgressKey(s);

        // Đọc giá trị cũ (nếu có) để không bao giờ giảm progress
        int oldMain = PlayerPrefs.GetInt(kNew, 0);
        int newValue = Mathf.Max(oldMain, s.currentSessionIndex);

        // Ghi lại theo giá trị lớn nhất
        PlayerPrefs.SetInt(kNew, newValue);

        // Ghi luôn sang 2 key legacy để ScheduleUI hay hệ thống cũ đọc vẫn đúng
        string legacy1 = LegacyProgressKey_Term_Name(s);
        string legacy2 = LegacyProgressKey_NoTerm_Name(s);

        int oldLegacy1 = PlayerPrefs.GetInt(legacy1, 0);
        int oldLegacy2 = PlayerPrefs.GetInt(legacy2, 0);

        int finalLegacy1 = Mathf.Max(oldLegacy1, newValue);
        int finalLegacy2 = Mathf.Max(oldLegacy2, newValue);

        PlayerPrefs.SetInt(legacy1, finalLegacy1);
        PlayerPrefs.SetInt(legacy2, finalLegacy2);

        PlayerPrefs.Save();

        Debug.Log($"[TeacherAction] SaveProgress '{s.subjectName}': " +
                  $"main={newValue}, legacy1={finalLegacy1}, legacy2={finalLegacy2}");
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
        PlayerPrefs.DeleteKey(p + "_taken");
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

        // **SỬA: Check TẤT CẢ môn, vì có thể player chưa vào ca nào cả**
        if (subjects == null || subjects.Count == 0)
        {
            Debug.Log("[TeacherAction] No subjects to check for exam schedules");
            yield break;
        }

        foreach (var subj in subjects)
        {
            if (subj != null && !string.IsNullOrWhiteSpace(subj.subjectName))
            {
                // **Load progress từ PlayerPrefs trước khi check**
                subj.currentSessionIndex = LoadProgress(subj);
                
                // **CẢI THIỆN: Kiểm tra và tạo lịch thi cho MỌI môn đã hoàn thành**
                CheckAndCreateExamScheduleIfFinished(subj);
                
                // **MỚI: Kiểm tra xem có cần tạo lịch thi lại không**
                CheckAndCreateRetakeSchedule(subj);
            }
        }
        
        Debug.Log($"[TeacherAction] ✓ CreateMissingExamSchedulesDelayed completed for {subjects.Count} subjects");
    }


    /// <summary>
    /// [UPDATED] Hàm tạo lịch thi - KHÔNG TẠO nếu đã bị cấm thi do nghỉ quá số buổi
    /// **MỚI: Ghi điểm 0 vào ExamResults.json khi bị cấm thi**
    /// </summary>
    public void CheckAndCreateExamScheduleIfFinished(SubjectEntry subj)
    {
        if (!autoCreateExamSchedule) return;

        // **KIỂM TRA 1: Nghỉ quá số buổi -> GHI ĐIỂM 0 VÀO FILE**
        var att = AttendanceManager.Instance;
        if (att != null && att.HasExceededAbsences(subj.subjectName, GetTeacherSemester()))
        {
            Debug.Log($"[TeacherAction] ✗ {subj.subjectName} đã bị cấm thi do nghỉ quá số buổi - GHI ĐIỂM 0");
            
            // **MỚI: Gọi RecordBannedExam() để ghi điểm 0 vào ExamResults.json**
            string subjectKey = GetStableSubjectKey(subj);
            int semester = GetTeacherSemester();
            
            // Kiểm tra xem đã ghi chưa (tránh trùng)
            if (!ExamResultStorageFile.IsSubjectBanned(subjectKey, semester))
            {
                ExamResultStorageFile.RecordBannedExam(subjectKey, subj.subjectName, semester);
                Debug.Log($"[TeacherAction] ✓ Đã ghi nhận môn '{subj.subjectName}' BỊ CẤM THI vào ExamResults.json");
            }
            else
            {
                Debug.Log($"[TeacherAction] ℹ Môn '{subj.subjectName}' đã có bản ghi cấm thi rồi");
            }
            
            return; // Không tạo lịch thi
        }

        // **KIỂM TRA 2: Môn chưa hoàn thành**
        if (!IsCourseFinished(subj))
        {
            return;
        }

        // **KIỂM TRA 3: Đã có lịch thi rồi**
        if (TryLoadExamAssignment(subj, out _, out _, out _, out _, out _, out _))
        {
            Debug.Log($"[TeacherAction] ✓ Lịch thi đã tồn tại cho {subj.subjectName}, bỏ qua");
            return;
        }

        // **TẠO LỊCH THI: Tìm ca tiếp theo**
        if (TryGetNextScheduledSlot(subj, out var examTerm, out var examWeek, out var examDay, out var examSlot))
        {
            SaveExamAssignment(subj, examTerm, examWeek, examDay, examSlot);

            string dayVN = DataKeyText.VN_Weekday(examDay);
            Debug.Log($"[TeacherAction] ✓ Đã tạo lịch thi: {subj.subjectName} -> Thi vào {dayVN} Ca {examSlot} Tuần {examWeek}");
            
            // Refresh ScheduleUI nếu đang mở
            var schedUI = FindFirstObjectByType<ScheduleUI>(FindObjectsInactive.Include);
            if (schedUI != null && schedUI.gameObject.activeInHierarchy)
            {
                schedUI.RefreshExamScheduleImmediately();
            }
        }
        else
        {
            // Fallback: Tìm ca gần nhất tuần sau
            if (TryGetNearestExamSlotNextWeek(subj, out var eTerm, out var eWeek, out var eDay, out var eSlot))
            {
                SaveExamAssignment(subj, eTerm, eWeek, eDay, eSlot);
                Debug.Log($"[TeacherAction] ✓ Tạo lịch thi Fallback: {subj.subjectName} -> {eDay} Ca {eSlot}");
            }
            else
            {
                Debug.LogWarning($"[TeacherAction] ✗ Không tìm được ca thi cho {subj.subjectName}");
            }
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
    /// **TỐI ƯU: Kiểm tra và tạo lịch thi lại nếu cần - GỘP LOGIC**
    /// </summary>
    private void CheckAndCreateRetakeSchedule(SubjectEntry subj)
    {
        string subjectKey = GetStableSubjectKey(subj);
        if (PlayerPrefs.GetInt($"NEEDS_RETAKE_SCHEDULE_{subjectKey}", 0) != 1)
        {
            return;
        }
        
        Debug.Log($"[TeacherAction] ✓ Phát hiện flag NEEDS_RETAKE_SCHEDULE cho {subj.subjectName}");
        
        if (TryLoadRetakeExamAssignment(subj, out _, out _, out _, out _, out _, out _))
        {
            Debug.Log($"[TeacherAction] ✓ Đã có lịch thi lại cho {subj.subjectName}, bỏ qua");
            return;
        }
        
        if (!TryLoadExamAssignment(subj, out int examTerm, out int examWeek, out Weekday examDay, out int examSlot, out _, out _))
        {
            return;
        }
        
        Debug.Log($"[TeacherAction] Lịch thi đầu: Term={examTerm}, Week={examWeek}, Day={examDay}, Slot={examSlot}");
        
        if (!TryGetNearestSessionAfter(subj, examTerm, examWeek, examDay, examSlot,
            out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot))
        {
            return;
        }

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
        
        Debug.Log($"[TeacherAction] ✓ ĐÃ TẠO LỊCH THI LẠI cho {subj.subjectName}:");
        Debug.Log($"[TeacherAction]   → {dayVN} ca {retakeSlot} ({timeStr}) - Tuần {retakeWeek}, Kỳ {retakeTerm}");
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
        int actualMax = Mathf.Max(1, subj.maxSessions);
        int requiredSessions = GetLearningSessionThreshold(actualMax);

        return (attended + abs) >= requiredSessions;
    }

    /// <summary>
    /// **MỚI: Lấy số buổi học tối đa để hiển thị (12 buổi -> 10, 18 buổi -> 15)**
    /// </summary>
    private int GetDisplayMaxSessions(int actualMaxSessions)
    {
        // Môn 12 buổi -> hiển thị /10
        if (actualMaxSessions == 12)
        {
            return 10;
        }
        // Môn 18 buổi -> hiển thị /15
        else if (actualMaxSessions == 18)
        {
            return 15;
        }
        // Các trường hợp khác giữ nguyên
        return actualMaxSessions;
    }

    private bool TryFindSubjectForNow(out SubjectEntry subj)
    {
        subj = null;
        if (!Clock || !semesterConfig || subjects == null || subjects.Count == 0)
        {
            return false;
        }

        if (!IsSubjectActiveNow())
        {
            Debug.LogWarning($"[TeacherAction] Subject not active now - Teacher term: {GetTeacherSemester()}, Current term: {GetCurrentTerm()}");
            return false;
        }

        var today = Clock.Weekday;
        var slot1Based = Clock.SlotIndex1Based;

        Debug.Log($"[TeacherAction] ======== TryFindSubjectForNow ========");
        Debug.Log($"[TeacherAction] Current time: {today} - Ca {slot1Based}");
        Debug.Log($"[TeacherAction] Checking {subjects.Count} subjects...");

        // **FIX: Auto-load progress cho subjects có currentSessionIndex = 0**
        // Sau khi ClearAllPlayerPrefs, currentSessionIndex về 0, cần load lại từ PlayerPrefs (hoặc set về 0)
        foreach (var s in subjects)
        {
            if (s != null && !string.IsNullOrWhiteSpace(s.subjectName))
            {
                if (s.currentSessionIndex == 0)
                {
                    s.currentSessionIndex = LoadProgress(s);
                    Debug.Log($"[TeacherAction] Force loaded progress for {s.subjectName}: {s.currentSessionIndex}");
                }
            }
        }

        foreach (var s in subjects)
        {
            if (string.IsNullOrWhiteSpace(s.subjectName))
            {
                Debug.LogWarning($"[TeacherAction] Skipping subject with empty name");
                continue;
            }
            
            Debug.Log($"[TeacherAction] Checking subject: {s.subjectName}");
            bool isMatch = ScheduleResolver.IsSessionMatch(semesterConfig, s.subjectName, today, slot1Based);
            Debug.Log($"[TeacherAction]   → IsSessionMatch: {isMatch}");
            
            if (isMatch)
            {
                subj = s;
                Debug.Log($"[TeacherAction] ✓ FOUND MATCH: {s.subjectName}");
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
    /// **SỬA: Cập nhật trạng thái hiển thị các nút thi - CHỈ HIỂN NÚT THI LẠI KHI ĐÚNG CA THI LẠI**
    /// </summary>
    private void UpdateExamButtonsState()
    {
        if (!TryFindSubjectForNow(out var subj))
        {
            UI?.UpdateExamButtonsVisibility(showRetake: false);
            return;
        }
        
        subj.currentSessionIndex = LoadProgress(subj);
        
        if (!IsCourseFinished(subj))
        {
            UI?.UpdateExamButtonsVisibility(showRetake: false);
            return;
        }
        
        if (TryLoadExamAssignment(subj, out _, out _, out _, out _, out _, out bool examTaken))
        {
            if (!examTaken)
            {
                Debug.Log($"[TeacherAction] ✗ Chưa thi lần đầu - Ẩn nút 'Thi lại' cho {subj.subjectName}");
                UI?.UpdateExamButtonsVisibility(showRetake: false);
                return;
            }
        }
        
        bool showRetakeButton = false;
        
        // **QUAN TRỌNG: CHỈ HIỆN NÚT KHI ĐÚNG CA THI LẠI**
        if (TryLoadRetakeExamAssignment(subj, 
            out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot, 
            out bool retakeMissed, out bool retakeTaken))
        {
            // Kiểm tra đã thi/bỏ lỡ chưa
            if (retakeTaken || retakeMissed)
            {
                Debug.Log($"[TeacherAction] ✗ Không hiện nút 'Thi lại' - taken={retakeTaken}, missed={retakeMissed}");
                UI?.UpdateExamButtonsVisibility(showRetake: false);
                return;
            }
            
            // **KIỂM TRA ĐÚNG CA THI LẠI**
            if (Clock != null)
            {
                int currentTerm = GetCurrentTerm();
                int currentWeek = Mathf.Max(1, Clock.Week);
                Weekday currentDay = Clock.Weekday;
                int currentSlot = Clock.SlotIndex1Based;
                
                bool isRetakeTime = (currentTerm == retakeTerm && 
                                    currentWeek == retakeWeek && 
                                    currentDay == retakeDay && 
                                    currentSlot == retakeSlot);
                
                if (isRetakeTime)
                {
                    showRetakeButton = true;
                    Debug.Log($"[TeacherAction] ✓ ĐÚNG CA THI LẠI - Hiện nút 'Thi lại' cho {subj.subjectName}");
                }
                else
                {
                    Debug.Log($"[TeacherAction] ✗ CHƯA ĐẾN CA THI LẠI - Ẩn nút 'Thi lại' cho {subj.subjectName}");
                    Debug.Log($"[TeacherAction]   Lịch thi lại: T{retakeTerm}/W{retakeWeek}/{retakeDay}/Ca{retakeSlot}");
                    Debug.Log($"[TeacherAction]   Hiện tại: T{currentTerm}/W{currentWeek}/{currentDay}/Ca{currentSlot}");
                }
            }
        }
        else
        {
            Debug.Log($"[TeacherAction] ✗ Không tìm thấy lịch thi lại cho {subj.subjectName}");
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

        // **KIỂM TRA ƯU TIÊN 1: Đã thi rồi (lần đầu hoặc thi lại) -> CHẶN HOÀN TOÀN**
        bool hasExamSchedule = TryLoadExamAssignment(subj, out _, out _, out _, out _, out _, out bool examTaken);
        bool hasRetakeSchedule = TryLoadRetakeExamAssignment(subj, out _, out _, out _, out _, out bool retakeMissed, out bool retakeTaken);
        
        // **SỬA: Kiểm tra đã thi (lần đầu hoặc thi lại) TRƯỚC, bất kể ca hiện tại**
        if (hasExamSchedule && examTaken)
        {
            UI?.OpenDialogue(TitleText(), "Em đã hoàn thành kỳ thi môn này. Không thể điểm danh và học thêm nữa!");
            return;
        }
        
        if (hasRetakeSchedule)
        {
            if (retakeTaken)
            {
                UI?.OpenDialogue(TitleText(), "Em đã hoàn thành kỳ thi lại môn này. Không thể điểm danh và học thêm nữa!");
                return;
            }
            
            if (retakeMissed)
            {
                UI?.OpenDialogue(TitleText(), "Em đã bỏ lỡ kỳ thi lại. Không thể điểm danh và học thêm nữa!");
                return;
            }
        }

        // **KIỂM TRA 2: Môn đã hoàn thành**
        if (IsCourseFinished(subj))
        {
            // **Nếu đã có lịch thi lại và đang trong ca thi lại**
            if (hasRetakeSchedule)
            {
                if (TryLoadRetakeExamAssignment(subj, 
                    out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot, 
                    out _, out _))
                {
                    // Kiểm tra xem hiện tại có đúng ca thi lại không
                    if (Clock != null)
                    {
                        int currentTerm = GetCurrentTerm();
                        int currentWeek = Mathf.Max(1, Clock.Week);
                        Weekday currentDay = Clock.Weekday;
                        int currentSlot = Clock.SlotIndex1Based;
                        
                        bool isRetakeTime = (currentTerm == retakeTerm && 
                                            currentWeek == retakeWeek && 
                                            currentDay == retakeDay && 
                                            currentSlot == retakeSlot);
                        
                        if (isRetakeTime)
                        {
                            // **ĐÚNG CA THI LẠI: Chặn không cho học, bắt phải thi lại**
                            UI?.OpenDialogue(TitleText(), "Đây là ca thi lại của em. Vui lòng nhấn nút 'Thi lại' để vào thi!");
                            return;
                        }
                    }
                }
            }
            
            // **MÔN ĐÃ HOÀN THÀNH: Không cho học nữa (bất kể có lịch thi chưa)**
            UI?.OpenDialogue(TitleText(), finishedAllSessionsText);
            return;
        }

        // **KIỂM TRA 3: Nghỉ quá số buổi quy định**
        var att = AttendanceManager.Instance;
        if (att != null && att.HasExceededAbsences(subj.subjectName, GetTeacherSemester()))
        {
            UI?.OpenDialogue(TitleText(), exceededAbsenceText);
            return;
        }

        // **MÔN CHƯA HOÀN THÀNH VÀ CHƯA THI: Cho phép học bình thường**
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

        // =======================================================================
        // PHẦN 1: LUỒNG THẤT BẠI
        // =======================================================================
        if (!quizPassed)
        {
            if (att != null)
            {
                att.CancelAttendanceAndMarkAbsent(subj.subjectName);
            }

            string failMsg = $"Chưa đạt yêu cầu kiểm tra! Em chưa đạt điểm tối thiểu trong bài kiểm tra quá trình học. Buổi học này sẽ được tính là vắng mặt. Tài liệu buổi học đã được thêm vào Túi đồ. Lưu ý: Nếu vắng quá số buổi quy định, em sẽ bị cấm thi!";

            // **SỬA: Đóng dialogue cũ trước khi mở mới**
            UI?.CloseDialogue(unbindTeacher: false);
            yield return new WaitForSecondsRealtime(0.2f);
            
            UI?.OpenDialogue(TitleText(), failMsg);
            yield return new WaitForSecondsRealtime(4.5f);

            // **KIỂM TRA: Môn có hoàn thành không (attended + absence >= threshold)**
            bool isCourseCompleted = IsCourseFinished(subj);

            // **KIỂM TRA EXCEEDED ABSENCES TRƯỚC KHI TẠO LỊCH**
            bool hasExceededAbsences = att != null && att.HasExceededAbsences(subj.subjectName, GetTeacherSemester());

            if (isCourseCompleted)
            {
                // **SỬA: Đóng dialogue trước khi hiển thị thông báo tiếp theo**
                UI?.CloseDialogue(unbindTeacher: false);
                yield return new WaitForSecondsRealtime(0.2f);
                
                if (hasExceededAbsences)
                {
                    // **TRƯỜNG HỢP: Hoàn thành nhưng bị cấm thi - KHÔNG TẠO LỊCH**
                    string bannedMsg = $"Em đã vắng {att.GetAbsences(subj.subjectName, GetTeacherSemester())} buổi học môn {subj.subjectName}. Em đã bị cấm thi môn này do nghỉ quá số buổi quy định hoặc không vượt qua điểm quá trình!";

                    UI?.OpenDialogue(TitleText(), bannedMsg);
                    yield return new WaitForSecondsRealtime(5f);
                }
                else
                {
                    // **TRƯỜNG HỢP: Hoàn thành và chưa bị cấm - TẠO LỊCH THI**
                    CheckAndCreateExamScheduleIfFinished(subj);

                    // Kiểm tra và thông báo nếu tạo lịch thành công
                    if (TryLoadExamAssignment(subj, out int examTerm, out int examWeek, out Weekday examDay, out int examSlot, out _, out _))
                    {
                        string dayVN = DataKeyText.VN_Weekday(examDay);
                        int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(examSlot));
                        string timeStr = DataKeyText.FormatHM(startMin);

                        string examNoticeMsg = $"Lưu ý: Em đã hoàn thành môn {subj.subjectName} (tính cả nghỉ). Lịch thi: {dayVN} - Ca {examSlot} ({timeStr}). Xem chi tiết trong Bảng lịch thi!";

                        UI?.OpenDialogue(TitleText(), examNoticeMsg);
                        yield return new WaitForSecondsRealtime(4.5f);
                        
                        // **THÊM: Refresh ScheduleUI nếu đang mở**
                        var scheduleUI = Object.FindFirstObjectByType<ScheduleUI>();
                        if (scheduleUI != null && scheduleUI.gameObject.activeInHierarchy)
                        {
                            scheduleUI.RefreshExamScheduleImmediately();
                        }
                    }
                }
            }

            UI?.CloseDialogue(unbindTeacher: true);
            yield return new WaitForSecondsRealtime(0.5f);

            Debug.Log($"[TeacherAction] Quiz failed - Chuyển sang ca tiếp theo");
            if (Clock) Clock.JumpToNextSessionStart();

            _state = State.Idle;
            UI?.HideInteractPrompt();
            onClassFinished?.Invoke();
            yield break;
        }

        // =======================================================================
        // PHẦN 2: LUỒNG THÀNH CÔNG (GIỮ NGUYÊN)
        // =======================================================================

        if (att != null)
        {
            att.ConfirmAttendance(subj.subjectName);
        }

        // **SỬA: Đóng dialogue cũ trước khi mở mới**
        UI?.CloseDialogue(unbindTeacher: false);
        yield return new WaitForSecondsRealtime(0.2f);
        
        UI?.OpenDialogue(TitleText(), "Hoàn thành buổi học! Tài liệu đã được thêm vào Túi đồ. Đang xử lý kết quả...");
        yield return new WaitForSecondsRealtime(1.5f);

        int abs = GetAbsencesFor(subj);
        int cap = Mathf.Max(1, subj.maxSessions);
        int attended = Mathf.Max(0, subj.currentSessionIndex);
        int threshold = GetLearningSessionThreshold(cap); 
        int attendedCap = Mathf.Max(0, threshold - abs);

        int displayCap = GetDisplayMaxSessions(cap); 

        int newAttended = attended + 1;
        if (newAttended <= attendedCap)
        {
            subj.currentSessionIndex = newAttended;
            SaveProgress(subj);
            Debug.Log($"[TeacherAction] Tăng progress: {attended} -> {newAttended} / {attendedCap} (threshold={threshold})");

            int verified = LoadProgress(subj);
            if (verified != newAttended) Debug.LogError($"[TeacherAction] SAVE FAILED! Expected: {newAttended}, Got: {verified}");
        }

        bool justFinished = (subj.currentSessionIndex >= attendedCap);

        SaveProgress(subj);
        if (justFinished)
        {
            CheckAndCreateExamScheduleIfFinished(subj);

            string messageTitle = TitleText();
            string finalMessage = "";

            if (TryLoadExamAssignment(subj, out var examTerm, out var examWeek, out var examDayNextWeek, out var slotIdx1Based, out _, out _))
            {
                string dayVN = DataKeyText.VN_Weekday(examDayNextWeek);
                int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slotIdx1Based));
                string timeStr = DataKeyText.FormatHM(startMin);

                finalMessage = $"Chúc mừng! Em đã hoàn thành buổi học môn {subj.subjectName}. ";
                finalMessage += $"Lịch thi: {dayVN} - Ca {slotIdx1Based} ({timeStr}) - Tuần {examWeek}. ";
                finalMessage += "Lưu ý: Em chỉ được phép thi vào đúng ca này!";

                Debug.Log($"[TeacherAction] ✓ Hiển thị thông báo lịch thi: {dayVN} - Ca {slotIdx1Based}");
            }
            else
            {
                finalMessage = "Chúc mừng em đã hoàn thành môn học. Hãy kiểm tra bảng thông báo để xem lịch thi.";
            }
            
            // **SỬA: Đóng dialogue cũ trước khi mở dialogue final**
            UI?.CloseDialogue(unbindTeacher: false);
            yield return new WaitForSecondsRealtime(0.2f);
            
            UI?.OpenDialogue(messageTitle, finalMessage);
            
            var schedUI = Object.FindFirstObjectByType<ScheduleUI>();
            if (schedUI != null && schedUI.gameObject.activeInHierarchy)
            {
                schedUI.RefreshExamScheduleImmediately();
            }
            
            // **SỬA: Đợi người chơi đọc thông báo trước khi đóng**
            yield return new WaitForSecondsRealtime(5f);
            
            Debug.Log($"[TeacherAction] Chuyển sang ca tiếp theo (End Course)");
            if (Clock) Clock.JumpToNextSessionStart();

            _state = State.Idle;
            onClassFinished?.Invoke();
            yield break;
        }

        UI?.CloseDialogue(unbindTeacher: true);
        yield return new WaitForSecondsRealtime(0.5f);

        Debug.Log($"[TeacherAction] Chuyển sang ca tiếp theo");
        if (Clock) Clock.JumpToNextSessionStart();

        _state = State.Idle;
        UI?.HideInteractPrompt();
        onClassFinished?.Invoke();
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

        // **KIỂM TRA 1: Nghỉ quá số buổi quy định - CHẶN NGAY TRƯỚC KHI KIỂM TRA GÌ KHÁC**
        var att = AttendanceManager.Instance;
        if (att != null && att.HasExceededAbsences(subj.subjectName, GetTeacherSemester()))
        {
            UI?.OpenDialogue(TitleText(), "Em đã vắng quá số buổi quy định hoặc không vượt qua điểm quá trình. Em bị cấm thi môn này!");
            return;
        }

        // **KIỂM TRA 2: Chưa hoàn thành đủ số buổi**
        if (!IsCourseFinished(subj))
        {
            UI?.OpenDialogue(TitleText(), "Em chưa hoàn thành đủ số buổi môn này");
            return;
        }

        // **KIỂM TRA 3: Đã thi rồi**
        EnsureExamScheduleExists(subj);
        if (!TryLoadExamAssignment(subj, out int aTerm, out int aWeek, out Weekday aDay, out int aSlot1, out bool missed, out bool taken))
        {
            UI?.OpenDialogue(TitleText(), "Không thể tạo lịch thi cho môn này. Kiểm tra cấu hình SemesterConfig.");
            return;
        }
        if (taken)
        {
            UI?.OpenDialogue(TitleText(), "Em đã hoàn thành kỳ thi này rồi. Không thể thi lại.");
            return;
        }
        if (missed)
        {
            UI?.OpenDialogue(TitleText(), "Em đã bỏ lỡ kỳ thi này. Không thể thi được nữa.");
            return;
        }

        // **KIỂM TRA 4: Đúng giờ thi chưa**
        if (Clock == null)
        {
            UI?.OpenDialogue(TitleText(), "Đồng hồ chưa sẵn sàng.");
            return;
        }

        int nTerm = GetCurrentTerm();
        int nWeek = Mathf.Max(1, Clock.Week);
        Weekday nDay = Clock.Weekday;
        int nSlot1 = Clock.SlotIndex1Based;
        bool correctTime = (nTerm == aTerm && nWeek == aWeek && nDay == aDay && nSlot1 == aSlot1);
        
        if (!correctTime)
        {
            string dayVN = DataKeyText.VN_Weekday(aDay);
            int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(aSlot1));
            string timeStr = DataKeyText.FormatHM(startMin);
            
            UI?.OpenDialogue(TitleText(), 
                $"Chưa đúng giờ thi! Lịch thi: {dayVN} - Ca {aSlot1} ({timeStr})");
            return;
        }

        // **KIỂM TRA 5: Đúng khung giờ điểm danh vào thi**
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
        if (!TryLoadRetakeExamAssignment(subj, 
            out int retakeTerm, out int retakeWeek, out Weekday retakeDay, out int retakeSlot, 
            out bool retakeMissed, out bool retakeTaken))
        {
            UI?.OpenDialogue(TitleText(), "Em chưa có lịch thi lại cho môn này.");
            return;
        }
        if (retakeTaken)
        {
            UI?.OpenDialogue(TitleText(), "Em đã hoàn thành kỳ thi lại này rồi.");
            return;
        }
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
        
        bool correctTime = (currentTerm == retakeTerm && currentWeek == retakeWeek && currentDay == retakeDay && currentSlot == retakeSlot);
        
        if (!correctTime)
        {
            string dayVN = DataKeyText.VN_Weekday(retakeDay);
            int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(retakeSlot));
            string timeStr = DataKeyText.FormatHM(startMin);
            
            UI?.OpenDialogue(TitleText(), 
                $"Chưa đúng giờ thi lại! Lịch thi lại: {dayVN} - Ca {retakeSlot} ({timeStr})");
            return;
        }
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
        UI?.OpenDialogue(TitleText(), $"Em đủ điều kiện để thi môn {subj.subjectName}. Chúc may mắn!");

        GameStateManager.SavePreExamState(subj.subjectName);

        ExamRouteData.Set(subj.subjectName, subj.subjectKeyForNotes);
        PlayerPrefs.DeleteKey("EXAM_IS_RETAKE");
        PlayerPrefs.Save();
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
        PlayerPrefs.SetInt("EXAM_IS_RETAKE", 1);
        PlayerPrefs.SetInt("JUST_FINISHED_RETAKE_EXAM", 1);
        PlayerPrefs.Save();
        MarkRetakeExamTaken(subj);
        Debug.Log("[TeacherAction] ✓ Đã set flag JUST_FINISHED_RETAKE_EXAM - Sẽ tự động chuyển ca khi quay lại");
        StartCoroutine(LoadExamSceneDelayed("ExamScene", 2.5f));
    }

    private IEnumerator LoadExamSceneDelayed(string sceneName, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        SceneLoader.Load(sceneName);
    }

    private int GetLearningSessionThreshold(int actualMaxSessions)
    {
        // Môn 18 buổi -> Học xong buổi 15 là tạo lịch thi
        if (actualMaxSessions >= 18) return 15;

        // --- BỔ SUNG DÒNG NÀY ĐỂ SỬA LỖI ---
        // Nếu môn học được setup MaxSessions = 15 thì phải học hết 15 buổi mới thi
        if (actualMaxSessions == 15) return 15;
        // -----------------------------------

        // Môn 12 buổi -> Học xong buổi 10 là tạo lịch thi
        // Lưu ý: Code cũ của bạn là (actualMaxSessions >= 12), số 15 thỏa mãn điều kiện này nên nó trả về 10 -> Lỗi
        if (actualMaxSessions >= 12) return 10;

        // Các môn ngắn hạn khác -> Học hết mới thi
        return actualMaxSessions;
    }

    // -------------------------------------------------------------------------------
    // Hàm quan trọng: Tìm slot tiếp theo (quét dư ra các tuần sau để tìm lịch thi)
    // -------------------------------------------------------------------------------
    private bool TryGetNextScheduledSlot(SubjectEntry subj, out int term, out int week, out Weekday day, out int slot1Based)
    {
        term = GetTeacherSemester();
        week = 1;
        day = Weekday.Mon;
        slot1Based = 1;

        if (!Clock || semesterConfig == null) return false;

        int curWeek = Mathf.Max(1, Clock.Week);
        int curDayInt = (int)Clock.Weekday;
        int curSlot = Clock.SlotIndex1Based;
        int maxWeeks = GetWeeksPerTerm() + 4;

        for (int w = curWeek; w <= maxWeeks; w++)
        {
            int startDay = (w == curWeek) ? curDayInt : (int)Weekday.Mon;

            for (int d = startDay; d <= (int)Weekday.Sun; d++)
            {
                int startSlot = (w == curWeek && d == curDayInt) ? (curSlot + 1) : 1;

                for (int s = startSlot; s <= 5; s++)
                {
                    if (ScheduleResolver.IsSessionMatch(semesterConfig, subj.subjectName, (Weekday)d, s))
                    {
                        term = GetTeacherSemester();
                        week = w;
                        day = (Weekday)d;
                        slot1Based = s;
                        return true; 
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// MỚI: Tính toán số buổi học ĐÃ TRÔI QUA dựa trên thời gian thực tế của GameClock
    /// Không phụ thuộc vào việc người chơi có điểm danh hay không.
    /// </summary>
    private int CalculatePassedSessionsByTime(SubjectEntry subj, int threshold)
    {
        if (!Clock || semesterConfig == null) return 0;

        int passedCount = 0;

        int currentWeek = Mathf.Max(1, Clock.Week);
        int currentDayInt = (int)Clock.Weekday;
        int currentSlot = Clock.SlotIndex1Based;

        for (int w = 1; w <= currentWeek; w++)
        {
            int endDayLimit = (w < currentWeek) ? (int)Weekday.Sun : currentDayInt;

            for (int d = (int)Weekday.Mon; d <= endDayLimit; d++)
            {
                int endSlotLimit = (w == currentWeek && d == currentDayInt) ? currentSlot : 5;

                for (int s = 1; s <= endSlotLimit; s++)
                {
                    if (ScheduleResolver.IsSessionMatch(semesterConfig, subj.subjectName, (Weekday)d, s))
                    {
                        passedCount++;
                        if (passedCount >= threshold) return passedCount;
                    }
                }
            }
        }

        return passedCount;
    }
    
    /// <summary>
    /// **MỚI: Kiểm tra xem subject có cần force load progress không**
    /// </summary>
    private bool NeedsProgressLoad(SubjectEntry s)
    {
        // Nếu currentSessionIndex > 0 thì đã load rồi
        if (s.currentSessionIndex > 0) return false;
        
        // Kiểm tra xem có key nào trong PlayerPrefs không
        string kNew = NewProgressKey(s);
        if (PlayerPrefs.HasKey(kNew)) return true;
        
        string kLegacy1 = LegacyProgressKey_Term_Name(s);
        if (PlayerPrefs.HasKey(kLegacy1)) return true;
        
        string kLegacy2 = LegacyProgressKey_NoTerm_Name(s);
        if (PlayerPrefs.HasKey(kLegacy2)) return true;
        
        return false;
    }
}
