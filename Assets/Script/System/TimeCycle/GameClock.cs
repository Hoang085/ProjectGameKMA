using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Quan ly dong thoi gian trong game: Nam, Ky, Tuan, Ngay, Ca
public class GameClock : Singleton<GameClock>
{
    [Header("Config (Assign asset CalendarConfig)")]
    public CalendarConfig config; // Cau hinh lich

    [Header("Current Time (1-based)")]
    [SerializeField] private int _year = 1; // Nam
    [SerializeField] private int _term = 1; // Ky
    [SerializeField] private int _week = 1; // Tuan
    [SerializeField] private int _day = 1; // Ngay (1 = Thu 2)
    [SerializeField] private DaySlot _slot = DaySlot.MorningA; // Ca

    // Getters
    public int Year => _year;
    public int Term => _term;
    public int Week => _week;
    public int DayIndex => _day; // Chi so ngay (1-based)
    public Weekday Weekday => (Weekday)Mathf.Clamp(_day - 1, 0, 6); // Thu trong tuan
    public DaySlot Slot => _slot; // Ca hien tai

    // Cac su kien khi thoi gian thay doi
    public event Action OnSlotChanged, OnDayChanged, OnWeekChanged, OnTermChanged, OnYearChanged;
    public event Action<int, string, int> OnSlotStarted; // Khi ca bat dau
    public event Action<int, string, int> OnSlotEnded; // Khi ca ket thuc

    // Khoi tao singleton va chuan hoa thoi gian
    public override void Awake()
    {
        MakeSingleton(false);
        if (!config) Debug.LogWarning("[GameClock] don't assign CalendarConfig!");
        NormalizeNow(fullClamp: true);
        FireSlotStarted(); // Kich hoat su kien ca bat dau
    }

    // Tang ca, chuyen sang ngay moi neu tran
    [ContextMenu("Next Slot")]
    public void NextSlot()
    {
        int sPerD = config ? Mathf.Clamp(config.slotsPerDay, 1, 5) : 4;

        FireSlotEnded(); // Ket thuc ca hien tai

        int currentSlotIndex = (int)_slot;
        bool willRollDay = (currentSlotIndex + 1) >= sPerD;

        if (!willRollDay)
        {
            _slot = (DaySlot)(currentSlotIndex + 1); // Sang ca tiep theo
            OnSlotChanged?.Invoke();
            FireSlotStarted(); // Bat dau ca moi
            return;
        }

        NextDayInternal(); // Chuyen sang ngay moi
        _slot = DaySlot.MorningA; // Reset ve ca dau ngay
        OnSlotChanged?.Invoke();
        FireSlotStarted(); // Bat dau ca moi
    }

    // Dat thoi gian moi, chuan hoa va kich hoat su kien
    public void SetTime(int year, int term, int week, int dayIndex1Based, DaySlot slot)
    {
        // lưu trạng thái cũ
        int oldYear = _year, oldTerm = _term, oldWeek = _week, oldDay = _day;
        DaySlot oldSlot = _slot;

        // gán mới
        _year = Mathf.Max(1, year);
        _term = Mathf.Max(1, term);
        _week = Mathf.Max(1, week);
        _day = Mathf.Max(1, dayIndex1Based);
        _slot = slot;

        NormalizeNow(fullClamp: true);

        // CHỈ phát event khi có thay đổi
        if (oldYear != _year) OnYearChanged?.Invoke();
        if (oldTerm != _term) OnTermChanged?.Invoke();
        if (oldWeek != _week) OnWeekChanged?.Invoke();
        if (oldDay != _day) OnDayChanged?.Invoke();
        if (oldSlot != _slot) { OnSlotChanged?.Invoke(); FireSlotStarted(); }
    }

    public void SetSlotOnly(DaySlot newSlot)
    {
        if (_slot == newSlot) return;
        _slot = newSlot;
        NormalizeNow(fullClamp: false);
        OnSlotChanged?.Invoke();
        FireSlotStarted();
    }


    // Kiem tra ngay co phai ngay day hoc
    public bool IsTeachingDay(Weekday d) =>
        config != null && ((IReadOnlyList<Weekday>)config.TeachingDays).Contains(d);

    // Kiem tra ca co duoc phep xep lich
    public bool IsSchedulableSlot(DaySlot s) =>
        config != null && !((IReadOnlyList<DaySlot>)config.BlockedSlots).Contains(s);

    // Lay chi so ca (1-based)
    public int GetSlotIndex1Based() => (int)_slot + 1;

