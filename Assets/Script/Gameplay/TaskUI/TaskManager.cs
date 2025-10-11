using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý tasks độc lập khỏi UI - tự động spawn khi đúng thời gian
/// </summary>
public class TaskManager : MonoBehaviour
{
    [Header("Task System Config")]
    private bool enableAutoTaskSpawn = true;
    [Min(0)] private int showTaskMinutesEarly = 5;

    [Header("Subject Display")]
    private string jsonPathInResources = "TextSubjectDisplay/subjectname";
    private bool normalizeKeyOnLoad = false;

    // Task storage - không phụ thuộc UI
    private readonly Dictionary<string, TaskData> activeTasks = new();
    private readonly Dictionary<string, string> subjectDisplayMap = new();

    // System references
    private GameClock clock;
    private AttendanceManager attendanceManager;

    // Notification tracking - FIXED: Initialize to null to force initial update
    private bool? hasActiveTasksLastFrame = null;

    public static TaskManager Instance { get; private set; }

    [System.Serializable]
    public class TaskData
    {
        public string key;
        public string title;
        public string detail;
        public string buttonText;
        public string searchData;
        public DateTime createdTime;
        public bool isValid = true;

        public TaskData(string key, string title, string detail, string buttonText, string searchData)
        {
            this.key = key;
            this.title = title;
            this.detail = detail;
            this.buttonText = buttonText;
            this.searchData = searchData;
            this.createdTime = DateTime.Now;
        }
    }

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        LoadSubjectNamesFromJson();
        SubscribeToEvents();

