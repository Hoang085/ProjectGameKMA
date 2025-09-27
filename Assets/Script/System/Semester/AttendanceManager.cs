using UnityEngine;

/// Quản lý điểm danh: vừa check khung giờ, vừa tính vắng học
[DisallowMultipleComponent]
public class AttendanceManager : MonoBehaviour
{
    public static AttendanceManager Instance { get; private set; }

    [Header("Configs")]
    public SemesterConfig[] semesterConfigs;          // Các kỳ học
    public SubjectAttendanceConfig slotPolicy;        // Chính sách theo ca (windowStart, windowEnd)
    public GameClock clock;

    [Header("Slot starts (phút từ 00:00)")]
    public int morningAStart = 7 * 60;
    public int morningBStart = 9 * 60 + 30;
    public int afternoonAStart = 12 * 60 + 30;
    public int afternoonBStart = 15 * 60;
    public int eveningStart = 17 * 60;

    private SubjectData _currentSubject;
    private bool _attendedThisSlot;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!clock) clock = FindFirstObjectByType<GameClock>();
    }

    void OnEnable()
    {
        if (clock != null)
        {
            clock.OnSlotStarted += HandleSlotStarted;
            clock.OnSlotEnded += HandleSlotEnded;
            HandleSlotStarted(clock.Week, null, clock.GetSlotIndex1Based());
        }
    }

    void OnDisable()
    {
        if (clock != null)
        {
            clock.OnSlotStarted -= HandleSlotStarted;
            clock.OnSlotEnded -= HandleSlotEnded;
        }
    }

    // === Sự kiện Slot ===
    private void HandleSlotStarted(int week, string _, int slot)
    {
        var sem = GetCurrentSemester();
        _currentSubject = SemesterConfigUtil.GetSubjectAt(sem, clock.Weekday, slot);
        _attendedThisSlot = false;
    }

    private void HandleSlotEnded(int week, string dayName, int slot)
    {
        if (_currentSubject != null && !_attendedThisSlot)
            IncrementAbsence(_currentSubject.Name, clock.Term);

        _currentSubject = null;
        _attendedThisSlot = false;
    }

    // Giữ hàm cũ để không phá code hiện tại
    public bool TryCheckIn(string subjectName, out string error) =>
        TryCheckIn(subjectName, out error, out _);

    // Hàm mới: có cờ isLate để UI biết và hiển thị thông báo phù hợp
    public bool TryCheckIn(string subjectName, out string error, out bool isLate)
    {
        isLate = false;
        error = null;

        var sem = GetCurrentSemester();
        var sub = FindSubject(sem, subjectName);
        if (sub == null)
        {
            error = DataKeyText.text4 + subjectName ;
            return false;
        }

        if (HasExceededAbsences(subjectName, clock.Term))
        {
            error = DataKeyText.text5;
            return false;
        }

        if (_currentSubject == null)
        {
            error = DataKeyText.text6;
            return false;
        }
        if (!SameSubject(subjectName, _currentSubject.Name))
        {
            error = DataKeyText.text6 + _currentSubject.Name;
            return false;
        }

        // Kiểm tra khung giờ từ slotPolicy
        if (!clock) { error = "Clock chưa sẵn sàng."; return false; }
        var clockUI = FindAnyObjectByType<ClockUI>();
        if (!clockUI) { error = "ClockUI chưa sẵn sàng."; return false; }

        var slot = clock.Slot;
        if (slot == DaySlot.Evening) return false; 
        if (slotPolicy == null) return false; 

        int slotStart = GetSlotStart(slot);
        if (!slotPolicy.TryGetWindow(slot, slotStart, out int windowStart, out int windowEnd))
        {
            error = DataKeyText.text8;
            return false;
        }

        int now = clockUI.GetMinuteOfDay();

        if (now < windowStart)
        {
            error = $"Chưa đến giờ học! Em hãy đến điểm danh từ {windowStart / 60:00}:{windowStart % 60:00} đến {windowEnd / 60:00}:{windowEnd % 60:00} nhé.";
            return false;
        }

        if (now < windowEnd)
        {
            // Đúng giờ trong [windowStart, windowEnd)
            _attendedThisSlot = true;
            isLate = false;
            return true;
        }

        // Hết endOffsetMinutes → không cho điểm danh
        error = DataKeyText.text9;
        return false;
    }


    // === Logic khung giờ từ AttendanceService ===
    public bool CanCheckInNow(string subjectName, out int windowStart, out int windowEnd)
    {
        windowStart = windowEnd = 0;
        if (!clock) return false;

        var clockUI = FindAnyObjectByType<ClockUI>();
        if (!clockUI) return false;

        Weekday day = clock.Weekday;
        DaySlot slot = clock.Slot;
        int now = clockUI.GetMinuteOfDay();

        // Slot tối: không cho check-in
        if (slot == DaySlot.Evening) return false;

        // Lấy mốc ca
        int slotStart = GetSlotStart(slot);
        if (slotPolicy == null) return false;
        if (!slotPolicy.TryGetWindow(slot, slotStart, out windowStart, out windowEnd))
            return false;

        return now >= windowStart && now < windowEnd;
    }

    public int GetSlotStart(DaySlot slot) => slot switch
    {
        DaySlot.MorningA => morningAStart,
        DaySlot.MorningB => morningBStart,
        DaySlot.AfternoonA => afternoonAStart,
        DaySlot.AfternoonB => afternoonBStart,
        DaySlot.Evening => eveningStart,
        _ => morningAStart
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
        return GetAbsences(subjectName, term) >= sub.MaxAbsences;
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
    public int GetNextSlotStart(DaySlot slot) => slot switch
    {
        DaySlot.MorningA => morningBStart,
        DaySlot.MorningB => afternoonAStart,
        DaySlot.AfternoonA => afternoonBStart,
        DaySlot.AfternoonB => eveningStart,
        DaySlot.Evening => 24 * 60, // cuối ngày; ta không cho check-in ca tối
        _ => 24 * 60
    };

}
