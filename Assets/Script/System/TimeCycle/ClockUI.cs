using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class ClockUI : MonoBehaviour
{
    [Header("Auto-Refs (leave empty to auto-find)")]
    [SerializeField] TextMeshProUGUI textTopDay;      // ScheduleBar/TextTopDay
    [SerializeField] TextMeshProUGUI textSession;     // ScheduleBar/TextBot/TextSession
    [SerializeField] TextMeshProUGUI textSemester;    // ScheduleBar/TextBot/TextSemester
    [SerializeField] TextMeshProUGUI textClock;       // TextClock
    [SerializeField] Image progressFilled;            // ScheduleBar/ProgressBar/Filled

    [Header("IconDay (1 Image + 3 Sprites)")]
    [SerializeField] Image iconDayImage;              // ScheduleBar/IconDay/Icon
    [SerializeField] Sprite iconMorning;              // Weather_0
    [SerializeField] Sprite iconAfternoon;            // Weather_1
    [SerializeField] Sprite iconNight;                // Weather_2

    [Header("Game-time speed")]
    [Tooltip("1 phút TRONG GAME = X giây THẬT")]
    [Min(0.01f)] public float secondsPerGameMinute = 30f;   // mặc định: 30s thật = 1 phút game

    [Header("Clock Warp Effect (when session changes)")]
    [Tooltip("Thời lượng hiệu ứng lướt số")]
    [Range(0.05f, 3f)] public float warpDuration = 0.8f;
    [Tooltip("Độ phóng to tối đa của text khi warp (1 = không phóng)")]
    [Range(1f, 1.6f)] public float warpScale = 1.12f;
    [Tooltip("Độ nhấn màu (alpha giữ nguyên)")]
    public Color warpTint = new Color(1f, 0.95f, 0.7f, 1f);

    const int MIN_PER_DAY = 24 * 60;
    int minuteOfDay;        // 0..1439 (thời gian thật trong game)
    int displayMinuteOfDay; // thời gian hiển thị (phục vụ hiệu ứng)
    float secAcc;

    // Slot edges (minutes)
    int t0700, t0930, t1230, t1500, t1700; // inclusive starts

    static readonly float[] FillBySession = { 1f, 0.8f, 0.6f, 0.4f, 0.2f };

    bool hooked;
    bool clockWarping;
    Color _clockBaseColor;
    Vector3 _clockBaseScale;

    void Awake()
    {
        // Auto-find
        if (!textTopDay) textTopDay = transform.Find("ScheduleBar/TextTopDay")?.GetComponent<TextMeshProUGUI>();
        if (!textSession) textSession = transform.Find("ScheduleBar/TextBot/TextSession")?.GetComponent<TextMeshProUGUI>();
        if (!textSemester) textSemester = transform.Find("ScheduleBar/TextBot/TextSemester")?.GetComponent<TextMeshProUGUI>();
        if (!textClock) textClock = transform.Find("TextClock")?.GetComponent<TextMeshProUGUI>();
        if (!progressFilled) progressFilled = transform.Find("ScheduleBar/ProgressBar/Filled")?.GetComponent<Image>();
        if (!iconDayImage) iconDayImage = transform.Find("ScheduleBar/IconDay/Icon")?.GetComponent<Image>();

        // slot boundaries (start minutes)
        t0700 = 7 * 60;
        t0930 = 9 * 60 + 30;
        t1230 = 12 * 60 + 30;
        t1500 = 15 * 60;
        t1700 = 17 * 60;

        // Mặc định bắt đầu 07:00
        minuteOfDay = 7 * 60;
        displayMinuteOfDay = minuteOfDay;

        if (textClock)
        {
            _clockBaseColor = textClock.color;
            _clockBaseScale = textClock.rectTransform.localScale;
        }
    }

    void OnEnable()
    {
        TryHookGameClock();
        RefreshUI(); // initial paint
    }

    void OnDisable()
    {
        if (hooked && GameClock.Ins)
        {
            GameClock.Ins.OnSlotChanged -= OnSlotChangedRefreshAndWarp;
            GameClock.Ins.OnDayChanged -= RefreshUI;
            GameClock.Ins.OnWeekChanged -= RefreshUI;
            GameClock.Ins.OnTermChanged -= RefreshUI;
            GameClock.Ins.OnYearChanged -= RefreshUI;
            hooked = false;
        }
    }

    void Start()
    {
        Debug.Log($"[ClockUI] secondsPerGameMinute = {secondsPerGameMinute} (unscaled time)");
    }

    void Update()
    {
        TryHookGameClock();

        // Luôn dùng thời gian thật, không phụ thuộc timeScale
        secAcc += Time.unscaledDeltaTime;

        if (secAcc >= secondsPerGameMinute)
        {
            int add = Mathf.FloorToInt(secAcc / secondsPerGameMinute);
            secAcc -= add * secondsPerGameMinute;

            if (add > 0)
                AdvanceMinutes(add);
        }

        // Render clock text: ưu tiên số hiển thị (đang warp) → nếu không warp thì bám số thật
        int minutesToShow = clockWarping ? displayMinuteOfDay : minuteOfDay;
        if (textClock)
        {
            int hh = minutesToShow / 60;
            int mm = minutesToShow % 60;
            textClock.text = $"{hh:00}:{mm:00}";
        }

        // TEST: N → jump to next session (sync time + GameClock)
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (GameClock.Ins != null)
            {
                Debug.Log("[ClockUI] N pressed → Jump to next session (time + slot)");
                JumpToNextSessionNow(); // sẽ phát OnSlotChanged → warp
            }
            else
            {
                Debug.LogWarning("[ClockUI] N pressed but GameClock.Ins == null");
            }
        }

        // Hotkeys để chỉnh tốc độ nhanh (tuỳ chọn)
        if (Input.GetKeyDown(KeyCode.Alpha1)) { secondsPerGameMinute = 30f; Debug.Log("[ClockUI] Speed set: 30s per game minute"); }
        if (Input.GetKeyDown(KeyCode.LeftBracket)) { secondsPerGameMinute *= 2f; Debug.Log($"[ClockUI] Slower → {secondsPerGameMinute:F2}s/min"); }
        if (Input.GetKeyDown(KeyCode.RightBracket)) { secondsPerGameMinute = Mathf.Max(0.01f, secondsPerGameMinute * 0.5f); Debug.Log($"[ClockUI] Faster → {secondsPerGameMinute:F2}s/min"); }
    }

    void TryHookGameClock()
    {
        if (hooked || !GameClock.Ins) return;
        // Khi đổi ca → vừa refresh vừa kích hoạt hiệu ứng warp
        GameClock.Ins.OnSlotChanged += OnSlotChangedRefreshAndWarp;
        GameClock.Ins.OnDayChanged += RefreshUI;
        GameClock.Ins.OnWeekChanged += RefreshUI;
        GameClock.Ins.OnTermChanged += RefreshUI;
        GameClock.Ins.OnYearChanged += RefreshUI;
        hooked = true;
        RefreshUI();
    }

    void OnSlotChangedRefreshAndWarp()
    {
        RefreshUI();
        TriggerClockWarp();
    }

    // time-of-day driver → tells GameClock when to change slot/day
    void AdvanceMinutes(int delta)
    {
        int before = minuteOfDay;
        minuteOfDay = (minuteOfDay + delta) % MIN_PER_DAY;

        int targetSession = GetSessionIndex(minuteOfDay);

        if (Crossed(before, minuteOfDay, t0930)) TryAdvanceToNextSession(2);
        if (Crossed(before, minuteOfDay, t1230)) TryAdvanceToNextSession(3);
        if (Crossed(before, minuteOfDay, t1500)) TryAdvanceToNextSession(4);
        if (Crossed(before, minuteOfDay, t1700)) TryAdvanceToNextSession(5);
        if (Crossed(before, minuteOfDay, t0700)) TryAdvanceFrom5To1();

        UpdateIconsBySession(targetSession);
        UpdateProgressDiscrete();
    }

    // crossed time a→b over threshold t (forward, considering midnight wrap)
    bool Crossed(int a, int b, int t)
    {
        if (a <= b) return a < t && b >= t;
        return a < t || b >= t; // wrapped midnight
    }

    int GetSessionIndex(int minOfDay)
    {
        if (minOfDay >= t0700 && minOfDay < t0930) return 1;
        if (minOfDay >= t0930 && minOfDay < t1230) return 2;
        if (minOfDay >= t1230 && minOfDay < t1500) return 3;
        if (minOfDay >= t1500 && minOfDay < t1700) return 4;
        return 5; // 17:00..24:00 and 00:00..07:00
    }

    void TryAdvanceToNextSession(int expectedNextSessionIndex1Based)
    {
        if (!GameClock.Ins) return;
        int nowIdx = GameClock.Ins.GetSlotIndex1Based();
        if (nowIdx + 1 == expectedNextSessionIndex1Based)
            GameClock.Ins.NextSlot(); // same day → OnSlotChanged → warp
    }

    void TryAdvanceFrom5To1()
    {
        if (!GameClock.Ins) return;
        if (GameClock.Ins.GetSlotIndex1Based() == 5)
            GameClock.Ins.NextSlot(); // 5 → next day (1) → OnSlotChanged → warp
    }

    // ===== UI binding to GameClock =====
    void RefreshUI()
    {
        if (!GameClock.Ins) return;

        if (textTopDay) textTopDay.text = GameClock.WeekdayToEN(GameClock.Ins.Weekday);
        if (textSession) textSession.text = "Session: " + GameClock.Ins.GetSlotIndex1Based();
        if (textSemester) textSemester.text = "Semester: " + GameClock.Ins.Term;

        UpdateIconsBySession(GameClock.Ins.GetSlotIndex1Based());
        UpdateProgressDiscrete();
    }

    void UpdateIconsBySession(int sessionIdx)
    {
        if (!iconDayImage) return;
        if (sessionIdx == 1 || sessionIdx == 2) iconDayImage.sprite = iconMorning;
        else if (sessionIdx == 3 || sessionIdx == 4) iconDayImage.sprite = iconAfternoon;
        else iconDayImage.sprite = iconNight;
    }

    void UpdateProgressDiscrete()
    {
        if (!progressFilled || !GameClock.Ins) return;
        int s = Mathf.Clamp(GameClock.Ins.GetSlotIndex1Based(), 1, 5);
        progressFilled.fillAmount = FillBySession[s - 1];
    }

    // ==== Clock warp (effect only for TextClock) ====
    void TriggerClockWarp()
    {
        if (!textClock) return;
        StopCoroutine(nameof(CoClockWarp));
        StartCoroutine(nameof(CoClockWarp));
    }

    System.Collections.IEnumerator CoClockWarp()
    {
        clockWarping = true;

        // Cache base states
        var rt = textClock.rectTransform;
        var baseScale = _clockBaseScale;
        var baseColor = _clockBaseColor;

        float t = 0f;
        int startMin = displayMinuteOfDay;
        int endMin = minuteOfDay;

        // Nếu cần, chọn hướng "tiến về phía trước" (qua nửa đêm thì cuộn tiếp tục)
        int forwardDist = ((endMin - startMin) % MIN_PER_DAY + MIN_PER_DAY) % MIN_PER_DAY;
        // Đảm bảo luôn cuộn tiến (không cuộn lùi)
        int WrapLerp(float from, float to, float k)
        {
            // nội suy theo quãng đường forwardDist
            float v = (from + forwardDist * k);
            int mi = Mathf.FloorToInt(v) % MIN_PER_DAY;
            return mi;
        }

        while (t < warpDuration)
        {
            float k = t / warpDuration;

            // EaseOutCubic cho lướt mượt
            float ease = 1f - Mathf.Pow(1f - k, 3f);

            // Lăn số phút hiển thị tiến dần đến endMin (theo quãng forwardDist)
            displayMinuteOfDay = WrapLerp(startMin, endMin, ease);

            // Scale bounce: easeOut lên warpScale rồi về 1.0 (yoyo)
            float bounce = Mathf.Sin(ease * Mathf.PI); // 0→1→0
            float scale = Mathf.Lerp(1f, warpScale, bounce);
            rt.localScale = baseScale * scale;

            // Tint nhẹ: blend màu base → warpTint → base
            Color mid = Color.Lerp(baseColor, warpTint, 0.6f);
            Color cur = Color.Lerp(Color.Lerp(baseColor, mid, Mathf.Clamp01(ease * 2f)), // đi lên nửa đầu
                                   Color.Lerp(mid, baseColor, Mathf.Clamp01((ease - 0.5f) * 2f)), // về nửa sau
                                   0.5f);
            cur.a = baseColor.a; // giữ alpha
            textClock.color = cur;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Kết thúc: chốt hiển thị đúng thời gian thật và reset visual
        displayMinuteOfDay = minuteOfDay;
        if (textClock)
        {
            textClock.rectTransform.localScale = baseScale;
            textClock.color = baseColor;
        }
        clockWarping = false;
    }

    // Handy buttons
    public void OnClickNextSlot() { if (GameClock.Ins) GameClock.Ins.NextSlot(); }
    public void SetSpeed(float secondsPerMinute) { secondsPerGameMinute = Mathf.Max(0.01f, secondsPerMinute); }

    // ==== Jump-to-next-session support (for key N) ====
    int GetNextSessionStartMinute(int sessionIdx)
    {
        // sessionIdx: 1..5
        switch (sessionIdx)
        {
            case 1: return t0930;
            case 2: return t1230;
            case 3: return t1500;
            case 4: return t1700;
            default: return t0700; // ca5 -> ca1 (ngày mới)
        }
    }

    public void JumpToNextSessionNow()
    {
        if (!GameClock.Ins) return;

        int curSession = GameClock.Ins.GetSlotIndex1Based(); // 1..5
        int targetMin = GetNextSessionStartMinute(curSession);

        if (curSession == 5) // sang ngày mới
        {
            minuteOfDay = targetMin; // 07:00
            GameClock.Ins.NextSlot(); // 5 -> (day+1, slot=1) → OnSlotChanged → warp
        }
        else
        {
            minuteOfDay = targetMin; // đến mốc đầu ca tiếp theo
            GameClock.Ins.NextSlot(); // slot -> slot+1 trong ngày → OnSlotChanged → warp
        }

        RefreshUI();
    }

    public int GetMinuteOfDay() => minuteOfDay;

    // minute: 0..1439. Nếu syncGameClock = true → tự chỉnh lại ca trong GameClock cho khớp minute
    public void SetMinuteOfDay(int minute, bool syncGameClock = true)
    {
        minuteOfDay = ((minute % 1440) + 1440) % 1440;

        if (syncGameClock && GameClock.Ins)
        {
            int s = GetSessionIndex(minuteOfDay); // đã có sẵn trong ClockUI
            int now = GameClock.Ins.GetSlotIndex1Based();
            if (s != now)
            {
                // Đưa GameClock về đúng ca theo minute hiện tại:
                // Cách đơn giản: gọi SetTime giữ nguyên y/t/w/d, chỉ đổi slot
                GameClock.Ins.SetTime(
                GameClock.Ins.Year,
                GameClock.Ins.Term,
                GameClock.Ins.Week,
                GameClock.Ins.DayIndex,
                SlotFromIndex1Based(s)
            );

            }

            // Cập nhật hiển thị ngay
            RefreshUI();
        }
    }

    private DaySlot SlotFromIndex1Based(int idx)
    {
        // mapping: 1 = MorningA, 2 = MorningB, 3 = AfternoonA, 4 = AfternoonB, 5 = Evening
        return idx switch
        {
            1 => DaySlot.MorningA,
            2 => DaySlot.MorningB,
            3 => DaySlot.AfternoonA,
            4 => DaySlot.AfternoonB,
            5 => DaySlot.Evening,
            _ => DaySlot.MorningA
        };
    }
    public static string FormatHM(int minuteOfDay)
    {
        int h = minuteOfDay / 60;
        int m = minuteOfDay % 60;
        return $"{h:00}:{m:00}";
    }
}
