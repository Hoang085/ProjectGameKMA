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
        // Auto-find theo hierarchy 
        if (!dayText) dayText = transform.Find("ShowTextTime/TextDayOfTheWeek")?.GetComponent<Text>();
        if (!sessionText) sessionText = transform.Find("ShowTextTime/TextSession")?.GetComponent<Text>();
        if (!semesterText) semesterText = transform.Find("ShowTextTime/TextSemester")?.GetComponent<Text>();
        if (!timeOfDayIcon) timeOfDayIcon = transform.Find("ImageTimeOfDay")?.GetComponent<Image>();
    }

    private void OnEnable() { TryHook(); Refresh(); }
    private void OnDisable() { Unhook(); }

    private void Update()
    {
        // Neu luc bat len GameClock chua san sang, moi frame thu hook 1 lan
        if (!_hooked) TryHook();

        // Test bang phim N
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (GameClock.Ins != null)
            {
                GameClock.Ins.NextSlot();
            }
            else
            {
                Debug.LogWarning("[ClockUI] N pressed but GameClock.I == null");
            }
        }
    }

    void TryHook() // Chi hook 1 lan
    {
        if (_hooked || GameClock.Ins == null) return;

        GameClock.Ins.OnSlotChanged += Refresh;
        GameClock.Ins.OnDayChanged += Refresh;
        GameClock.Ins.OnWeekChanged += Refresh;
        GameClock.Ins.OnTermChanged += Refresh;
        GameClock.Ins.OnYearChanged += Refresh;

        _hooked = true;
        Debug.Log("[ClockUI] Hooked GameClock events");
        Refresh();
    }

    void Unhook() // Chi unhook 1 lan
    {
        if (!_hooked || GameClock.Ins == null) return;

        GameClock.Ins.OnSlotChanged -= Refresh;
        GameClock.Ins.OnDayChanged -= Refresh;
        GameClock.Ins.OnWeekChanged -= Refresh;
        GameClock.Ins.OnTermChanged -= Refresh;
        GameClock.Ins.OnYearChanged -= Refresh;

        _hooked = false;
    }

    public void Refresh() // Cap nhat UI
    {
        if (GameClock.Ins == null) return;

        if (dayText) dayText.text = GameClock.WeekdayToEN(GameClock.Ins.Weekday);
        if (sessionText) sessionText.text = "Session: " + GameClock.Ins.GetSlotIndex1Based();
        if (semesterText) semesterText.text = "Semester: " + GameClock.Ins.Term;
        if (weekText) weekText.text = "Week: " + GameClock.Ins.Week;

        if (timeOfDayIcon)
        {
            var s = GameClock.Ins.Slot;
            if (s == DaySlot.Evening && nightIcon) timeOfDayIcon.sprite = nightIcon;
            else if (s == DaySlot.MorningA || s == DaySlot.MorningB) timeOfDayIcon.sprite = morningIcon;
            else timeOfDayIcon.sprite = afternoonIcon;
        }
    }

    // Gan vao button Next
    public void OnClickNextSlot()
    {
        if (GameClock.Ins == null) { Debug.LogWarning("[ClockUI] NextSlot clicked nhưng GameClock.I == null"); return; }
        Debug.Log("[ClockUI] Button Next → NextSlot()");
        GameClock.Ins.NextSlot();
    }
}
