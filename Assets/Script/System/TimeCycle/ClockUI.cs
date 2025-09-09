using UnityEngine;
using UnityEngine.UI;

public class ClockUI : MonoBehaviour
{
    [Header("Labels (UI Text)")]
    [SerializeField] private Text dayText;       // ShowTextTime/TextDayOfTheWeek
    [SerializeField] private Text sessionText;   // ShowTextTime/TextSession
    [SerializeField] private Text semesterText;  // ShowTextTime/TextSemester
    [SerializeField] private Text weekText;      // optional

    [Header("Icons (optional)")]
    [SerializeField] private Image timeOfDayIcon; // ImageTimeOfDay
    [SerializeField] private Sprite morningIcon, afternoonIcon, nightIcon;

    bool _hooked;

    private void Awake()
    {
        // Auto-find theo hierarchy bạn gửi (nếu quên kéo)
        if (!dayText) dayText = transform.Find("ShowTextTime/TextDayOfTheWeek")?.GetComponent<Text>();
        if (!sessionText) sessionText = transform.Find("ShowTextTime/TextSession")?.GetComponent<Text>();
        if (!semesterText) semesterText = transform.Find("ShowTextTime/TextSemester")?.GetComponent<Text>();
        if (!timeOfDayIcon) timeOfDayIcon = transform.Find("ImageTimeOfDay")?.GetComponent<Image>();
    }

    private void OnEnable() { TryHook(); Refresh(); }
    private void OnDisable() { Unhook(); }

    private void Update()
    {
        // Nếu lúc bật lên GameClock chưa sẵn sàng, mỗi frame thử hook 1 lần
        if (!_hooked) TryHook();

        // Phím test
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (GameClock.I != null)
            {
                GameClock.I.NextSlot();
            }
            else
            {
                //Debug.LogWarning("[ClockUI] N pressed nhưng GameClock.I == null");
            }
        }
    }

    void TryHook()
    {
        if (_hooked || GameClock.I == null) return;

        GameClock.I.OnSlotChanged += Refresh;
        GameClock.I.OnDayChanged += Refresh;
        GameClock.I.OnWeekChanged += Refresh;
        GameClock.I.OnTermChanged += Refresh;
        GameClock.I.OnYearChanged += Refresh;

        _hooked = true;
        Debug.Log("[ClockUI] Hooked GameClock events");
        Refresh();
    }

    void Unhook()
    {
        if (!_hooked || GameClock.I == null) return;

        GameClock.I.OnSlotChanged -= Refresh;
        GameClock.I.OnDayChanged -= Refresh;
        GameClock.I.OnWeekChanged -= Refresh;
        GameClock.I.OnTermChanged -= Refresh;
        GameClock.I.OnYearChanged -= Refresh;

        _hooked = false;
    }

    public void Refresh()
    {
        if (GameClock.I == null) return;

        if (dayText) dayText.text = GameClock.WeekdayToEN(GameClock.I.Weekday);
        if (sessionText) sessionText.text = "Session: " + GameClock.I.GetSlotIndex1Based();
        if (semesterText) semesterText.text = "Semester: " + GameClock.I.Term;
        if (weekText) weekText.text = "Week: " + GameClock.I.Week;

        if (timeOfDayIcon)
        {
            var s = GameClock.I.Slot;
            if (s == DaySlot.Evening && nightIcon) timeOfDayIcon.sprite = nightIcon;
            else if (s == DaySlot.MorningA || s == DaySlot.MorningB) timeOfDayIcon.sprite = morningIcon;
            else timeOfDayIcon.sprite = afternoonIcon;
        }
    }

    // Gán vào Button → OnClick nếu có
    public void OnClickNextSlot()
    {
        if (GameClock.I == null) { Debug.LogWarning("[ClockUI] NextSlot clicked nhưng GameClock.I == null"); return; }
        Debug.Log("[ClockUI] Button Next → NextSlot()");
        GameClock.I.NextSlot();
    }
}
