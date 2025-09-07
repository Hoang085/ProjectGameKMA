using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Quản lý dòng thời gian: Năm → Kì → Tuần → Ngày → Ca
/// Data lấy từ CalendarConfig (terms, weeks, days, slots, teachingDays, blockedSlots)
public class GameClock : MonoBehaviour
{
    public static GameClock I { get; private set; }

    [Header("Config (gán asset CalendarConfig)")]
    public CalendarConfig config;

    [Header("Thời gian hiện tại (1-based)")]
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

    // Events cho UI/hệ thống khác lắng nghe
    public event Action OnSlotChanged, OnDayChanged, OnWeekChanged, OnTermChanged, OnYearChanged;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        if (!config) Debug.LogWarning("[GameClock] Chưa gán CalendarConfig!");
        NormalizeNow(fullClamp: true);
    }

    // ===================== API =====================

    /// Tăng thời gian lên 1 ca.
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

        // qua ngày
        _slot = DaySlot.MorningA;
        OnSlotChanged?.Invoke();
        NextDayInternal();
    }

    /// Đặt thẳng thời gian (phục vụ load/save)
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

    /// Hôm nay có phải ngày dạy (Mon–Fri) không?
    public bool IsTeachingDay(Weekday d) =>
        config != null && ((IReadOnlyList<Weekday>)config.TeachingDays).Contains(d);

    /// Slot hiện tại có được phép xếp hoạt động không? (ví dụ chặn buổi tối)
    public bool IsSchedulableSlot(DaySlot s) =>
        config != null && !((IReadOnlyList<DaySlot>)config.BlockedSlots).Contains(s);

    public int GetSlotIndex1Based() => (int)_slot + 1;

    public string GetFormattedLabelEN() =>
        $"Year {_year} – Term {_term} – Week {_week} – {WeekdayToEN(Weekday)} – {SlotToEN(_slot)}";
    public string GetFormattedLabelVN() =>
        $"Năm {_year} – Kì {_term} – Tuần {_week} – {WeekdayToVN(Weekday)} – {SlotToVN(_slot)}";

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
    public static string WeekdayToVN(Weekday d) => d switch
    {
        Weekday.Mon => "Thứ 2",
        Weekday.Tue => "Thứ 3",
        Weekday.Wed => "Thứ 4",
        Weekday.Thu => "Thứ 5",
        Weekday.Fri => "Thứ 6",
        Weekday.Sat => "Thứ 7",
        _ => "Chủ nhật"
    };
    public static string SlotToEN(DaySlot s) => s switch
    {
        DaySlot.MorningA => "Morning 1",
        DaySlot.MorningB => "Morning 2",
        DaySlot.AfternoonA => "Afternoon 1",
        DaySlot.AfternoonB => "Afternoon 2",
        _ => "Evening"
    };
    public static string SlotToVN(DaySlot s) => s switch
    {
        DaySlot.MorningA => "Sáng 1",
        DaySlot.MorningB => "Sáng 2",
        DaySlot.AfternoonA => "Chiều 1",
        DaySlot.AfternoonB => "Chiều 2",
        _ => "Tối"
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

    /// Đảm bảo state nằm trong phạm vi config
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