        // FIXED: Delay initialization to ensure GameManager is ready
        StartCoroutine(DelayedInitialization());
    }

    // FIXED: Add delayed notification sync
    private IEnumerator DelayedInitialization()
    {
        // Wait a few frames to ensure all managers are initialized
        yield return null;
        yield return null;

        // Initialize tasks
        if (enableAutoTaskSpawn)
        {
            RefreshTasks();
        }

        // FIXED: Initialize notification state properly
        bool currentlyHasTasks = activeTasks.Count > 0;
        hasActiveTasksLastFrame = currentlyHasTasks;

        // FIXED: Force sync with GameManager
        if (GameManager.Ins != null)
        {
            GameManager.Ins.SetIconNotification(IconType.Task, currentlyHasTasks);
            Debug.Log($"[TaskManager] Initial sync completed: {(currentlyHasTasks ? "SHOW" : "HIDE")} ({activeTasks.Count} tasks)");
        }

        Debug.Log($"[TaskManager] Delayed initialization completed with {activeTasks.Count} tasks");
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void InitializeSystem()
    {
        clock = GameClock.Ins;
        attendanceManager = FindAnyObjectByType<AttendanceManager>();

        Debug.Log("[TaskManager] System initialized");
    }

    void LoadSubjectNamesFromJson()
    {
        if (string.IsNullOrEmpty(jsonPathInResources)) return;

        var textAsset = Resources.Load<TextAsset>(jsonPathInResources);
        if (!textAsset) return;

        try
        {
            var list = JsonUtility.FromJson<TaskPlayerUI.SubjectNameList>(textAsset.text);
            if (list?.items == null) return;

            subjectDisplayMap.Clear();
            foreach (var item in list.items)
            {
                if (item == null || string.IsNullOrEmpty(item.key) || string.IsNullOrEmpty(item.display))
                    continue;

                var key = normalizeKeyOnLoad ? NormalizeKey(item.key) : item.key;
                subjectDisplayMap[key] = item.display;
            }

            Debug.Log($"[TaskManager] Loaded {subjectDisplayMap.Count} subject display names");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TaskManager] Error loading subject names: {ex.Message}");
        }
    }

    void SubscribeToEvents()
    {
        if (clock != null)
        {
            clock.OnSlotStarted += OnSlotStarted;
            clock.OnSlotEnded += OnSlotEnded;
        }

        // Subscribe to minute changes for real-time task updates
        if (enableAutoTaskSpawn)
        {
            InvokeRepeating(nameof(RefreshTasks), 1f, 30f); // Check every 30 seconds
        }
    }

    void UnsubscribeFromEvents()
    {
        if (clock != null)
        {
            clock.OnSlotStarted -= OnSlotStarted;
            clock.OnSlotEnded -= OnSlotEnded;
        }

        CancelInvoke(nameof(RefreshTasks));
    }

    void OnSlotStarted(int week, string dayEN, int slot1)
    {
        if (enableAutoTaskSpawn)
        {
            RefreshTasks();
        }
    }

    void OnSlotEnded(int week, string dayEN, int slot1)
    {
        ClearAllTasks();
        UpdateTaskNotificationState();
    }

    /// <summary>
    /// Refresh tasks - được gọi tự động theo thời gian
    /// </summary>
    public void RefreshTasks()
    {
        if (!IsValidState())
        {
            ClearAllTasks();
            UpdateTaskNotificationState();
            return;
        }

        var currentSubject = GetCurrentSubject();
        if (currentSubject == null)
        {
            ClearAllTasks();
            UpdateTaskNotificationState();
            return;
        }

        ProcessSubjectTask(currentSubject);
        UpdateTaskNotificationState();
    }

    bool IsValidState() => clock != null && attendanceManager != null && enableAutoTaskSpawn;

    SubjectData GetCurrentSubject()
    {
        try
        {
            int semIndex = Mathf.Clamp(clock.Term - 1, 0, attendanceManager.semesterConfigs.Length - 1);
            var semester = attendanceManager.semesterConfigs[semIndex];
            return SemesterConfigUtil.instance.GetSubjectAt(semester, clock.Weekday, clock.GetSlotIndex1Based());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TaskManager] Error getting current subject: {ex.Message}");
            return null;
        }
    }

    void ProcessSubjectTask(SubjectData subject)
    {
        string subjectKey = KeyUtil.MakeKey(subject.Name);
        string subjectDisplayName = GetDisplayName(subjectKey);
        string taskKey = $"study:{subjectKey}";

        if (ShouldShowTask(subjectDisplayName, out int windowStart, out int windowEnd))
            CreateStudyTask(taskKey, subjectDisplayName, subjectKey, windowStart);
        else
            RemoveTask(taskKey);
    }

    bool ShouldShowTask(string subjectName, out int windowStart, out int windowEnd)
    {
        windowStart = windowEnd = 0;
        bool canCheckIn = attendanceManager.CanCheckInNow(subjectName, out windowStart, out windowEnd);
        if (canCheckIn) return true;
        return ShouldShowEarlyNotification(out windowStart, out windowEnd);
    }

    bool ShouldShowEarlyNotification(out int windowStart, out int windowEnd)
    {
        windowStart = windowEnd = 0;

        var clockUI = FindAnyObjectByType<ClockUI>();
        if (clockUI == null || attendanceManager.slotPolicy == null) return false;

        int currentMinute = clockUI.GetMinuteOfDay();
        int slotStart = attendanceManager.GetSlotStart(clock.Slot);

        if (attendanceManager.slotPolicy.TryGetWindow(clock.Slot, slotStart, out int winStart, out int winEnd))
        {
            int earlyStartTime = winStart - showTaskMinutesEarly;
            bool shouldShow = (currentMinute >= earlyStartTime) && (currentMinute < winEnd);
            if (shouldShow)
            {
                windowStart = winStart;
                windowEnd = winEnd;
                return true;
            }
        }
        return false;
    }

    void CreateStudyTask(string taskKey, string subjectDisplayName, string subjectKey, int windowStart)
    {
        string timeStr = ClockUI.FormatHM(windowStart);
        string slotName = GetSlotDisplayName(clock.Slot);

        string title = $"Sắp đến giờ học môn {subjectDisplayName}";
        string detail = $"Bắt đầu từ {timeStr} - {slotName}";
        string buttonText = "Đi học";
        string searchData = $"{subjectDisplayName}|{subjectKey}";

        CreateOrUpdateTask(taskKey, title, detail, buttonText, searchData);
    }

    void CreateOrUpdateTask(string key, string title, string detail, string btnText, string searchData)
    {
        bool isNewTask = !activeTasks.ContainsKey(key);

        if (isNewTask)
        {
            activeTasks[key] = new TaskData(key, title, detail, btnText, searchData);

            Debug.Log($"[TaskManager] Created new task: {title}");
        }
        else
        {
            // Update existing task
            var task = activeTasks[key];
            task.title = title;
            task.detail = detail;
            task.buttonText = btnText;
            task.searchData = searchData;
        }
    }

    void RemoveTask(string key)
    {
        if (activeTasks.Remove(key))
        {
            Debug.Log($"[TaskManager] Removed task: {key}");
        }
    }

    void ClearAllTasks()
    {
        if (activeTasks.Count > 0)
        {
            int count = activeTasks.Count;
            activeTasks.Clear();
            Debug.Log($"[TaskManager] Cleared {count} tasks");
        }
    }

    // FIXED: Improved notification state management
    void UpdateTaskNotificationState()
    {
        bool hasActiveTasks = activeTasks.Count > 0;

        // FIXED: Update notification every time during first few seconds, then only on change
        bool shouldUpdate = hasActiveTasksLastFrame == null ||
                           hasActiveTasks != hasActiveTasksLastFrame.Value ||
                           Time.time < 5f; // Force updates for first 5 seconds

        if (shouldUpdate)
        {
            hasActiveTasksLastFrame = hasActiveTasks;

            if (GameManager.Ins != null)
            {
                GameManager.Ins.SetIconNotification(IconType.Task, hasActiveTasks);
                Debug.Log($"[TaskManager] Task notification updated: {(hasActiveTasks ? "SHOW" : "HIDE")} (Active tasks: {activeTasks.Count})");
            }
            else
            {
                Debug.LogWarning("[TaskManager] GameManager not available for notification sync");
            }
        }
    }

    // FIXED: Add method to reset notification state (useful for testing)
    [ContextMenu("Reset Task Notification State")]
    public void ResetTaskNotificationState()
    {
        hasActiveTasksLastFrame = null;
        ForceUpdateTaskNotificationState();
        Debug.Log("[TaskManager] Task notification state reset and synced");
    }

    // FIXED: Add method to force notification update
    /// <summary>
    /// Force update task notification state regardless of previous state
    /// </summary>
    private void ForceUpdateTaskNotificationState()
    {
        bool hasActiveTasks = activeTasks.Count > 0;
        hasActiveTasksLastFrame = hasActiveTasks;

        if (GameManager.Ins != null)
        {
            GameManager.Ins.SetIconNotification(IconType.Task, hasActiveTasks);
            Debug.Log($"[TaskManager] Task notification FORCE updated: {(hasActiveTasks ? "SHOW" : "HIDE")} (Active tasks: {activeTasks.Count})");
        }
    }

    // === Public API ===
    public Dictionary<string, TaskData> GetActiveTasks()
    {
        return new Dictionary<string, TaskData>(activeTasks);
    }

    public bool HasPendingTasks()
    {
        return activeTasks.Count > 0;
    }

    public int GetActiveTaskCount()
    {
        return activeTasks.Count;
    }

    public TaskData GetTask(string key)
    {
        return activeTasks.TryGetValue(key, out var task) ? task : null;
    }

    // === Navigation Handling ===
    public void HandleTaskAction(string searchData)
    {
        if (string.IsNullOrEmpty(searchData)) return;

        string[] parts = searchData.Split('|');
        string displayName = parts[0];
        string subjectKey = parts.Length > 1 ? parts[1] : displayName;

        var targetTeacher = FindTeacherForSubject(displayName, subjectKey);
        if (targetTeacher != null)
        {
            NavigateToTeacher(targetTeacher, displayName);
        }
        else
        {
            Debug.LogWarning($"[TaskManager] Teacher not found for subject '{displayName}'");
        }
    }

    TeacherAction FindTeacherForSubject(string displayName, string subjectKey)
    {
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        foreach (var teacher in teachers)
        {
            if (teacher.subjects == null) continue;
            foreach (var subject in teacher.subjects)
            {
                if (subject == null) continue;
                if (IsSubjectMatch(subject.subjectName, displayName, subjectKey))
                    return teacher;
            }
        }
        return null;
    }

    bool IsSubjectMatch(string teacherSubjectName, string displayName, string subjectKey)
    {
        if (string.IsNullOrEmpty(teacherSubjectName)) return false;

        if (string.Equals(teacherSubjectName, displayName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(teacherSubjectName, subjectKey, StringComparison.OrdinalIgnoreCase))
            return true;

        string nTeacher = NormalizeKey(teacherSubjectName);
        string nDisplay = NormalizeKey(displayName);
        string nKey = NormalizeKey(subjectKey);

        return string.Equals(nTeacher, nDisplay, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(nTeacher, nKey, StringComparison.OrdinalIgnoreCase);
    }

    void NavigateToTeacher(TeacherAction teacher, string subjectName)
    {
        if (NavigationLineManager.Instance != null)
            NavigationLineManager.Instance.CreateNavigationLine(teacher.transform, $"GV {teacher.name} - {subjectName}");

        // Close Task UI if open
        if (GameUIManager.Ins != null && GameUIManager.Ins.IsAnyStatUIOpen)
        {
            GameUIManager.Ins.CloseAllUIs();
        }

        Debug.Log($"[TaskManager] Navigation started to teacher {teacher.name} for subject {subjectName}");
    }

    // === Helper Methods ===
    string NormalizeKey(string key) => key.Replace(" ", "").Replace("_", "").ToLowerInvariant();

    string GetDisplayName(string keyOrFallback)
    {
        if (string.IsNullOrEmpty(keyOrFallback)) return keyOrFallback;
        var key = normalizeKeyOnLoad ? NormalizeKey(keyOrFallback) : keyOrFallback;
        if (subjectDisplayMap.TryGetValue(key, out var display)) return display;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
               .ToTitleCase(key.Replace('_', ' ').ToLowerInvariant());
    }

    string GetSlotDisplayName(DaySlot slot) => slot switch
    {
        DaySlot.MorningA => "Buổi sáng - Ca 1",
        DaySlot.MorningB => "Buổi sáng - Ca 2",
        DaySlot.AfternoonA => "Buổi chiều - Ca 3",
        DaySlot.AfternoonB => "Buổi chiều - Ca 4",
        DaySlot.Evening => "Buổi tối - Ca 5",
        _ => slot.ToString()
    };

    // === Editor Methods ===
    [ContextMenu("Force Refresh Tasks")]
    public void ForceRefreshTasks()
    {
        RefreshTasks();
    }

    [ContextMenu("Clear All Tasks")]
    public void ForceClearAllTasks()
    {
        ClearAllTasks();
        ForceUpdateTaskNotificationState(); // FIXED: Use force update method
    }

    // FIXED: Add public method to force notification refresh
    [ContextMenu("Force Refresh Notification")]
    public void ForceRefreshNotification()
    {
        ForceUpdateTaskNotificationState();
    }
}