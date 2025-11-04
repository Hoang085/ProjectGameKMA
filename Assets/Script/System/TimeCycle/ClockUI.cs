using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class ClockUI : MonoBehaviour
{
    [Header("TextClockUI")]
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

    [Header("Clock Warp Effect")]
    [Tooltip("Thời lượng hiệu ứng lướt số khi đổi ca")]
    [Range(0.05f, 3f)] public float warpDuration = 0.8f;
    [Tooltip("Độ phóng tối đa của chữ đồng hồ")]
    [Range(1f, 1.6f)] public float warpScale = 1.12f;
    [Tooltip("Màu tint nhẹ trong lúc warp")]
    public Color warpTint = new Color(1f, 0.95f, 0.7f, 1f);

    static readonly float[] FillBySession = { 1f, 0.8f, 0.6f, 0.4f, 0.2f };

    // runtime state purely for UI
    int minuteOfDay;          // mirror từ GameClock.MinuteOfDay
    int displayMinuteOfDay;   // phút đang hiển thị (phục vụ warp)
    bool hooked;
    bool clockWarping;
    Color _clockBaseColor;
    Vector3 _clockBaseScale;

    void Awake()
    {
        if (textClock)
        {
            _clockBaseColor = textClock.color;
            _clockBaseScale = textClock.rectTransform.localScale;
        }
    }

    void OnEnable()
    {
        TryHookGameClock();
        // Đồng bộ giá trị ban đầu từ GameClock
        if (GameClock.Ins)
        {
            minuteOfDay = GameClock.Ins.MinuteOfDay;
            displayMinuteOfDay = minuteOfDay;
        }
        RefreshUI(); // paint lần đầu
    }

    void OnDisable()
    {
        UnhookGameClockEvents();
    }

    void Update()
    {
        // Đảm bảo đã hook khi GameClock xuất hiện muộn.
        TryHookGameClock();

        // Render textClock mỗi frame theo state hiển thị hiện tại
        if (textClock)
        {
            int minutesToShow = clockWarping ? displayMinuteOfDay : minuteOfDay;
            int hh = minutesToShow / 60;
            int mm = minutesToShow % 60;
            textClock.text = $"{hh:00}:{mm:00}";
        }
    }

    void TryHookGameClock()
    {
        if (hooked || !GameClock.Ins) return;

        // Dòng thời gian (data owner): GameClock phát event, UI update
        GameClock.Ins.OnMinuteChanged += OnMinuteChanged;               // cập nhật đồng hồ số
        GameClock.Ins.OnSlotChanged += OnSlotChangedRefreshAndWarp;   // đổi ca → warp
        GameClock.Ins.OnDayChanged += RefreshUI;
        GameClock.Ins.OnWeekChanged += RefreshUI;
        GameClock.Ins.OnTermChanged += RefreshUI;
        GameClock.Ins.OnYearChanged += RefreshUI;

        hooked = true;
        RefreshUI();
    }

    void UnhookGameClockEvents()
    {
        if (!hooked) return;
        if (GameClock.Ins)
        {
            GameClock.Ins.OnMinuteChanged -= OnMinuteChanged;
            GameClock.Ins.OnSlotChanged -= OnSlotChangedRefreshAndWarp;
            GameClock.Ins.OnDayChanged -= RefreshUI;
            GameClock.Ins.OnWeekChanged -= RefreshUI;
            GameClock.Ins.OnTermChanged -= RefreshUI;
            GameClock.Ins.OnYearChanged -= RefreshUI;
        }
        hooked = false;
    }

    void OnMinuteChanged(int newMinute)
    {
        minuteOfDay = newMinute;

        // Nếu không warp, cập nhật hiển thị ngay lập tức
        if (!clockWarping)
            displayMinuteOfDay = minuteOfDay;
    }

    void OnSlotChangedRefreshAndWarp()
    {
        RefreshUI();     // cập nhật textTopDay, textSession, icon, progress (rời rạc theo ca)
        TriggerClockWarp();
    }

    void RefreshUI()
    {
        if (!GameClock.Ins) return;

        // Day name (VN)
        if (textTopDay) textTopDay.text = GameClock.WeekdayToVN(GameClock.Ins.Weekday);

        // Session text
        if (textSession) textSession.text = "Ca Học: " + GameClock.Ins.SlotIndex1Based;

        // Semester text 
        if (textSemester) textSemester.text = $"Học kì: {GameClock.Ins.Term}";

        // Icon theo ca
        UpdateIconsBySession(GameClock.Ins.SlotIndex1Based);

        // Thanh tiến trình rời rạc theo ca
        UpdateProgressDiscrete();

        // Đồng bộ minute hiển thị nếu GameClock mới khởi phát
        minuteOfDay = GameClock.Ins.MinuteOfDay;
        if (!clockWarping) displayMinuteOfDay = minuteOfDay;
    }

    void UpdateIconsBySession(int sessionIdx)
    {
        if (!iconDayImage) return;

        // Quy ước: 1-2 sáng, 3-4 chiều, 5 tối
        if (sessionIdx == 1 || sessionIdx == 2) iconDayImage.sprite = iconMorning;
        else if (sessionIdx == 3 || sessionIdx == 4) iconDayImage.sprite = iconAfternoon;
        else iconDayImage.sprite = iconNight;
    }

    void UpdateProgressDiscrete()
    {
        if (!progressFilled || !GameClock.Ins) return;
        int s = Mathf.Clamp(GameClock.Ins.SlotIndex1Based, 1, 5);
        progressFilled.fillAmount = FillBySession[s - 1];
    }

    void TriggerClockWarp()
    {
        if (!textClock) return;
        StopCoroutine(nameof(CoClockWarp));
        StartCoroutine(nameof(CoClockWarp));
    }

    IEnumerator CoClockWarp()
    {
        clockWarping = true;

        var rt = textClock.rectTransform;
        var baseScale = _clockBaseScale;
        var baseColor = _clockBaseColor;

        float t = 0f;
        int startMin = displayMinuteOfDay;
        int endMin = GameClock.Ins ? GameClock.Ins.MinuteOfDay : startMin;

        const int MIN_PER_DAY = 24 * 60;

        // chọn quãng cuộn "tiến về phía trước"
        int forwardDist = ((endMin - startMin) % MIN_PER_DAY + MIN_PER_DAY) % MIN_PER_DAY;

        int WrapLerpInt(int from, int to, float k)
        {
            float v = from + forwardDist * k;
            int mi = Mathf.FloorToInt(v) % MIN_PER_DAY;
            return mi;
        }

        while (t < warpDuration)
        {
            float k = t / warpDuration;
            // easeOutCubic
            float ease = 1f - Mathf.Pow(1f - k, 3f);

            // lăn số phút hiển thị tiến dần đến endMin
            displayMinuteOfDay = WrapLerpInt(startMin, endMin, ease);

            // scale bounce
            float bounce = Mathf.Sin(ease * Mathf.PI); // 0→1→0
            float scale = Mathf.Lerp(1f, warpScale, bounce);
            rt.localScale = baseScale * scale;

            // tint nhẹ
            Color mid = Color.Lerp(baseColor, warpTint, 0.6f);
            Color cur = Color.Lerp(
                Color.Lerp(baseColor, mid, Mathf.Clamp01(ease * 2f)),
                Color.Lerp(mid, baseColor, Mathf.Clamp01((ease - 0.5f) * 2f)),
                0.5f
            );
            cur.a = baseColor.a;
            textClock.color = cur;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // chốt
        displayMinuteOfDay = GameClock.Ins ? GameClock.Ins.MinuteOfDay : displayMinuteOfDay;
        if (textClock)
        {
            rt.localScale = baseScale;
            textClock.color = baseColor;
        }
        clockWarping = false;
    }

    // ===== Convenience wrappers (UI gọi vào GameClock — tùy dùng) =====
    public void OnClickNextSlot() { if (GameClock.Ins) GameClock.Ins.NextSlot(); }
    public void JumpToNextSessionNow() { if (GameClock.Ins) GameClock.Ins.JumpToNextSessionStart(); }
    public int GetMinuteOfDay() { return GameClock.Ins ? GameClock.Ins.MinuteOfDay : minuteOfDay; }
    public void SetMinuteOfDay(int minute, bool syncGameClock = true)
    {
        if (!GameClock.Ins) return;
        GameClock.Ins.SetMinuteOfDay(minute, syncSlot: syncGameClock);
        // Sự kiện OnMinuteChanged sẽ đồng bộ lại UI
    }
}
