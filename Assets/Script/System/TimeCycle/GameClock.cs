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
        _year = Mathf.Max(1, year);
        _term = Mathf.Max(1, term);
        _week = Mathf.Max(1, week);
        _day = Mathf.Max(1, dayIndex1Based);
        _slot = slot;
        NormalizeNow(fullClamp: true);

        OnYearChanged?.Invoke();
        OnTermChanged?.Invoke();
        OnWeekChanged?.Invoke();
        OnDayChanged?.Invoke();
        OnSlotChanged?.Invoke();
        FireSlotStarted(); // Bat dau ca tai thoi diem moi
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
    public static string WeekdayToEN(Weekday d) => d switch
    {
        Weekday.Mon => "Monday",
        Weekday.Tue => "Tuesday",
        Weekday.Wed => "Wednesday",
        Weekday.Thu => "Thursday",
        Weekday.Fri => "Friday",
        Weekday.Sat => "Saturday",
        _ => "Sunday"
    };

    // Lay ten ngay hien tai bang tieng Anh
    public string CurrentDayNameEN() => WeekdayToEN(Weekday);

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

                // SỬA LỖI: Bỏ logic reset term về 1, chỉ tăng year khi cần
                // Kiểm tra xem có cần tăng year không (ví dụ: mỗi 2 kỳ = 1 năm)
                if (_term > 1 && (_term - 1) % tPerY == 0)
                {
                    _year++;
                    OnYearChanged?.Invoke();
                    Debug.Log($"[GameClock] Tăng năm lên {_year} tại kỳ {_term}");
                }
            }
        }
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
            // SỬA LỖI: Bỏ clamp term, cho phép term tăng liên tục
            // _term = Mathf.Clamp(_term, 1, tPerY); // ← COMMENT DÒNG NÀY
            _week = Mathf.Clamp(_week, 1, wPerT);
            _day = Mathf.Clamp(_day, 1, dPerW);
        }

        int maxSlotIndex = Mathf.Min((int)DaySlot.Evening, sPerD - 1);
        _slot = (DaySlot)Mathf.Clamp((int)_slot, 0, Mathf.Max(0, maxSlotIndex));
    }

    // Kich hoat su kien bat dau ca
    private void FireSlotStarted()
    {
        OnSlotStarted?.Invoke(_week, CurrentDayNameEN(), GetSlotIndex1Based());
    }

    // Kich hoat su kien ket thuc ca
    private void FireSlotEnded()
    {
        OnSlotEnded?.Invoke(_week, CurrentDayNameEN(), GetSlotIndex1Based());
    }
}