    // Chuyen enum Weekday thanh ten tieng Anh
    public static string WeekdayToVN(Weekday d) => d switch
    {
        Weekday.Mon => "Thứ Hai",
        Weekday.Tue => "Thứ Ba",
        Weekday.Wed => "Thứ Tư",
        Weekday.Thu => "Thứ Năm",
        Weekday.Fri => "Thứ Sáu",
        Weekday.Sat => "Thứ Bảy",
        _ => "Chủ Nhật"
    };

    // Lay ten ngay hien tai bang tieng Anh
    public string CurrentDayNameVN() => WeekdayToVN(Weekday);

    /// <summary>
    /// **MỚI: Lấy năm học dựa trên kỳ hiện tại**
    /// Ví dụ: Kỳ 1,2 = Năm học 1; Kỳ 3,4 = Năm học 2; ...
    /// </summary>
    public int GetAcademicYear()
    {
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;
        return (((_term - 1) / tPerY) + 1);
    }

    /// <summary>
    /// **MỚI: Lấy kỳ trong năm học (1 hoặc 2)**
    /// Ví dụ: Kỳ 1,3,5,7,9 = Kỳ 1 trong năm; Kỳ 2,4,6,8,10 = Kỳ 2 trong năm
    /// </summary>
    public int GetTermInYear()
    {
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;
        return (((_term - 1) % tPerY) + 1);
    }

    /// <summary>
    /// **MỚI: Lấy chuỗi mô tả năm học và kỳ**
    /// Ví dụ: "Năm 1 - Kỳ 1", "Năm 2 - Kỳ 2", etc.
    /// </summary>
    public string GetAcademicYearString()
    {
        return $"Năm {GetAcademicYear()} - Kỳ {GetTermInYear()}";
    }

    /// <summary>
    /// **MỚI: Debug method để hiển thị thông tin kỳ học**
    /// </summary>
    [ContextMenu("Show Academic Info")]
    public void ShowAcademicInfo()
    {
        Debug.Log($"[GameClock] === THÔNG TIN KỲ HỌC ===");
        Debug.Log($"[GameClock] Kỳ tuyệt đối: {_term}");
        Debug.Log($"[GameClock] Năm học: {GetAcademicYear()}");
        Debug.Log($"[GameClock] Kỳ trong năm: {GetTermInYear()}");
        Debug.Log($"[GameClock] Chuỗi mô tả: {GetAcademicYearString()}");
        Debug.Log($"[GameClock] Tuần: {_week} | Ngày: {CurrentDayNameVN()} | Ca: {GetSlotIndex1Based()}");
    }

    /// <summary>
    /// **TEST: Simulate chuyển đến kỳ cụ thể để test logic**
    /// </summary>
    [ContextMenu("TEST: Jump to Term 3")]
    public void TestJumpToTerm3()
    {
        SetTime(2, 3, 1, 1, DaySlot.MorningA);
        ShowAcademicInfo();
        Debug.Log("[GameClock] TEST: Đã chuyển đến Kỳ 3 (Năm 2 - Kỳ 1)");
    }

    /// <summary>
    /// **TEST: Simulate chuyển đến kỳ 10 để test**
    /// </summary>
    [ContextMenu("TEST: Jump to Term 10")]
    public void TestJumpToTerm10()
    {
        SetTime(5, 10, 1, 1, DaySlot.MorningA);
        ShowAcademicInfo();
        Debug.Log("[GameClock] TEST: Đã chuyển đến Kỳ 10 (Năm 5 - Kỳ 2)");
    }

    /// <summary>
    /// **TEST: Chuyển nhanh nhiều kỳ để test logic tăng dần**
    /// </summary>
    [ContextMenu("TEST: Fast Forward Terms")]
    public void TestFastForwardTerms()
    {
        Debug.Log("[GameClock] === TEST FAST FORWARD TERMS ===");
        
        for (int testTerm = 1; testTerm <= 10; testTerm++)
        {
            int expectedYear = ((testTerm - 1) / 2) + 1;
            int expectedTermInYear = ((testTerm - 1) % 2) + 1;
            
            SetTime(expectedYear, testTerm, 1, 1, DaySlot.MorningA);
            
            Debug.Log($"[GameClock] Kỳ {testTerm}: Năm {GetAcademicYear()}, Kỳ trong năm {GetTermInYear()} | Expected: Năm {expectedYear}, Kỳ {expectedTermInYear}");
            
            if (GetAcademicYear() != expectedYear || GetTermInYear() != expectedTermInYear)
            {
                Debug.LogError($"[GameClock] ❌ LOGIC ERROR at Term {testTerm}!");
            }
        }
        
        Debug.Log("[GameClock] === TEST COMPLETED ===");
    }

