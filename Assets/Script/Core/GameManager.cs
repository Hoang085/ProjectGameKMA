using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : Singleton<GameManager>
{
    [Header("Icon Notification System")]
    [SerializeField] private IconNotificationManager iconNotificationManager;

    // Events for icon notifications
    public event Action<IconType> OnIconNotificationChanged;

    // Dictionary to track notification states
    private Dictionary<IconType, bool> iconNotificationStates = new Dictionary<IconType, bool>();

    // Tracking variables for notification conditions
    private Dictionary<string, long> lastViewedScores = new Dictionary<string, long>();
    private List<string> lastViewedNotes = new List<string>();
    private float lastViewedGPA = 0f;
    private bool hasInitializedData = false;

    // Task notification flag
    private bool taskNotificationEnabled = false;

    void Start()
    {
        InitializeIconNotifications();
        InitializeTrackingData();

        // FIXED: Delay task notification check to ensure TaskManager is ready
        StartCoroutine(DelayedTaskNotificationCheck());
    }

    // FIXED: Add delayed task notification check
    private IEnumerator DelayedTaskNotificationCheck()
    {
        // Wait for TaskManager to initialize
        yield return new WaitForSeconds(0.5f);

        // Force initial task notification sync
        if (TaskManager.Instance != null)
        {
            bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
            SetIconNotification(IconType.Task, hasActiveTasks);
            Debug.Log($"[GameManager] Initial task notification set: {(hasActiveTasks ? "SHOW" : "HIDE")}");
        }
        else
        {
            SetIconNotification(IconType.Task, false);
            Debug.Log("[GameManager] TaskManager not found - task notification set to HIDE");
        }
    }

    void Update()
    {
        // Check for changes that might trigger notifications
        CheckForNotificationTriggers();
    }

    private void InitializeIconNotifications()
    {
        // Initialize all icon types as not having notifications
        foreach (IconType iconType in System.Enum.GetValues(typeof(IconType)))
        {
            iconNotificationStates[iconType] = false;
        }

        // Get reference to IconNotificationManager if not assigned
        if (iconNotificationManager == null)
        {
            iconNotificationManager = FindFirstObjectByType<IconNotificationManager>();
        }

        // FIXED: Check if TaskManager is available and set flag accordingly
        taskNotificationEnabled = TaskManager.Instance != null;
        if (taskNotificationEnabled)
        {
            Debug.Log("[GameManager] TaskManager found - task notifications enabled");
        }
        else
        {
            Debug.LogWarning("[GameManager] TaskManager NOT found! Task notifications will be disabled.");
        }
    }

    private void InitializeTrackingData()
    {
        if (hasInitializedData) return;

        // Initialize last viewed data to current state to prevent initial notifications
        InitializeLastViewedScores();
        InitializeLastViewedNotes();
        InitializeLastViewedGPA();

        hasInitializedData = true;
    }

    private void InitializeLastViewedScores()
    {
        var examDB = ExamResultStorageFile.Load();
        if (examDB?.entries != null)
        {
            foreach (var entry in examDB.entries)
            {
                string key = $"{entry.subjectKey}_{entry.semesterIndex}";
                if (!lastViewedScores.ContainsKey(key) || entry.takenAtUnix > lastViewedScores[key])
                {
                    lastViewedScores[key] = entry.takenAtUnix;
                }
            }
        }
    }

    private void InitializeLastViewedNotes()
    {
        var notesService = NotesService.Instance;
        if (notesService != null)
        {
            foreach (var noteRef in notesService.noteRefs)
            {
                string noteKey = $"{noteRef.subjectKey}_{noteRef.sessionIndex}";
                if (!lastViewedNotes.Contains(noteKey))
                {
                    lastViewedNotes.Add(noteKey);
                }
            }
        }
    }

    private void InitializeLastViewedGPA()
    {
        var playerStats = FindFirstObjectByType<PlayerStatsUI>();
        if (playerStats != null)
        {
            // Get current GPA as baseline
            var examDB = ExamResultStorageFile.Load();
            if (examDB?.entries != null && examDB.entries.Count > 0)
            {
                var latestScores = new Dictionary<string, float>();
                foreach (var entry in examDB.entries)
                {
                    if (!lastViewedScores.ContainsKey(entry.subjectKey) ||
                        entry.takenAtUnix > lastViewedScores.GetValueOrDefault(entry.subjectKey, 0))
                    {
                        latestScores[entry.subjectKey] = entry.score4;
                    }
                }

                if (latestScores.Count > 0)
                {
                    float totalScore = 0f;
                    foreach (var score in latestScores.Values)
                    {
                        totalScore += score;
                    }
                    lastViewedGPA = Mathf.Clamp(totalScore / latestScores.Count, 0f, 4f);
                }
            }
        }
    }

    private void CheckForNotificationTriggers()
    {
        if (!hasInitializedData) return;

        CheckScoreNotification();
        CheckTaskNotification();
        CheckBaloNotification();
        CheckPlayerStatsNotification();
    }

    private void CheckPlayerStatsNotification()
    {
        // Show notification if GPA has changed
        bool shouldShow = false;

        var examDB = ExamResultStorageFile.Load();
        if (examDB?.entries != null && examDB.entries.Count > 0)
        {
            var latestScores = new Dictionary<string, float>();
            foreach (var entry in examDB.entries)
            {
                if (!latestScores.ContainsKey(entry.subjectKey) ||
                    entry.takenAtUnix > lastViewedScores.GetValueOrDefault($"{entry.subjectKey}_{entry.semesterIndex}", 0))
                {
                    latestScores[entry.subjectKey] = entry.score4;
                }
            }

            if (latestScores.Count > 0)
            {
                float totalScore = 0f;
                foreach (var score in latestScores.Values)
                {
                    totalScore += score;
                }
                float currentGPA = Mathf.Clamp(totalScore / latestScores.Count, 0f, 4f);

                // Check if GPA has changed (with small tolerance for floating point comparison)
                if (Mathf.Abs(currentGPA - lastViewedGPA) > 0.01f)
                {
                    shouldShow = true;
                }
            }
        }

        SetIconNotification(IconType.Player, shouldShow);
    }

    // ===== SIMPLIFIED TASK NOTIFICATION METHOD =====
    private void CheckTaskNotification()
    {
        // FIXED: More robust task notification checking
        if (!taskNotificationEnabled)
        {
            SetIconNotification(IconType.Task, false);
            return;
        }

        if (TaskManager.Instance == null)
        {
            SetIconNotification(IconType.Task, false);
            Debug.LogWarning("[GameManager] TaskManager.Instance is null - hiding task notification");
            return;
        }

        // FIXED: Get task state directly from TaskManager with error handling
        try
        {
            bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
            int taskCount = TaskManager.Instance.GetActiveTaskCount();

            SetIconNotification(IconType.Task, hasActiveTasks);

            // Debug log only when state changes
            if (Time.frameCount % 300 == 0) // Log every 300 frames (~5 seconds at 60fps)
            {
                Debug.Log($"[GameManager] Task notification: {(hasActiveTasks ? "SHOW" : "HIDE")} ({taskCount} tasks)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] Error checking task notification: {ex.Message}");
            SetIconNotification(IconType.Task, false);
        }
    }


    private void CheckBaloNotification()
    {
        // Show notification if new notes have been added to backpack
        bool shouldShow = false;

        var notesService = NotesService.Instance;
        if (notesService != null)
        {
            foreach (var noteRef in notesService.noteRefs)
            {
                string noteKey = $"{noteRef.subjectKey}_{noteRef.sessionIndex}";
                if (!lastViewedNotes.Contains(noteKey))
                {
                    shouldShow = true;
                    break;
                }
            }
        }

        SetIconNotification(IconType.Balo, shouldShow);
    }

    private void CheckScoreNotification()
    {
        // Show notification if there are new scores or updated scores
        bool shouldShow = false;

        var examDB = ExamResultStorageFile.Load();
        if (examDB?.entries != null)
        {
            foreach (var entry in examDB.entries)
            {
                string key = $"{entry.subjectKey}_{entry.semesterIndex}";
                long lastViewed = lastViewedScores.GetValueOrDefault(key, 0);

                if (entry.takenAtUnix > lastViewed)
                {
                    shouldShow = true;
                    break;
                }
            }
        }

        SetIconNotification(IconType.Score, shouldShow);
    }

    /// <summary>
    /// Set notification state for a specific icon
    /// </summary>
    /// <param name="iconType">Type of icon</param>
    /// <param name="showNotification">Whether to show or hide notification</param>
    public void SetIconNotification(IconType iconType, bool showNotification)
    {
        if (!iconNotificationStates.ContainsKey(iconType))
        {
            iconNotificationStates[iconType] = false;
        }

        if (iconNotificationStates[iconType] != showNotification)
        {
            iconNotificationStates[iconType] = showNotification;

            // Update UI
            if (iconNotificationManager != null)
            {
                iconNotificationManager.SetNotificationVisible(iconType, showNotification);
            }
            else
            {
                Debug.LogWarning($"[GameManager] IconNotificationManager is null - cannot show notification for {iconType}");
            }

            // Trigger event
            OnIconNotificationChanged?.Invoke(iconType);

            Debug.Log($"[GameManager] Notification {iconType}: {(showNotification ? "SHOW" : "HIDE")}");
        }
    }

    /// <summary>
    /// Get current notification state for an icon
    /// </summary>
    /// <param name="iconType">Type of icon</param>
    /// <returns>True if notification should be shown</returns>
    public bool GetIconNotification(IconType iconType)
    {
        return iconNotificationStates.ContainsKey(iconType) && iconNotificationStates[iconType];
    }

    /// <summary>
    /// Clear notification when player clicks on icon
    /// </summary>
    /// <param name="iconType">Type of icon that was clicked</param>
    // FIXED: Improve task notification handling when icon is clicked
    public void OnIconClicked(IconType iconType)
    {
        if (iconType == IconType.Task)
        {
            // FIXED: Don't clear task notification immediately when clicked
            // Only clear it when Task UI is actually closed
            Debug.Log($"[GameManager] Task icon clicked - keeping notification until UI is closed");
            return;
        }

        // Update the "last viewed" data when icon is clicked
        UpdateLastViewedData(iconType);

        SetIconNotification(iconType, false);
        Debug.Log($"[GameManager] Icon {iconType} clicked - notification cleared");
    }

    // IMPROVED: Better handling of task UI close event
    public void OnTaskUIClosed()
    {
        // FIXED: Always refresh notification state when task UI is closed
        if (TaskManager.Instance != null)
        {
            try
            {
                bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                SetIconNotification(IconType.Task, hasActiveTasks);
                Debug.Log($"[GameManager] Task UI closed - notification refreshed: {(hasActiveTasks ? "SHOW" : "HIDE")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Error refreshing task notification on UI close: {ex.Message}");
                SetIconNotification(IconType.Task, false);
            }
        }
        else
        {
            SetIconNotification(IconType.Task, false);
            Debug.LogWarning("[GameManager] TaskManager not available - setting task notification to HIDE");
        }
    }

    private void UpdateLastViewedData(IconType iconType)
    {
        switch (iconType)
        {
            case IconType.Score:
                UpdateLastViewedScores();
                break;
            case IconType.Task:
                // ĐÃ SỬA: Không xử lý gì ở đây nữa, để TaskManager tự quản lý
                Debug.Log("[GameManager] Task viewed - chờ đóng UI để xóa thông báo");
                break;
            case IconType.Balo:
                UpdateLastViewedNotes();
                break;
            case IconType.Player:
                UpdateLastViewedGPA();
                break;
        }
    }

    private void UpdateLastViewedScores()
    {
        var examDB = ExamResultStorageFile.Load();
        if (examDB?.entries != null)
        {
            lastViewedScores.Clear();
            foreach (var entry in examDB.entries)
            {
                string key = $"{entry.subjectKey}_{entry.semesterIndex}";
                if (!lastViewedScores.ContainsKey(key) || entry.takenAtUnix > lastViewedScores[key])
                {
                    lastViewedScores[key] = entry.takenAtUnix;
                }
            }
        }
    }

    private void UpdateLastViewedNotes()
    {
        var notesService = NotesService.Instance;
        if (notesService != null)
        {
            lastViewedNotes.Clear();
            foreach (var noteRef in notesService.noteRefs)
            {
                string noteKey = $"{noteRef.subjectKey}_{noteRef.sessionIndex}";
                lastViewedNotes.Add(noteKey);
            }
        }
    }

    private void UpdateLastViewedGPA()
    {
        var examDB = ExamResultStorageFile.Load();
        if (examDB?.entries != null && examDB.entries.Count > 0)
        {
            var latestScores = new Dictionary<string, float>();
            foreach (var entry in examDB.entries)
            {
                if (!latestScores.ContainsKey(entry.subjectKey) ||
                    entry.takenAtUnix > lastViewedScores.GetValueOrDefault($"{entry.subjectKey}_{entry.semesterIndex}", 0))
                {
                    latestScores[entry.subjectKey] = entry.score4;
                }
            }

            if (latestScores.Count > 0)
            {
                float totalScore = 0f;
                foreach (var score in latestScores.Values)
                {
                    totalScore += score;
                }
                lastViewedGPA = Mathf.Clamp(totalScore / latestScores.Count, 0f, 4f);
            }
        }
    }

    /// <summary>
    /// Manually trigger notification for specific icon (for external systems)
    /// </summary>
    /// <param name="iconType">Type of icon</param>
    public void TriggerIconNotification(IconType iconType)
    {
        SetIconNotification(iconType, true);
        Debug.Log($"[GameManager] Notification triggered for icon {iconType}");
    }

    /// <summary>
    /// Force refresh notification for specific icon (useful when data changes externally)
    /// </summary>
    /// <param name="iconType">Type of icon to refresh</param>
    public void RefreshIconNotification(IconType iconType)
    {
        switch (iconType)
        {
            case IconType.Score:
                CheckScoreNotification();
                break;
            case IconType.Task:
                // Use TaskManager directly for task notifications
                if (TaskManager.Instance != null)
                {
                    bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                    SetIconNotification(IconType.Task, hasActiveTasks);
                    Debug.Log($"[GameManager] Task notification refreshed via TaskManager: {(hasActiveTasks ? "SHOW" : "HIDE")} ({TaskManager.Instance.GetActiveTaskCount()} tasks)");
                }
                else
                {
                    SetIconNotification(IconType.Task, false);
                    Debug.LogWarning("[GameManager] TaskManager not available - task notification set to false");
                }
                break;
            case IconType.Balo:
                CheckBaloNotification();
                break;
            case IconType.Player:
                CheckPlayerStatsNotification();
                break;
        }
    }

    /// <summary>
    /// Clear all notifications
    /// </summary>
    public void ClearAllNotifications()
    {
        foreach (IconType iconType in System.Enum.GetValues(typeof(IconType)))
        {
            SetIconNotification(iconType, false);
        }
        Debug.Log("[GameManager] All notifications cleared");
    }

    /// <summary>
    /// Public method to be called when new note is added (from external systems)
    /// </summary>
    public void OnNoteAdded(string subjectKey, int sessionIndex)
    {
        string noteKey = $"{subjectKey}_{sessionIndex}";
        if (!lastViewedNotes.Contains(noteKey))
        {
            SetIconNotification(IconType.Balo, true);
            Debug.Log($"[GameManager] New note detected: {noteKey} - Balo notification triggered");
        }
    }

    /// <summary>
    /// Public method to be called when new score is added (from external systems)
    /// </summary>
    public void OnScoreAdded(string subjectKey, int semesterIndex, long timestamp)
    {
        string key = $"{subjectKey}_{semesterIndex}";
        long lastViewed = lastViewedScores.GetValueOrDefault(key, 0);

        if (timestamp > lastViewed)
        {
            SetIconNotification(IconType.Score, true);
            SetIconNotification(IconType.Player, true); // GPA might have changed
            Debug.Log($"[GameManager] New score detected: {key} - Score and Player notifications triggered");
        }
    }

    /// <summary>
    /// Manual test method to trigger task notification (for testing)
    /// </summary>
    [ContextMenu("Test Task Notification")]
    public void TestTaskNotification()
    {
        SetIconNotification(IconType.Task, true);
        Debug.Log("[GameManager] Task notification manually triggered for testing");
    }

    /// <summary>
    /// Manual method to clear task notification (for debugging)
    /// </summary>
    [ContextMenu("Clear Task Notification")]
    public void ClearTaskNotification()
    {
        SetIconNotification(IconType.Task, false);
        Debug.Log("[GameManager] Task notification manually cleared");
    }

    /// <summary>
    /// Check TaskManager availability (for debugging)
    /// </summary>
    [ContextMenu("Check TaskManager Status")]
    public void CheckTaskManagerStatus()
    {
        if (TaskManager.Instance != null)
        {
            int taskCount = TaskManager.Instance.GetActiveTaskCount();
            bool hasTasks = TaskManager.Instance.HasPendingTasks();
            Debug.Log($"[GameManager] TaskManager Status: Available, {taskCount} active tasks, has pending: {hasTasks}");
        }
        else
        {
            Debug.LogWarning("[GameManager] TaskManager Status: NOT AVAILABLE");
        }
    }

    // Thêm vào cuối class GameManager, trước dấu }

    /// <summary>
    /// ĐÃ SỬA: Phương thức debug để đồng bộ thông báo nhiệm vụ với TaskManager
    /// </summary>
    [ContextMenu("Đồng Bộ Thông Báo Nhiệm Vụ Với TaskManager")]
    public void SyncTaskNotificationWithTaskManager()
    {
        if (TaskManager.Instance != null)
        {
            bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
            int taskCount = TaskManager.Instance.GetActiveTaskCount();

            SetIconNotification(IconType.Task, hasActiveTasks);

            Debug.Log($"[GameManager] Đã đồng bộ thông báo nhiệm vụ: {(hasActiveTasks ? "HIỆN" : "ẨN")} " +
                     $"(TaskManager báo cáo {taskCount} nhiệm vụ đang hoạt động)");
        }
        else
        {
            SetIconNotification(IconType.Task, false);
            Debug.LogWarning("[GameManager] TaskManager không khả dụng - đặt thông báo nhiệm vụ thành false");
        }
    }

    /// <summary>
    /// ĐÃ SỬA: Phương thức debug để kiểm tra trạng thái thông báo tất cả icon
    /// </summary>
    [ContextMenu("Kiểm Tra Trạng Thái Tất Cả Thông Báo")]
    public void CheckAllNotificationStatus()
    {
        Debug.Log("=== TRẠNG THÁI THÔNG BÁO TẤT CẢ ICON ===");
        foreach (IconType iconType in System.Enum.GetValues(typeof(IconType)))
        {
            bool isActive = GetIconNotification(iconType);
            Debug.Log($"[GameManager] {iconType}: {(isActive ? "ĐANG HIỆN" : "ĐANG ẨN")}");
        }

        // Kiểm tra đặc biệt cho TaskManager
        if (TaskManager.Instance != null)
        {
            int taskCount = TaskManager.Instance.GetActiveTaskCount();
            bool hasTasks = TaskManager.Instance.HasPendingTasks();
            Debug.Log($"[GameManager] TaskManager: {taskCount} nhiệm vụ, có nhiệm vụ chờ: {hasTasks}");
        }
        else
        {
            Debug.LogWarning("[GameManager] TaskManager: KHÔNG KHẢ DỤNG");
        }
    }
}


/// <summary>
/// Enum defining types of icons that can have notifications
/// </summary>
public enum IconType
{
    Player,
    Balo,
    Task,
    Score
}