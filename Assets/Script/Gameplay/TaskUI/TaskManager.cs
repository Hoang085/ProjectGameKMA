using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý tasks độc lập khỏi UI - tự động spawn khi đúng thời gian.
/// Đã cập nhật: chỉ hoạt động trong học kỳ hiện tại, tự clear khi chuyển kỳ.
/// </summary>
public class TaskManager : MonoBehaviour
{
    [Header("Task System Config")]
    [SerializeField] private bool enableAutoTaskSpawn = true;
    [Min(0)][SerializeField] private int showTaskMinutesEarly = 5;

    [Header("Subject Display")]
    [SerializeField] private string jsonPathInResources = "TextSubjectDisplay/subjectname";
    [SerializeField] private bool normalizeKeyOnLoad = false;

    // Task storage
    private readonly Dictionary<string, TaskData> activeTasks = new();
    private readonly Dictionary<string, string> subjectDisplayMap = new();

    // System references
    private GameClock clock;
    private AttendanceManager attendanceManager;
    private Coroutine _waitSyncRoutine;

    // Notification tracking
    private bool? hasActiveTasksLastFrame = null;

    // Term tracking
    private int lastKnownTerm = -1;

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

    // ============================================================
    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSystem();
        }
        else Destroy(gameObject);
    }

    private void Start()
    {
        LoadSubjectNamesFromJson();
        SubscribeToEvents();
        StartCoroutine(DelayedInitialization());
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        if (Instance == this)
            Instance = null;
    }

    #endregion
    // ============================================================

    private void InitializeSystem()
    {
        clock = GameClock.Ins; // BẮT BUỘC có
        attendanceManager = FindAnyObjectByType<AttendanceManager>();
        lastKnownTerm = clock.Term;
        Debug.Log("[TaskManager] System initialized.");
    }

    private IEnumerator DelayedInitialization()
    {
        yield return null;
        yield return null;

        if (enableAutoTaskSpawn)
            RefreshTasks();

        bool hasTasks = activeTasks.Count > 0;
        hasActiveTasksLastFrame = hasTasks;

        if (GameManager.Ins != null)
            GameManager.Ins.SetIconNotification(IconType.Task, hasTasks);

        Debug.Log($"[TaskManager] Delayed init complete. Active tasks: {activeTasks.Count}");
    }

    private void SubscribeToEvents()
    {
        clock.OnSlotStarted += OnSlotStarted;
        clock.OnSlotEnded += OnSlotEnded;
        clock.OnTermChanged += OnTermChanged;

        if (enableAutoTaskSpawn)
            InvokeRepeating(nameof(RefreshTasks), 1f, 30f);
    }

    private void UnsubscribeFromEvents()
    {
        if (clock != null)
        {
            clock.OnSlotStarted -= OnSlotStarted;
            clock.OnSlotEnded -= OnSlotEnded;
            clock.OnTermChanged -= OnTermChanged;
        }

        CancelInvoke(nameof(RefreshTasks));
    }

    // ============================================================
    #region GameClock Events

    private void OnSlotStarted(int week, string dayEN, int slot1)
    {
        if (enableAutoTaskSpawn)
            RefreshTasks();
    }

    private void OnSlotEnded(int week, string dayEN, int slot1)
    {
        ClearAllTasks();
        UpdateTaskNotificationState();
    }

    private void OnTermChanged()
    {
        lastKnownTerm = clock.Term;

        ClearAllTasks();
        UpdateTaskNotificationState();

        if (enableAutoTaskSpawn)
            RefreshTasks();

        Debug.Log($"[TaskManager] Term changed → T{lastKnownTerm}. Cleared old tasks and refreshed.");
    }

    #endregion
    // ============================================================

    #region Core Logic

    public void RefreshTasks()
    {
        if (!IsValidState())
        {
            ClearAllTasks();
            UpdateTaskNotificationState();
            return;
        }

        // Khi kỳ đổi, dọn sạch task kỳ cũ
        if (lastKnownTerm != clock.Term)
        {
            lastKnownTerm = clock.Term;
            ClearAllTasks();
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

    private bool IsValidState() =>
        clock != null && attendanceManager != null && enableAutoTaskSpawn;

    private SubjectData GetCurrentSubject()
    {
        try
        {
            int semIndex = Mathf.Clamp(clock.Term - 1, 0, attendanceManager.semesterConfigs.Length - 1);
            var semester = attendanceManager.semesterConfigs[semIndex];
            return SemesterConfigUtil.instance.GetSubjectAt(semester, clock.Weekday, clock.SlotIndex1Based);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TaskManager] Error getting current subject: {ex.Message}");
            return null;
        }
    }

    private void ProcessSubjectTask(SubjectData subject)
    {
        string subjectKey = KeyUtil.MakeKey(subject.Name);
        string subjectDisplayName = GetDisplayName(subjectKey);
        string taskKey = $"study:{subjectKey}";

        if (ShouldShowTask(subjectDisplayName, out int windowStart, out int windowEnd))
            CreateStudyTask(taskKey, subjectDisplayName, subjectKey, windowStart);
        else
            RemoveTask(taskKey);
    }

    private bool ShouldShowTask(string subjectName, out int windowStart, out int windowEnd)
    {
        windowStart = windowEnd = 0;
        bool canCheckIn = attendanceManager.CanCheckInNow(subjectName, out windowStart, out windowEnd);
        if (canCheckIn) return true;
        return ShouldShowEarlyNotification(out windowStart, out windowEnd);
    }

    private bool ShouldShowEarlyNotification(out int windowStart, out int windowEnd)
    {
        windowStart = windowEnd = 0;

        if (attendanceManager.slotPolicy == null) return false;

        int currentMinute = clock.MinuteOfDay;
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

    private void CreateStudyTask(string taskKey, string subjectDisplayName, string subjectKey, int windowStart)
    {
        string timeStr = GameClock.FormatHM(windowStart);
        string slotName = GetSlotDisplayName(clock.Slot);

        string title = $"Sắp đến giờ học môn {subjectDisplayName}";
        string detail = $"Bắt đầu từ {timeStr} - {slotName}";
        string buttonText = "Đi học";
        string searchData = $"{subjectDisplayName}|{subjectKey}";

        CreateOrUpdateTask(taskKey, title, detail, buttonText, searchData);
    }

    private void CreateOrUpdateTask(string key, string title, string detail, string btnText, string searchData)
    {
        bool isNewTask = !activeTasks.ContainsKey(key);

        if (isNewTask)
        {
            activeTasks[key] = new TaskData(key, title, detail, btnText, searchData);
            Debug.Log($"[TaskManager] Created new task: {title}");
        }
        else
        {
            var task = activeTasks[key];
            task.title = title;
            task.detail = detail;
            task.buttonText = btnText;
            task.searchData = searchData;
        }
    }

    private void RemoveTask(string key)
    {
        if (activeTasks.Remove(key))
            Debug.Log($"[TaskManager] Removed task: {key}");
    }

    private void ClearAllTasks()
    {
        if (activeTasks.Count > 0)
        {
            int count = activeTasks.Count;
            activeTasks.Clear();
            Debug.Log($"[TaskManager] Cleared {count} tasks");
        }
    }

    #endregion
    // ============================================================

    #region Notifications

    private void UpdateTaskNotificationState()
    {
        bool hasActiveTasks = activeTasks.Count > 0;

        if (GameManager.Ins == null || !GameManager.Ins.isActiveAndEnabled)
        {
            if (_waitSyncRoutine == null)
                _waitSyncRoutine = StartCoroutine(WaitForGameManagerAndSync(hasActiveTasks));
            return;
        }

        GameManager.Ins.SetIconNotification(IconType.Task, hasActiveTasks);
    }

    private IEnumerator WaitForGameManagerAndSync(bool stateToSync)
    {
        while (GameManager.Ins == null || !GameManager.Ins.isActiveAndEnabled)
            yield return null;

        GameManager.Ins.SetIconNotification(IconType.Task, stateToSync);
        _waitSyncRoutine = null;
    }

    #endregion
    // ============================================================

    #region Public API

    public Dictionary<string, TaskData> GetActiveTasks() =>
        new Dictionary<string, TaskData>(activeTasks);

    public bool HasPendingTasks() => activeTasks.Count > 0;
    public int GetActiveTaskCount() => activeTasks.Count;

    public TaskData GetTask(string key) =>
        activeTasks.TryGetValue(key, out var task) ? task : null;

    #endregion
    // ============================================================

    #region Navigation

    public void HandleTaskAction(string searchData)
    {
        if (string.IsNullOrEmpty(searchData)) return;

        string[] parts = searchData.Split('|');
        string displayName = parts[0];
        string subjectKey = parts.Length > 1 ? parts[1] : displayName;

        var targetTeacher = FindTeacherForSubject(displayName, subjectKey);
        if (targetTeacher != null)
            NavigateToTeacher(targetTeacher, displayName);
        else
            Debug.LogWarning($"[TaskManager] Teacher not found for subject '{displayName}'");
    }

    private TeacherAction FindTeacherForSubject(string displayName, string subjectKey)
    {
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        foreach (var teacher in teachers)
        {
            // Bỏ qua giáo viên không thuộc kỳ hiện tại
            if (teacher.semesterConfig != null && teacher.semesterConfig.Semester != clock.Term)
                continue;

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

    private bool IsSubjectMatch(string teacherSubjectName, string displayName, string subjectKey)
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

    private void NavigateToTeacher(TeacherAction teacher, string subjectName)
    {
        if (NavigationLineManager.Instance != null)
            NavigationLineManager.Instance.CreateNavigationLine(teacher.transform, $"GV {teacher.name} - {subjectName}");

        if (GameUIManager.Ins != null && GameUIManager.Ins.IsAnyStatUIOpen)
            GameUIManager.Ins.CloseAllUIs();

        Debug.Log($"[TaskManager] Navigation started to teacher {teacher.name} for subject {subjectName}");
    }

    #endregion
    // ============================================================

    #region Helpers

    private void LoadSubjectNamesFromJson()
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

    private string NormalizeKey(string key) =>
        key.Replace(" ", "").Replace("_", "").ToLowerInvariant();

    private string GetDisplayName(string keyOrFallback)
    {
        if (string.IsNullOrEmpty(keyOrFallback)) return keyOrFallback;
        var key = normalizeKeyOnLoad ? NormalizeKey(keyOrFallback) : keyOrFallback;
        if (subjectDisplayMap.TryGetValue(key, out var display)) return display;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
               .ToTitleCase(key.Replace('_', ' ').ToLowerInvariant());
    }

    [ContextMenu("Force Refresh Tasks (compat)")]
    public void ForceRefreshTasks() => RefreshTasks();

    [ContextMenu("Reset Task Notification State (compat)")]
    public void ResetTaskNotificationState()
    {
        hasActiveTasksLastFrame = null;
        UpdateTaskNotificationState();
    }

    private string GetSlotDisplayName(DaySlot slot) => slot switch
    {
        DaySlot.MorningA => "Buổi sáng - Ca 1",
        DaySlot.MorningB => "Buổi sáng - Ca 2",
        DaySlot.AfternoonA => "Buổi chiều - Ca 3",
        DaySlot.AfternoonB => "Buổi chiều - Ca 4",
        DaySlot.Evening => "Buổi tối - Ca 5",
        _ => slot.ToString()
    };

    #endregion
}
