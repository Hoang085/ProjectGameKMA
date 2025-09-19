using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(10000)]
public class TimeSaveManager : Singleton<TimeSaveManager>
{
    public const string PREF_KEY = "Save_GameClockState";

    [System.Serializable]
    public struct ClockStateDTO
    {
        public int year;  // 1-based
        public int term;  // 1-based
        public int week;  // 1-based
        public int day;   // 1-based (1 = Monday)
        public int slot;  // DaySlot enum -> int
    }

    [Header("Options")]
    public bool loadOnStart = true;
    public bool autoSaveOnPause = true;
    public bool autoSaveOnQuit = true;

    // Lay cache gan nhat
    private ClockStateDTO _lastState;
    private bool _hasState;

    public override void Awake()
    {
        MakeSingleton(false);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        UnhookClock();
    }

    private void Start()
    {
        if (loadOnStart) TryLoad();
        HookClock();
        Capture(); // Lay cache dau tien neu co
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


    // Luu lai trang thai hien tai vao PlayerPrefs
    private void Capture()
    {
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c) return;

        _lastState = ToDTO(c);
        _hasState = true;
    }


    private void OnApplicationPause(bool pauseStatus)
    {
        if (autoSaveOnPause && pauseStatus) Save(); // Luu khi pause
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit) Save(); // Neu GameClock null thi dung cache
    }

    [ContextMenu("Save Now")]
    public void Save()
    {
        // Uu tien tu GameClock
        if (TrySaveFromClock()) return;

        // Game Clock null, dung cache neu co
        if (_hasState)
        {
            SaveDTO(_lastState);
            return;
        }

#if UNITY_EDITOR
        Debug.LogWarning("[TimeSaveManager] Khong co GameClock cung nhu chua co state de luu");
#endif
    }

    private bool TrySaveFromClock()
    {
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c) return false;

        var dto = ToDTO(c);
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

        //Neu luc load chua co GameClock, cache lai va cho scene gameplay
        var c = GameClock.Ins ?? FindFirstObjectByType<GameClock>(FindObjectsInactive.Include);
        if (!c)
        {
            _lastState = dto;
            _hasState = true;
            return;
        }

        c.SetTime(dto.year, dto.term, dto.week, dto.day, (DaySlot)dto.slot);
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
            slot = (int)c.Slot
        };
    }
}