    /// <summary>
    /// **TEST: Test chuyển tuần để trigger chuyển kỳ**
    /// </summary>
    [ContextMenu("TEST: Trigger Term Change")]
    public void TestTriggerTermChange()
    {
        Debug.Log("[GameClock] === TEST TRIGGER TERM CHANGE ===");
        
        // Set to week 6, should trigger term change on next week
        int currentTerm = _term;
        SetTime(_year, currentTerm, 6, 7, DaySlot.Evening); // Last day of week 6
        
        Debug.Log($"[GameClock] Before trigger: Term {_term}");
        ShowAcademicInfo();
        
        // Trigger next slot -> should go to next day -> next week -> next term
        NextSlot();
        
        Debug.Log($"[GameClock] After trigger: Term {_term}");
        ShowAcademicInfo();
        
        if (_term == currentTerm + 1)
        {
            Debug.Log("[GameClock] ✅ Term change triggered successfully!");
        }
        else
        {
            Debug.LogError($"[GameClock] ❌ Term change failed! Expected {currentTerm + 1}, got {_term}");
        }
        
        Debug.Log("[GameClock] === TEST COMPLETED ===");
    }

    // Chuyen sang ngay moi, cap nhat tuan/ky/nam neu can
    private void NextDayInternal()
    {
        _day++;
        OnDayChanged?.Invoke();

        int dPerW = config ? Mathf.Clamp(config.daysPerWeek, 1, 7) : 7;
        int wPerT = config ? Mathf.Max(1, config.weeksPerTerm) : 6;
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;

        if (_day > dPerW)
        {
            _day = 1;
            _week++;
            OnWeekChanged?.Invoke();

            if (_week > wPerT)
            {
                _week = 1;
                _term++;
                OnTermChanged?.Invoke();

                // **SỬA LỖI: Logic chuyển kỳ - kỳ tăng liên tục, không reset**
                // Tính năm dựa trên kỳ hiện tại: mỗi tPerY kỳ = 1 năm
                int newYear = (((_term - 1) / tPerY) + 1);
                if (newYear != _year)
                {
                    _year = newYear;
                    OnYearChanged?.Invoke();
                    Debug.Log($"[GameClock] Chuyển sang năm {_year}, kỳ {_term}");
                }
                else
                {
                    Debug.Log($"[GameClock] Chuyển sang kỳ {_term}, năm {_year}");
                }
            }
        }
        
        Debug.Log($"[GameClock] NextDayInternal: Y{_year} T{_term} W{_week} D{_day}");
    }

    // Chuan hoa thoi gian hien tai
    private void NormalizeNow(bool fullClamp)
    {
        int dPerW = config ? Mathf.Clamp(config.daysPerWeek, 1, 7) : 7;
        int wPerT = config ? Mathf.Max(1, config.weeksPerTerm) : 6;
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;
        int sPerD = config ? Mathf.Clamp(config.slotsPerDay, 1, 5) : 4;

        if (fullClamp)
        {
            // **SỬA LỖI: Không clamp term nữa, cho phép kỳ tăng liên tục**
            // Chỉ clamp tuần và ngày
            _week = Mathf.Clamp(_week, 1, wPerT);
            _day = Mathf.Clamp(_day, 1, dPerW);
            
            // **THÊM: Tính toán năm dựa trên kỳ hiện tại**
            // Đảm bảo year được cập nhật đúng theo term
            int calculatedYear = (((_term - 1) / tPerY) + 1);
            if (_year < calculatedYear)
            {
                _year = calculatedYear;
                Debug.Log($"[GameClock] NormalizeNow: Điều chỉnh năm thành {_year} dựa trên kỳ {_term}");
            }
        }

        int maxSlotIndex = Mathf.Min((int)DaySlot.Evening, sPerD - 1);
        _slot = (DaySlot)Mathf.Clamp((int)_slot, 0, Mathf.Max(0, maxSlotIndex));
    }

    // Kich hoat su kien bat dau ca
    private void FireSlotStarted()
    {
        OnSlotStarted?.Invoke(_week, CurrentDayNameVN(), GetSlotIndex1Based());
    }

    // Kich hoat su kien ket thuc ca
    private void FireSlotEnded()
    {
        OnSlotEnded?.Invoke(_week, CurrentDayNameVN(), GetSlotIndex1Based());
    }
}