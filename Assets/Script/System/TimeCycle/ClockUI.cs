using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class ClockUI : MonoBehaviour
{
    [Header("TextClockUI")]
    [SerializeField] TextMeshProUGUI textTopDay;
    [SerializeField] TextMeshProUGUI textSession;
    [SerializeField] TextMeshProUGUI textSemester;
    [SerializeField] TextMeshProUGUI textClock;
    [SerializeField] Image progressFilled;

    [Header("IconDay (1 Image + 3 Sprites)")]
    [SerializeField] Image iconDayImage;
    [SerializeField] Sprite iconMorning;
    [SerializeField] Sprite iconAfternoon;
    [SerializeField] Sprite iconNight;

    [Header("Clock Warp Effect")]
    [Range(0.05f, 3f)] public float warpDuration = 0.8f;
    [Range(1f, 1.6f)] public float warpScale = 1.12f;
    public Color warpTint = new Color(1f, 0.95f, 0.7f, 1f);

    static readonly float[] FillBySession = { 1f, 0.8f, 0.6f, 0.4f, 0.2f };

    int minuteOfDay;
    int displayMinuteOfDay;
    int lastDisplayedMinuteBeforeWarp; // 🟩 thêm: giữ phút cũ trước khi đổi ca
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
        if (GameClock.Ins)
        {
            minuteOfDay = GameClock.Ins.MinuteOfDay;
            displayMinuteOfDay = minuteOfDay;
            lastDisplayedMinuteBeforeWarp = minuteOfDay; // 🟩 init
        }
        RefreshUI();
    }

    void OnDisable() => UnhookGameClockEvents();

    void Update()
    {
        TryHookGameClock();

        // 🟩 Ghi nhớ giá trị hiển thị cuối cùng khi không warp
        if (!clockWarping)
            lastDisplayedMinuteBeforeWarp = displayMinuteOfDay;

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
        GameClock.Ins.OnMinuteChanged += OnMinuteChanged;
        GameClock.Ins.OnSlotChanged += OnSlotChangedRefreshAndWarp;
        GameClock.Ins.OnDayChanged += RefreshUI;
        GameClock.Ins.OnWeekChanged += RefreshUI;
        GameClock.Ins.OnTermChanged += RefreshUI;
        GameClock.Ins.OnYearChanged += RefreshUI;
        hooked = true;
        RefreshUI();
    }

    void UnhookGameClockEvents()
    {
        if (!hooked || !GameClock.Ins) return;
        GameClock.Ins.OnMinuteChanged -= OnMinuteChanged;
        GameClock.Ins.OnSlotChanged -= OnSlotChangedRefreshAndWarp;
        GameClock.Ins.OnDayChanged -= RefreshUI;
        GameClock.Ins.OnWeekChanged -= RefreshUI;
        GameClock.Ins.OnTermChanged -= RefreshUI;
        GameClock.Ins.OnYearChanged -= RefreshUI;
        hooked = false;
    }

    void OnMinuteChanged(int newMinute)
    {
        minuteOfDay = newMinute;
        if (!clockWarping)
            displayMinuteOfDay = minuteOfDay;
    }

    void OnSlotChangedRefreshAndWarp()
    {
        RefreshUI();
        TriggerClockWarp();
    }

    void RefreshUI()
    {
        if (!GameClock.Ins) return;
        if (textTopDay) textTopDay.text = GameClock.WeekdayToVN(GameClock.Ins.Weekday);
        if (textSession) textSession.text = "Ca Học: " + GameClock.Ins.SlotIndex1Based;
        if (textSemester) textSemester.text = $"Học kì: {GameClock.Ins.Term}";
        UpdateIconsBySession(GameClock.Ins.SlotIndex1Based);
        UpdateProgressDiscrete();

        minuteOfDay = GameClock.Ins.MinuteOfDay;
        if (!clockWarping) displayMinuteOfDay = minuteOfDay;
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

        // 🟩 BẮT ĐẦU từ phút hiển thị cũ (dù đã qua 1–2 giây vẫn còn lưu)
        int startMin = lastDisplayedMinuteBeforeWarp;
        int endMin = GameClock.Ins ? GameClock.Ins.MinuteOfDay : startMin;

        const int MIN_PER_DAY = 24 * 60;
        int forwardDist = ((endMin - startMin) % MIN_PER_DAY + MIN_PER_DAY) % MIN_PER_DAY;

        int WrapLerpInt(float k)
        {
            float v = startMin + forwardDist * k;
            return Mathf.FloorToInt(v) % MIN_PER_DAY;
        }

        while (t < warpDuration)
        {
            float k = t / warpDuration;
            float ease = 1f - Mathf.Pow(1f - k, 3f);

            displayMinuteOfDay = WrapLerpInt(ease);

            float bounce = Mathf.Sin(ease * Mathf.PI);
            rt.localScale = baseScale * Mathf.Lerp(1f, warpScale, bounce);

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

        displayMinuteOfDay = endMin;
        rt.localScale = baseScale;
        textClock.color = baseColor;
        clockWarping = false;
    }

    // Convenience
    public void OnClickNextSlot() { if (GameClock.Ins) GameClock.Ins.NextSlot(); }
    public void JumpToNextSessionNow() { if (GameClock.Ins) GameClock.Ins.JumpToNextSessionStart(); }
}
