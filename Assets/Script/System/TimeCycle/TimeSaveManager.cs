using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(10000)]
public class TimeSaveManager : Singleton<TimeSaveManager>
{
    public const string PREF_KEY = "Save_GameClockState";

    [System.Serializable]
    public struct ClockStateDTO
    {
        public int year;   // 1-based
        public int term;   // 1-based
        public int week;   // 1-based
        public int day;    // 1-based (1 = Monday)
        public int slot;   // DaySlot enum -> int
        public int minuteOfDay; // NEW: 0..1439, thời gian trong ngày của ClockUI
    }

    [Header("Options")]
    public bool loadOnStart = true;
    public bool autoSaveOnPause = true;
    public bool autoSaveOnQuit = true;
    [Tooltip("Khi load, đồng bộ GameClock slot theo minuteOfDay (nếu lệch).")]
    public bool syncSlotWithMinuteOnLoad = true;

    // cache gần nhất
    private ClockStateDTO _lastState;
    private bool _hasState;

    public override void Awake() { MakeSingleton(false); }

    private void OnEnable() { SceneManager.activeSceneChanged += OnSceneChanged; }
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        UnhookClock();
    }

    private void Start()
    {
        if (loadOnStart) TryLoad();
        HookClock();
        Capture(); // cache lần đầu
    }

    private void OnSceneChanged(Scene _, Scene __)
    {
        UnhookClock();
        HookClock();
        Capture();
    }

    private void HookClock()
    {
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c) return;

        c.OnSlotChanged += Capture;
        c.OnDayChanged += Capture;
        c.OnWeekChanged += Capture;
        c.OnTermChanged += Capture;
        c.OnYearChanged += Capture;
    }

    private void UnhookClock()
    {
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c) return;

        c.OnSlotChanged -= Capture;
        c.OnDayChanged -= Capture;
        c.OnWeekChanged -= Capture;
        c.OnTermChanged -= Capture;
        c.OnYearChanged -= Capture;
    }

    // ==== Lấy trạng thái hiện tại (kèm minute từ GameClock) ====
    private void Capture()
    {
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c) return;

        var dto = ToDTO(c);
        // Lấy minuteOfDay trực tiếp từ GameClock (source of truth)
        dto.minuteOfDay = c.MinuteOfDay;

        _lastState = dto;
        _hasState = true;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (autoSaveOnPause && pauseStatus) Save();
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit) Save();
    }

    [ContextMenu("Save Now")]
    public void Save()
    {
        // Ưu tiên lấy mới từ scene hiện tại
        if (TrySaveFromScene()) return;

        // Không có scene hợp lệ → lưu cache nếu có
        if (_hasState)
        {
            SaveDTO(_lastState);
            return;
        }

#if UNITY_EDITOR
        Debug.LogWarning("[TimeSaveManager] Khong co GameClock/ClockUI va chua co cache de luu.");
#endif
    }

    private bool TrySaveFromScene()
    {
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c) return false;

        var dto = ToDTO(c);

        // Lấy minuteOfDay trực tiếp từ GameClock
        dto.minuteOfDay = c.MinuteOfDay;

        SaveDTO(dto);

        _lastState = dto;
        _hasState = true;
        return true;
    }

    private static void SaveDTO(ClockStateDTO dto)
    {
        var json = JsonUtility.ToJson(dto);
        PlayerPrefs.SetString(PREF_KEY, json);
        PlayerPrefs.Save();
#if UNITY_EDITOR
        Debug.Log($"[TimeSaveManager] Saved -> {json}");
#endif
    }

    [ContextMenu("Load Now")]
    public void TryLoad()
    {
        if (!PlayerPrefs.HasKey(PREF_KEY)) return;
        var json = PlayerPrefs.GetString(PREF_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        var dto = JsonUtility.FromJson<ClockStateDTO>(json);

        // Nếu chưa có GameClock, cache lại đợi scene gameplay
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c)
        {
            _lastState = dto;
            _hasState = true;
            return;
        }

        // 1) Khôi phục GameClock (bao gồm cả minuteOfDay)
        c.SetTime(dto.year, dto.term, dto.week, dto.day, (DaySlot)dto.slot);
        
        // 2) Khôi phục minuteOfDay nếu có dữ liệu hợp lệ
        if (dto.minuteOfDay >= 0)
        {
            c.SetMinuteOfDay(dto.minuteOfDay, syncSlotWithMinuteOnLoad);
        }

        _lastState = dto;
        _hasState = true;
#if UNITY_EDITOR
        Debug.Log($"[TimeSaveManager] Loaded <- {json}");
#endif
    }

    [ContextMenu("Clear Save")]
    public void ClearSave()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
#if UNITY_EDITOR
        Debug.Log("[TimeSaveManager] Cleared.");
#endif
    }

    private static ClockStateDTO ToDTO(GameClock c)
    {
        return new ClockStateDTO
        {
            year = c.Year,
            term = c.Term,
            week = c.Week,
            day = c.DayIndex,
            slot = (int)c.Slot,
            minuteOfDay = c.MinuteOfDay // Lấy trực tiếp từ GameClock
        };
    }
}
