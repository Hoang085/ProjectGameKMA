using UnityEngine;

/// Quan ly diem danh va nghi hoc theo cac ky
public class AttendanceManager : MonoBehaviour
{
    public static AttendanceManager Instance { get; private set; } // Singleton instance

    [Header("Refs")]
    public SemesterConfig[] semesterConfigs; // Mang cac ky
    public GameClock clock;

    private SubjectData _currentSubject; // Mon hoc dang dien ra
    private bool _attendedThisSlot; // Trang thai diem danh trong ca

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!clock) clock = FindFirstObjectByType<GameClock>();
    }

    // Lang nghe su kien bat dau/ket thuc ca hoc
    void OnEnable()
    {
        if (clock != null)
        {
            clock.OnSlotStarted += HandleSlotStarted;
            clock.OnSlotEnded += HandleSlotEnded;
            HandleSlotStarted(clock.Week, null, clock.GetSlotIndex1Based());
            Debug.Log("[Attendance] OnEnable");
        }
    }

    // Huy lang nghe su kien
    void OnDisable()
    {
        if (clock != null)
        {
            clock.OnSlotStarted -= HandleSlotStarted;
            clock.OnSlotEnded -= HandleSlotEnded;
        }
        Debug.Log("[Attendance] OnDisable");
    }

    // Lay ky hoc hien tai
    SemesterConfig GetCurrentSemester()
    {
        if (semesterConfigs == null || semesterConfigs.Length == 0) return null;
        int idx = Mathf.Clamp(clock.Term - 1, 0, semesterConfigs.Length - 1);
        return semesterConfigs[idx];
    }

    // Xu ly khi ca hoc bat dau
    private void HandleSlotStarted(int week, string _unused, int slot)
    {
        var sem = GetCurrentSemester();
        _currentSubject = SemesterConfigUtil.GetSubjectAt(sem, clock.Weekday, slot); // Lay mon hoc theo ca
        _attendedThisSlot = false;
        Debug.Log($"[Attendance] SlotStarted → week={week}, day='{clock.Weekday}', slot={slot}, found='{_currentSubject?.Name ?? "null"}'");
    }

    // Xu ly khi ca hoc ket thuc
    private void HandleSlotEnded(int week, string dayName, int slot)
    {
        if (_currentSubject != null && !_attendedThisSlot)
        {
            IncrementAbsence(_currentSubject.Name, clock.Term); // Tang so buoi nghi
        }
        _currentSubject = null;
        _attendedThisSlot = false;
    }

    // Kiem tra diem danh
    public bool TryCheckIn(string subjectName, out string error)
    {
        error = null;
        var sem = GetCurrentSemester();
        var sub = FindSubject(sem, subjectName);
        if (sub == null)
        {
            error = $"Môn '{subjectName}' không có trong SemesterConfig hiện tại.";
            return false;
        }

        if (HasExceededAbsences(subjectName, clock.Term))
        {
            error = "Em đã nghỉ quá số buổi quy định cho phép";
            return false;
        }

        if (_currentSubject == null)
        {
            error = "Ca hiện tại chưa được khởi tạo. (Hãy Force Refresh hoặc chuyển ca 1 lần.)";
            Debug.LogWarning($"[Attendance] _currentSubject=null. Day='{clock.CurrentDayNameEN()}', Slot={clock.GetSlotIndex1Based()}");
            return false;
        }
        if (!SameSubject(subjectName, _currentSubject.Name))
        {
            error = $"Không đúng ca học của môn này (đang là: '{_currentSubject.Name}').";
            Debug.LogWarning($"[Attendance] Subject mismatch. Want='{subjectName}', Current='{_currentSubject.Name}'");
            return false;
        }

        _attendedThisSlot = true; // Danh dau da diem danh
        return true;
    }

    // Kiem tra vuot qua so buoi nghi cho phep
    public bool HasExceededAbsences(string subjectName, int term)
    {
        var sem = GetCurrentSemester();
        var sub = FindSubject(sem, subjectName);
        if (sub == null) return false;
        if (sub.MaxAbsences <= 0) return false; // Khong gioi han nghi
        int abs = GetAbsences(subjectName, term);
        return abs >= sub.MaxAbsences;
    }

    // Lay so buoi nghi
    public int GetAbsences(string subjectName, int term)
    {
        return PlayerPrefs.GetInt(AbsKey(subjectName, term), 0);
    }

    // Xoa so buoi nghi
    public void ResetAbsences(string subjectName, int term)
    {
        PlayerPrefs.DeleteKey(AbsKey(subjectName, term));
    }

    // Tang so buoi nghi
    private void IncrementAbsence(string subjectName, int term)
    {
        string k = AbsKey(subjectName, term);
        int v = PlayerPrefs.GetInt(k, 0) + 1;
        PlayerPrefs.SetInt(k, v);
        PlayerPrefs.Save();
    }

    // Tao key luu tru so buoi nghi
    private string AbsKey(string subjectName, int term)
    {
        return $"abs_T{term}_{Normalize(subjectName)}";
    }

    // Tim mon hoc trong ky
    private SubjectData FindSubject(SemesterConfig sem, string subjectName)
    {
        if (sem == null || sem.Subjects == null) return null;
        string target = Normalize(subjectName);
        foreach (var s in sem.Subjects)
            if (Normalize(s?.Name) == target) return s;
        return null;
    }

    // So sanh ten mon hoc
    private static bool SameSubject(string a, string b) => Normalize(a) == Normalize(b);

    // Chuan hoa ten mon hoc
    private static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();
}