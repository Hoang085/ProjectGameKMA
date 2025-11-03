using UnityEngine;

[DisallowMultipleComponent]
public class AttendanceManager : MonoBehaviour
{
    public static AttendanceManager Instance { get; private set; }

    [Header("Configs")]
    public SemesterConfig[] semesterConfigs;      // Các kỳ học (theo Term tuyệt đối)
    public SubjectAttendanceConfig slotPolicy;    // Chính sách theo ca (windowStart, windowEnd)
    public GameClock clock;                       // BẮT BUỘC: chủ sở hữu dữ liệu thời gian

    private SubjectData _currentSubject;
    private bool _attendedThisSlot;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!clock) clock = FindFirstObjectByType<GameClock>();
        if (!clock)
        {
            Debug.LogError("[AttendanceManager] GameClock is required but not found!");
            enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        clock.OnSlotStarted += HandleSlotStarted;
        clock.OnSlotEnded += HandleSlotEnded;

        // Khởi tạo trạng thái theo ca hiện tại
        HandleSlotStarted(clock.Week, null, clock.SlotIndex1Based);
    }

    void OnDisable()
    {
        if (!clock) return;
        clock.OnSlotStarted -= HandleSlotStarted;
        clock.OnSlotEnded -= HandleSlotEnded;
    }

    // === Sự kiện Slot ===
    private void HandleSlotStarted(int week, string _, int slotIndex1Based)
    {
        var sem = GetCurrentSemester();
        _currentSubject = SemesterConfigUtil.instance.GetSubjectAt(sem, clock.Weekday, slotIndex1Based);
        _attendedThisSlot = false;
    }

    private void HandleSlotEnded(int week, string dayName, int slotIndex1Based)
    {
        // Kết thúc ca mà chưa điểm danh -> tính vắng
        if (_currentSubject != null && !_attendedThisSlot)
            IncrementAbsence(_currentSubject.Name, clock.Term);

        _currentSubject = null;
        _attendedThisSlot = false;
    }

    // Giữ hàm cũ để không phá code hiện tại
    public bool TryCheckIn(string subjectName, out string error) =>
        TryCheckIn(subjectName, out error, out _);

    // Hàm mới: có cờ isLate (chưa dùng late window)
    public bool TryCheckIn(string subjectName, out string error, out bool isLate)
    {
        isLate = false;
        error = null;

        var sem = GetCurrentSemester();
        var sub = FindSubject(sem, subjectName);
        if (sub == null) { error = DataKeyText.text4 + subjectName; return false; }

        int term = clock.Term;
        if (HasExceededAbsences(subjectName, term))
        {
            error = DataKeyText.text5; // quá số buổi vắng
            return false;
        }

        if (_currentSubject == null)
        {
            error = DataKeyText.text6; // Không đúng môn ở ca này
            return false;
        }
        if (!SameSubject(subjectName, _currentSubject.Name))
        {
            error = DataKeyText.text7 + _currentSubject.Name; // Đang là môn khác
            return false;
        }

        var slot = clock.Slot;
        if (slot == DaySlot.Evening) { error = "Ca tối không cho điểm danh."; return false; }
        if (slotPolicy == null) { error = "Chưa cấu hình SubjectAttendanceConfig."; return false; }

        int slotStart = GetSlotStart(slot);
        if (!slotPolicy.TryGetWindow(slot, slotStart, out int windowStart, out int windowEnd))
        {
            error = DataKeyText.text8; // Không có khung giờ cho ca này
            return false;
        }

        int now = clock.MinuteOfDay;

        if (now < windowStart)
        {
            error = $"Chưa đến giờ học! Em hãy đến điểm danh từ {windowStart / 60:00}:{windowStart % 60:00} đến {windowEnd / 60:00}:{windowEnd % 60:00} nhé.";
            return false;
        }

        if (now < windowEnd)
        {
            _attendedThisSlot = true;   // Đã điểm danh trong ca -> không tính vắng ở HandleSlotEnded
            isLate = false;
            return true;
        }

        error = DataKeyText.text9; // Hết giờ điểm danh
        return false;
    }

    // === API cho TaskManager: kiểm tra có thể check-in NGAY BÂY GIỜ ===
    public bool CanCheckInNow(string subjectName, out int windowStart, out int windowEnd)
    {
        windowStart = windowEnd = 0;

        // Không cho điểm danh ca tối hoặc thiếu cấu hình
        if (clock.Slot == DaySlot.Evening || slotPolicy == null) return false;

        // Lấy mốc ca theo GameClock
        int slotStart = GetSlotStart(clock.Slot);
        if (!slotPolicy.TryGetWindow(clock.Slot, slotStart, out windowStart, out windowEnd))
            return false;

        int now = clock.MinuteOfDay;
        return now >= windowStart && now < windowEnd;
    }

    // === Khung giờ theo GameClock ===
    public int GetSlotStart(DaySlot slot) => slot switch
    {
        DaySlot.MorningA => clock.tSession1,
        DaySlot.MorningB => clock.tSession2,
        DaySlot.AfternoonA => clock.tSession3,
        DaySlot.AfternoonB => clock.tSession4,
        DaySlot.Evening => clock.tSession5,
        _ => clock.tSession1
    };

    public int GetNextSlotStart(DaySlot slot) => slot switch
    {
        DaySlot.MorningA => clock.tSession2,
        DaySlot.MorningB => clock.tSession3,
        DaySlot.AfternoonA => clock.tSession4,
        DaySlot.AfternoonB => clock.tSession5,
        DaySlot.Evening => 24 * 60, // cuối ngày; không cho check-in ca tối
        _ => 24 * 60
    };

    // === Absence tracking ===
    SemesterConfig GetCurrentSemester()
    {
        if (semesterConfigs == null || semesterConfigs.Length == 0) return null;
        int idx = Mathf.Clamp(clock.Term - 1, 0, semesterConfigs.Length - 1);
        return semesterConfigs[idx];
    }

    public bool HasExceededAbsences(string subjectName, int term)
    {
        var sem = GetCurrentSemester();
        var sub = FindSubject(sem, subjectName);
        if (sub == null || sub.MaxAbsences <= 0) return false;
        return GetAbsences(subjectName, term) > sub.MaxAbsences;
    }

    public int GetAbsences(string subjectName, int term) =>
        PlayerPrefs.GetInt(AbsKey(subjectName, term), 0);

    private void IncrementAbsence(string subjectName, int term)
    {
        string k = AbsKey(subjectName, term);
        int v = PlayerPrefs.GetInt(k, 0) + 1;
        PlayerPrefs.SetInt(k, v);
        PlayerPrefs.Save();
    }

    private string AbsKey(string subjectName, int term) =>
        $"abs_T{term}_{Normalize(subjectName)}";

    // === Helpers ===
    private SubjectData FindSubject(SemesterConfig sem, string subjectName)
    {
        if (sem?.Subjects == null) return null;
        string target = Normalize(subjectName);
        foreach (var s in sem.Subjects)
            if (Normalize(s?.Name) == target) return s;
        return null;
    }

    private static bool SameSubject(string a, string b) =>
        Normalize(a) == Normalize(b);

    private static string Normalize(string s) =>
        (s ?? "").Trim().ToLowerInvariant();
}
