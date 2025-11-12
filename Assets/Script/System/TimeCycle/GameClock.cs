using System;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class GameClock : Singleton<GameClock>
{
    [Header("Config (assign CalendarConfig)")]
    public CalendarConfig config;

    [Header("Current Time (1-based)")]
    [SerializeField] private int _year = 1;              // Năm học
    [SerializeField] private int _term = 1;              // Kỳ học 
    [SerializeField] private int _week = 1;              // Tuần trong kỳ
    [SerializeField] private int _day = 1;               // Ngày trong tuần 
    [SerializeField] private DaySlot _slot = DaySlot.MorningA; // Ca hiện tại
    [SerializeField] private int _minuteOfDay = 7 * 60;

    [Header("Game-time speed")]
    [Tooltip("1 phút TRONG GAME = X giây THẬT (unscaled time)")]
    [Min(0.01f)] public float secondsPerGameMinute = 30f;

    [Header("Pause Control")]
    [Tooltip("Khi true, GameClock sẽ tạm dừng (không tính thời gian)")]
    [SerializeField] private bool _isPaused = false;

    [Header("Session (slot) boundaries - start minutes")]
    public int tSession1 = 7 * 60;
    public int tSession2 = 9 * 60 + 30;
    public int tSession3 = 12 * 60 + 30;
    public int tSession4 = 15 * 60;
    public int tSession5 = 17 * 60;

    const int MIN_PER_DAY = 24 * 60;

    // Getters (đọc-only)
    public int Year => _year;
    public int Term => _term;
    public int Week => _week;
    public int DayIndex => _day; // 1..daysPerWeek
    public Weekday Weekday => (Weekday)Mathf.Clamp(_day - 1, 0, 6);
    public DaySlot Slot => _slot;
    public int MinuteOfDay => _minuteOfDay;
    public int SlotIndex1Based => ((int)_slot) + 1;
    
    /// <summary>
    /// Kiểm tra xem GameClock có đang bị tạm dừng không
    /// </summary>
    public bool IsPaused => _isPaused;

    // EVENTS
    public event Action OnSlotChanged, OnDayChanged, OnWeekChanged, OnTermChanged, OnYearChanged;
    public event Action<int, string, int> OnSlotStarted; // args: week, dayNameVN, slotIdx(1..5)
    public event Action<int, string, int> OnSlotEnded;
    public event Action<int> OnMinuteChanged;            // NEW: minuteOfDay (0..1439) thay đổi

    // Internal tick
    float _secAcc;
    bool _started;

    // ===== Unity lifecycle =====
    public override void Awake()
    {
        MakeSingleton(false);
        if (!config) Debug.LogWarning("[GameClock] CalendarConfig chưa được gán!");
        NormalizeNow(fullClamp: true);
    }

    void OnEnable()
    {
        // Khởi phát ca hiện tại để mọi hệ thống UI có trạng thái ban đầu
        FireSlotStarted();
        _started = true;
    }

    void Update()
    {
        // **MỚI: Kiểm tra pause trước khi tính thời gian**
        if (_isPaused)
        {
            return; // Không tính thời gian khi pause
        }
        
        // dùng unscaled time để không bị ảnh hưởng bởi Time.timeScale
        _secAcc += Time.unscaledDeltaTime;
        if (_secAcc >= secondsPerGameMinute)
        {
            int add = Mathf.FloorToInt(_secAcc / secondsPerGameMinute);
            _secAcc -= add * secondsPerGameMinute;
            if (add > 0) AdvanceMinutes(add);
        }

        // hotkeys optional (giữ nguyên cho tiện test)
        if (Input.GetKeyDown(KeyCode.N)) JumpToNextSessionStart();
        if (Input.GetKeyDown(KeyCode.LeftBracket)) { secondsPerGameMinute *= 2f; Debug.Log($"[GameClock] Slower → {secondsPerGameMinute:F2}s/min"); }
        if (Input.GetKeyDown(KeyCode.RightBracket)) { secondsPerGameMinute = Mathf.Max(0.01f, secondsPerGameMinute * 0.5f); Debug.Log($"[GameClock] Faster → {secondsPerGameMinute:F2}s/min"); }
    }

    // ======= TIME FLOW (data + logic moved from ClockUI) =======

    /// <summary>Tiến thời gian trong ngày thêm 'delta' phút game, tự xử lý đổi ca/ngày/tuần/kỳ/năm.</summary>
    public void AdvanceMinutes(int delta)
    {
        if (delta <= 0) return;

        int before = _minuteOfDay;
        _minuteOfDay = ((_minuteOfDay + delta) % MIN_PER_DAY + MIN_PER_DAY) % MIN_PER_DAY;
        OnMinuteChanged?.Invoke(_minuteOfDay);

        // kiểm tra ranh giới ca (dựa theo thứ tự thời gian trong ngày)
        int toSession = GetSessionIndexFromMinute(_minuteOfDay);

        // Cross helpers (xét vòng qua 0h)
        bool Crossed(int a, int b, int t)
        {
            if (a <= b) return a < t && b >= t;
            return a < t || b >= t;
        }

        // đổi ca trong cùng ngày
        if (Crossed(before, _minuteOfDay, tSession2)) TryAdvanceSlotTo(2);
        if (Crossed(before, _minuteOfDay, tSession3)) TryAdvanceSlotTo(3);
        if (Crossed(before, _minuteOfDay, tSession4)) TryAdvanceSlotTo(4);
        if (Crossed(before, _minuteOfDay, tSession5)) TryAdvanceSlotTo(5);

        // qua ngày mới (Ca5 -> Ca1 ở mốc tSession1)
        if (Crossed(before, _minuteOfDay, tSession1))
        {
            // chỉ khi đang ở ca 5 mới sang ca 1 + qua ngày
            if (SlotIndex1Based == 5) NextSlot(); // NextSlot sẽ roll day và set slot=1
            else
            {
                // nếu vì lý do nào đó không ở 5, ta sync slot theo minute để nhất quán
                SyncSlotWithMinute(force: true);
            }
        }
        else
        {
            // không qua mốc đầu ngày → đảm bảo slot nhất quán với minute (không force để giữ tiến trình NextSlot)
            SyncSlotWithMinute(force: false);
        }
    }

    /// <summary>Đặt minute-of-day (0..1439). Nếu syncSlot = true thì tự căn lại ca cho khớp.</summary>
    public void SetMinuteOfDay(int minute, bool syncSlot = true)
    {
        int clamped = ((minute % MIN_PER_DAY) + MIN_PER_DAY) % MIN_PER_DAY;
        if (clamped == _minuteOfDay && !syncSlot) return;

        _minuteOfDay = clamped;
        OnMinuteChanged?.Invoke(_minuteOfDay);
        if (syncSlot) SyncSlotWithMinute(force: true);
    }

    /// <summary>Nhảy tới mốc đầu ca tiếp theo (giữ ngày/tuần/kỳ theo logic NextSlot).</summary>
    public void JumpToNextSessionStart()
    {
        int cur = SlotIndex1Based; // 1..5
        int targetMin = cur switch
        {
            1 => tSession2,
            2 => tSession3,
            3 => tSession4,
            4 => tSession5,
            _ => tSession1 // 5 -> đầu ngày mới
        };

        if (cur == 5)
        {
            _minuteOfDay = targetMin;        // 07:00
            OnMinuteChanged?.Invoke(_minuteOfDay);
            NextSlot();                       // 5 -> (day+1, slot=1)
        }
        else
        {
            _minuteOfDay = targetMin;        // tới đầu ca tiếp theo trong ngày
            OnMinuteChanged?.Invoke(_minuteOfDay);
            NextSlot();                       // slot + 1
        }
    }

    /// <summary>Đổi ca sang chỉ số mong muốn (1..5) nếu đúng ca kế tiếp.</summary>
    void TryAdvanceSlotTo(int expectedNextSessionIndex1Based)
    {
        if (SlotIndex1Based + 1 == expectedNextSessionIndex1Based)
            NextSlot();
    }

    /// <summary>Đồng bộ Slot theo _minuteOfDay (không gọi NextSlot nếu không cần).</summary>
    void SyncSlotWithMinute(bool force)
    {
        int session = GetSessionIndexFromMinute(_minuteOfDay); // 1..5
        if (force || session != SlotIndex1Based)
        {
            SetSlotOnly(SlotFromIndex1Based(session));
        }
    }

    /// <summary>Mapping minute-of-day → session index (1..5).</summary>
    public int GetSessionIndexFromMinute(int min)
    {
        // lưu ý: khoảng 17:00..24:00 và 00:00..(trước tSession1) là Ca5
        if (InRange(min, tSession1, tSession2)) return 1;
        if (InRange(min, tSession2, tSession3)) return 2;
        if (InRange(min, tSession3, tSession4)) return 3;
        if (InRange(min, tSession4, tSession5)) return 4;
        return 5;

        bool InRange(int x, int start, int end) => start <= end ? (x >= start && x < end) : (x >= start || x < end);
    }

    // ======= Public controls for date/term =======

    [ContextMenu("Next Slot")]
    public void NextSlot()
    {
        int sPerD = config ? Mathf.Clamp(config.slotsPerDay, 1, 5) : 5;

        // kết thúc ca hiện tại
        FireSlotEnded();

        int currentSlotIndex = (int)_slot;
        bool willRollDay = (currentSlotIndex + 1) >= sPerD;

        if (!willRollDay)
        {
            _slot = (DaySlot)(currentSlotIndex + 1);
            OnSlotChanged?.Invoke();
            FireSlotStarted();
            return;
        }

        // hết ngày → sang ngày mới, slot=1
        NextDayInternal();
        _slot = DaySlot.MorningA;
        OnSlotChanged?.Invoke();
        FireSlotStarted();
    }

    /// <summary>Đặt thời gian tuyệt đối (year, term, week, day, slot). Tự normalize & phát event theo thay đổi.</summary>
    public void SetTime(int year, int term, int week, int dayIndex1Based, DaySlot slot)
    {
        int oldYear = _year, oldTerm = _term, oldWeek = _week, oldDay = _day; DaySlot oldSlot = _slot;

        _year = Mathf.Max(1, year);
        _term = Mathf.Max(1, term);
        _week = Mathf.Max(1, week);
        _day = Mathf.Max(1, dayIndex1Based);
        _slot = slot;

        NormalizeNow(fullClamp: true);

        if (oldYear != _year) OnYearChanged?.Invoke();
        if (oldTerm != _term) OnTermChanged?.Invoke();
        if (oldWeek != _week) OnWeekChanged?.Invoke();
        if (oldDay != _day) OnDayChanged?.Invoke();
        if (oldSlot != _slot)
        {
            OnSlotChanged?.Invoke();
            // khi set slot thủ công, coi như bắt đầu ca mới:
            FireSlotStarted();
        }
    }

    /// <summary>Chỉ set slot, giữ y/t/w/d, có clamp theo config.</summary>
    public void SetSlotOnly(DaySlot newSlot)
    {
        if (_slot == newSlot) return;
        _slot = ClampSlotByConfig(newSlot);
        OnSlotChanged?.Invoke();
        FireSlotStarted();
    }

    // ======= Helpers & academic info =======
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

    public string CurrentDayNameVN() => WeekdayToVN(Weekday);

    public int GetAcademicYear()
    {
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;
        return (((_term - 1) / tPerY) + 1);
    }

    public int GetTermInYear()
    {
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;
        return (((_term - 1) % tPerY) + 1);
    }

    public string GetAcademicYearString() => $"Năm {GetAcademicYear()} - Kỳ {GetTermInYear()}";

    public static string FormatHM(int minuteOfDay)
    {
        int h = Mathf.FloorToInt(minuteOfDay / 60f);
        int m = minuteOfDay % 60;
        return $"{h:00}:{m:00}";
    }

    // ======= Internals =======
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

                int newYear = (((_term - 1) / tPerY) + 1);
                if (newYear != _year)
                {
                    _year = newYear;
                    OnYearChanged?.Invoke();
                    Debug.Log($"[GameClock] → Năm {_year}, Kỳ {_term}");
                }
                else
                {
                    Debug.Log($"[GameClock] → Kỳ {_term}, Năm {_year}");
                }
            }
        }

        // khi sang ngày mới, đưa clock về đầu ngày theo mốc Ca1
        if (_started)
        {
            _minuteOfDay = tSession1;
            OnMinuteChanged?.Invoke(_minuteOfDay);
        }

        Debug.Log($"[GameClock] NextDay: Y{_year} T{_term} W{_week} D{_day}");
    }

    private void NormalizeNow(bool fullClamp)
    {
        int dPerW = config ? Mathf.Clamp(config.daysPerWeek, 1, 7) : 7;
        int wPerT = config ? Mathf.Max(1, config.weeksPerTerm) : 6;
        int tPerY = config ? Mathf.Max(1, config.termsPerYear) : 2;
        int sPerD = config ? Mathf.Clamp(config.slotsPerDay, 1, 5) : 5;

        if (fullClamp)
        {
            _week = Mathf.Clamp(_week, 1, wPerT);
            _day = Mathf.Clamp(_day, 1, dPerW);

            int calculatedYear = (((_term - 1) / tPerY) + 1);
            if (_year < calculatedYear)
            {
                _year = calculatedYear;
                Debug.Log($"[GameClock] NormalizeNow → Điều chỉnh Năm = {_year} theo Kỳ = {_term}");
            }
        }

        // clamp slot theo số ca/ngày trong config
        int maxSlotIndex = Mathf.Min((int)DaySlot.Evening, sPerD - 1);
        _slot = (DaySlot)Mathf.Clamp((int)_slot, 0, Mathf.Max(0, maxSlotIndex));

        // clamp minute-of-day hợp lệ
        _minuteOfDay = ((_minuteOfDay % MIN_PER_DAY) + MIN_PER_DAY) % MIN_PER_DAY;
        OnMinuteChanged?.Invoke(_minuteOfDay);
    }

    private DaySlot ClampSlotByConfig(DaySlot s)
    {
        int sPerD = config ? Mathf.Clamp(config.slotsPerDay, 1, 5) : 5;
        int maxSlotIndex = Mathf.Min((int)DaySlot.Evening, sPerD - 1);
        return (DaySlot)Mathf.Clamp((int)s, 0, Mathf.Max(0, maxSlotIndex));
    }

    private DaySlot SlotFromIndex1Based(int idx) => idx switch
    {
        1 => DaySlot.MorningA,
        2 => DaySlot.MorningB,
        3 => DaySlot.AfternoonA,
        4 => DaySlot.AfternoonB,
        5 => DaySlot.Evening,
        _ => DaySlot.MorningA
    };

    private void FireSlotStarted() => OnSlotStarted?.Invoke(_week, CurrentDayNameVN(), SlotIndex1Based);
    private void FireSlotEnded() => OnSlotEnded?.Invoke(_week, CurrentDayNameVN(), SlotIndex1Based);

    // ====== Convenience Queries ======
    public bool IsTeachingDay(Weekday d) =>
        config != null && ((System.Collections.Generic.IReadOnlyList<Weekday>)config.TeachingDays).Contains(d);

    public bool IsSchedulableSlot(DaySlot s) =>
        config != null && !((System.Collections.Generic.IReadOnlyList<DaySlot>)config.BlockedSlots).Contains(s);
    
    // ====== PAUSE/RESUME CONTROL ======
    
    /// <summary>
    /// Tạm dừng GameClock (thời gian sẽ không chạy)
    /// </summary>
    public void Pause()
    {
        if (_isPaused) return;
        _isPaused = true;
        Debug.Log("[GameClock] Đã tạm dừng");
    }
    
    /// <summary>
    /// Tiếp tục chạy GameClock sau khi pause
    /// </summary>
    public void Resume()
    {
        if (!_isPaused) return;
        _isPaused = false;
        Debug.Log("[GameClock] Đã tiếp tục");
    }
    
    /// <summary>
    /// Set trạng thái pause/resume theo tham số
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (paused) Pause();
        else Resume();
    }
}
