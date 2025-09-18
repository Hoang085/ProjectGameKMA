using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Quan ly dong thoi gian: Nawm, Ky, Tuan, Ngay, Ca
/// Data lay tu CalendarConfig (terms, weeks, days, slots, teachingDays, blockedSlots)
public class GameClock : Singleton<GameClock>
{
    [Header("Config (Assign asset CalendarConfig)")]
    public CalendarConfig config;

    [Header("Current Time (1-based)")]
    [SerializeField] private int _year = 1;
    [SerializeField] private int _term = 1;    // 1..termsPerYear
    [SerializeField] private int _week = 1;    // 1..weeksPerTerm
    [SerializeField] private int _day = 1;     // 1..daysPerWeek (1 = Monday)
    [SerializeField] private DaySlot _slot = DaySlot.MorningA;

    // Getters
    public int Year => _year;
    public int Term => _term;
    public int Week => _week;
    public int DayIndex => _day; // 1..daysPerWeek
    public Weekday Weekday => (Weekday)Mathf.Clamp(_day - 1, 0, 6);
    public DaySlot Slot => _slot;

    // Events for System listeners
    public event Action OnSlotChanged, OnDayChanged, OnWeekChanged, OnTermChanged, OnYearChanged;

    public override void Awake()
    {
        MakeSingleton(false);

        if (!config) Debug.LogWarning("[GameClock] don't assign CalendarConfig!");
        NormalizeNow(fullClamp: true);
    }

    // ===================== API =====================
    /// Increase to next slot; if overflow, go to next day
    [ContextMenu("Next Slot")]
    public void NextSlot()
    {
        int sPerD = config ? Mathf.Clamp(config.slotsPerDay, 1, 5) : 4;
        int nextSlot = (int)_slot + 1;

        if (nextSlot < sPerD)
        {
            _slot = (DaySlot)nextSlot;
            OnSlotChanged?.Invoke();
            return;
        }

        // Over day
        _slot = DaySlot.MorningA;
        OnSlotChanged?.Invoke();
        NextDayInternal();
    }

    /// Setup time; auto normalize
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
    }

    /// check teaching day for Mon..Fri
    public bool IsTeachingDay(Weekday d) =>
        config != null && ((IReadOnlyList<Weekday>)config.TeachingDays).Contains(d);

    ///Slot hien tai co duoc phep xep hoat dong khong? (vi du chan buoi toi)
    public bool IsSchedulableSlot(DaySlot s) =>
        config != null && !((IReadOnlyList<DaySlot>)config.BlockedSlots).Contains(s);

    public int GetSlotIndex1Based() => (int)_slot + 1;

    // ================= Helpers (text) =================
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

    // ================= Internal =================
    private void NextDayInternal()
    {
        _day++;
        OnDayChanged?.Invoke();

        int dPerW = config ? Mathf.Clamp(config.daysPerWeek, 1, 7) : 7;
        int wPerT = config ? Mathf.Max(1, config.weeksPerTerm) : 5;
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

                if (_term > tPerY)
                {
                    _term = 1;
                    _year++;
                    OnYearChanged?.Invoke();
                }
            }
        }
    }

    /// Dam bao state hien tai hop le
    private void NormalizeNow(bool fullClamp)
    {
        int dPerW = config ? Mathf.Clamp(config.daysPerWeek, 1, 7) : 7;
        int wPerT = config ? Mathf.Max(1, config.weeksPerTerm) : 5;
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;
        int sPerD = config ? Mathf.Clamp(config.slotsPerDay, 1, 5) : 4;

        if (fullClamp)
        {
            _term = Mathf.Clamp(_term, 1, tPerY);
            _week = Mathf.Clamp(_week, 1, wPerT);
            _day = Mathf.Clamp(_day, 1, dPerW);
        }

        int maxSlotIndex = Mathf.Min((int)DaySlot.Evening, sPerD - 1);
        _slot = (DaySlot)Mathf.Clamp((int)_slot, 0, Mathf.Max(0, maxSlotIndex));
    }
}
