using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : Singleton<GameManager>
{
    [Header("Icon Notification System")]
    [SerializeField] private IconNotificationManager iconNotificationManager;

    public static event Action OnGameManagerReady;

    public event Action<IconType> OnIconNotificationChanged;

    private Dictionary<IconType, bool> iconNotificationStates = new Dictionary<IconType, bool>();

    private Dictionary<string, long> lastViewedScores = new Dictionary<string, long>();
    private List<string> lastViewedNotes = new List<string>();
    private float lastViewedGPA = 0f;
    private bool hasInitializedData = false;

    private bool taskNotificationEnabled = false;

    // Biến theo dõi scene để tránh duplicate check
    private string lastSceneName = "";
    // Track exam completion để tránh xử lý trùng
    private int lastProcessedExamTimestamp = 0;

    private float taskManagerCheckTimer = 0f;
    private const float TASK_MANAGER_CHECK_INTERVAL = 0.5f; // Check every 0.5 seconds - more responsive
    private bool taskManagerWasNull = true; // Start as true to detect initial connection
    private int taskManagerRetryCount = 0;
    private const int MAX_TASK_MANAGER_RETRIES = 10; // Max retries before giving up temporarily

    public override void Awake()
    {
        base.Awake();
        MakeSingleton(true);
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Thông báo cho các hệ thống khác khi GameManager đã sẵn sàng
        OnGameManagerReady?.Invoke();
    }

    void OnDestroy()
    {
        // **QUAN TRỌNG: Hủy đăng ký sự kiện**
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // **TỐI ƪU: Kiểm tra ngay lập tức không delay**
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // **SỬA: Reset lastSceneName nếu không phải GameScene để cho phép xử lý lần tiếp theo**
        if (scene.name != "GameScene")
        {
            lastSceneName = "";
            Debug.Log($"[GameManager] Scene '{scene.name}' loaded - reset lastSceneName for next GameScene load");
            return;
        }

        // Lưới an toàn: nếu trước đó MiniGame có pause, bảo đảm về GameScene luôn chạy bình thường
        Time.timeScale = 1f;

        Debug.Log($"[GameManager] Scene '{scene.name}' đã load - Kiểm tra cờ khôi phục...");

        bool shouldRestoreExam = PlayerPrefs.GetInt("ShouldRestoreStateAfterExam", 0) == 1;
        bool shouldRestoreMiniGame = PlayerPrefs.GetInt("ShouldRestoreStateAfterMiniGame", 0) == 1;
        bool shouldAdvance = PlayerPrefs.GetInt("ShouldAdvanceTimeAfterExam", 0) == 1;

        bool hasAnyFlag = shouldRestoreExam || shouldRestoreMiniGame || shouldAdvance;

        if (!hasAnyFlag)
        {
            Debug.Log("[GameManager] Không có cờ khôi phục nào được thiết lập");
            return;
        }

        // **SỬA: Sử dụng timestamp để tránh xử lý lặp thay vì dùng lastSceneName**
        int currentTimestamp = PlayerPrefs.GetInt("EXAM_COMPLETED_TIMESTAMP", 0);
        if (currentTimestamp > 0 && currentTimestamp == lastProcessedExamTimestamp)
        {
            Debug.Log($"[GameManager] Bỏ qua vì đã xử lý exam timestamp {currentTimestamp}.");
            return;
        }

        // **MỚI: Lưu timestamp để tránh xử lý lặp**
        if (currentTimestamp > 0)
        {
            lastProcessedExamTimestamp = currentTimestamp;
        }

        Debug.Log($"[GameManager] Phát hiện cờ khôi phục: RestoreExam={shouldRestoreExam}, RestoreMiniGame={shouldRestoreMiniGame}, Advance={shouldAdvance}");

        // Xoá các cờ NGAY LẬP TỨC để tránh loop nếu có reload
        if (shouldRestoreExam)
            PlayerPrefs.DeleteKey("ShouldRestoreStateAfterExam");
        if (shouldRestoreMiniGame)
            PlayerPrefs.DeleteKey("ShouldRestoreStateAfterMiniGame");
        if (shouldAdvance)
            PlayerPrefs.DeleteKey("ShouldAdvanceTimeAfterExam");
        PlayerPrefs.Save();

        // Ưu tiên khôi phục state nếu có (khôi phục xong sẽ tự chuyển ca)
        if (shouldRestoreExam || shouldRestoreMiniGame)
        {
            StartCoroutine(FastRestoreStateCoroutine());
            return;
        }

        // Nếu chỉ có yêu cầu chuyển ca (không cần khôi phục)
        if (shouldAdvance)
        {
            StartCoroutine(AdvanceTimeAfterExamCoroutine());
            return;
        }
    }


    void Start()
    {
        Debug.Log("[GameManager] Start() được gọi - Khởi tạo cơ bản...");

        InitializeIconNotifications();
        InitializeTrackingData();

        // **THÊM: Khởi tạo PlayerStatsUI sớm để hệ thống stamina hoạt động**
        // NHƯNG đảm bảo không làm tăng _openPopupCount
        StartCoroutine(InitializePlayerStatsUIQuietly());

        // **SỬA: Start TaskManager check immediately**
        StartCoroutine(DelayedTaskNotificationCheck());
    }

    /// <summary>
    /// **MỚI: Khởi tạo PlayerStatsUI "im lặng" - không tính vào _openPopupCount**
    /// </summary>
    private System.Collections.IEnumerator InitializePlayerStatsUIQuietly()
    {
        Debug.Log("[GameManager] Initializing PlayerStatsUI quietly...");

        // Đợi PopupManager sẵn sàng
        yield return new WaitForSeconds(0.3f);

        if (HHH.Common.PopupManager.Ins == null)
        {
            Debug.LogWarning("[GameManager] ✗ PopupManager not found - PlayerStatsUI initialization skipped");
            yield break;
        }

        var popupManager = HHH.Common.PopupManager.Ins;
        var playerStatsPopup = popupManager.GetPopup(HHH.Common.PopupName.PlayerStat);

        if (playerStatsPopup != null)
        {
            Debug.Log("[GameManager] ✓ PlayerStatsUI already exists");
            yield break;
        }

        Debug.Log("[GameManager] Creating PlayerStatsUI in background...");

        // **GIẢI PHÁP: Tạo popup nhưng đảm bảo nó KHÔNG gọi OnPopupOpened**
        // Bằng cách disable GameObject trước khi ShowScreen
        popupManager.OnShowScreen(HHH.Common.PopupName.PlayerStat);

        // Đợi 1 frame để popup được tạo
        yield return null;

        // Lấy popup vừa tạo
        playerStatsPopup = popupManager.GetPopup(HHH.Common.PopupName.PlayerStat);

        if (playerStatsPopup != null)
        {
            // **QUAN TRỌNG: Đóng popup NGAY LẬP TỨC và giảm _openPopupCount**
            var basePopup = playerStatsPopup.GetComponent<HHH.Common.BasePopUp>();
            if (basePopup != null)
            {
                // Gọi OnDeActived để đóng popup đúng cách
                basePopup.OnDeActived();
            }

            // Đảm bảo popup bị ẩn hoàn toàn
            playerStatsPopup.SetActive(false);

            // **FIX: Giảm popup counter về 0 vì ta không muốn tính popup này**
            if (GameUIManager.Ins != null)
            {
                GameUIManager.Ins.OnPopupClosed();
            }

            Debug.Log("[GameManager] ✓ PlayerStatsUI initialized quietly - stamina system ready");
        }
        else
        {
            Debug.LogWarning("[GameManager] ✗ Failed to create PlayerStatsUI");
        }
    }

    void Update()
    {
        CheckForNotificationTriggers();

        // **SỬA: Improved TaskManager availability check**
        CheckTaskManagerAvailability();
    }

    // **SỬA: Improved TaskManager availability checking với better error handling**
    private void CheckTaskManagerAvailability()
    {
        taskManagerCheckTimer += Time.deltaTime;

        if (taskManagerCheckTimer >= TASK_MANAGER_CHECK_INTERVAL)
        {
            taskManagerCheckTimer = 0f;

            bool taskManagerAvailable = false;

            // **SỬA: More robust TaskManager checking**
            try
            {
                taskManagerAvailable = TaskManager.Instance != null && TaskManager.Instance.gameObject != null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameManager] Exception while checking TaskManager: {ex.Message}");
                taskManagerAvailable = false;
            }

            // **SỬA: TaskManager became available**
            if (taskManagerAvailable && taskManagerWasNull)
            {
                Debug.Log("[GameManager] TaskManager is now available - enabling task notifications");
                taskNotificationEnabled = true;
                taskManagerRetryCount = 0;

                // **SỬA: Force sync notification state with error handling**
                try
                {
                    bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                    SetIconNotification(IconType.Task, hasActiveTasks);
                    Debug.Log($"[GameManager] TaskManager reconnected - notification: {(hasActiveTasks ? "SHOW" : "HIDE")} ({TaskManager.Instance.GetActiveTaskCount()} tasks)");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameManager] Error syncing task notification after reconnect: {ex.Message}");
                    SetIconNotification(IconType.Task, false);
                }

                taskManagerWasNull = false;
            }
            // **SỬA: TaskManager became unavailable**
            else if (!taskManagerAvailable && !taskManagerWasNull)
            {
                taskManagerRetryCount++;

                if (taskManagerRetryCount <= MAX_TASK_MANAGER_RETRIES)
                {
                    Debug.LogWarning($"[GameManager] TaskManager became unavailable (retry {taskManagerRetryCount}/{MAX_TASK_MANAGER_RETRIES}) - temporarily disabling task notifications");
                }
                else
                {
                    Debug.LogWarning("[GameManager] TaskManager unavailable after max retries - disabling task notifications");
                }

                taskNotificationEnabled = false;
                SetIconNotification(IconType.Task, false);
                taskManagerWasNull = true;
            }
        }
    }

    // **SỬA: Improved delayed task notification check với better retry logic**
    private IEnumerator DelayedTaskNotificationCheck()
    {
        Debug.Log("[GameManager] Starting TaskManager connection check...");

        // **SỬA: Wait for TaskManager with more robust checking**
        int maxRetries = 20; // Increase max retries
        int currentRetry = 0;
        float retryInterval = 0.25f; // Shorter retry interval

        while (currentRetry < maxRetries)
        {
            bool taskManagerReady = false;

            try
            {
                taskManagerReady = TaskManager.Instance != null && TaskManager.Instance.gameObject != null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameManager] Exception during TaskManager check (retry {currentRetry}): {ex.Message}");
                taskManagerReady = false;
            }

            if (taskManagerReady)
            {
                // **SỬA: TaskManager is ready**
                taskNotificationEnabled = true;
                taskManagerWasNull = false;
                taskManagerRetryCount = 0;

                try
                {
                    bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                    SetIconNotification(IconType.Task, hasActiveTasks);
                    Debug.Log($"[GameManager] TaskManager connected successfully after {currentRetry} retries - notification: {(hasActiveTasks ? "SHOW" : "HIDE")} ({TaskManager.Instance.GetActiveTaskCount()} tasks)");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameManager] Error setting initial task notification: {ex.Message}");
                    SetIconNotification(IconType.Task, false);
                }

                yield break;
            }

            currentRetry++;
            Debug.Log($"[GameManager] TaskManager not ready, retry {currentRetry}/{maxRetries}");
            yield return new WaitForSeconds(retryInterval);
        }

        // **SỬA: Failed to connect after all retries**
        Debug.LogWarning($"[GameManager] TaskManager not found after {maxRetries} retries - task notifications will be disabled until TaskManager becomes available");
        taskNotificationEnabled = false;
        SetIconNotification(IconType.Task, false);
        taskManagerWasNull = true;
    }

    // **THÊM: Method hỗ trợ GameStateManager refresh notification states**
    /// <summary>
    /// Called by GameStateManager after state restoration to refresh all notification states
    /// </summary>
    public void RefreshAllNotificationStatesAfterRestore()
    {
        Debug.Log("[GameManager] Refreshing all notification states after GameStateManager restoration...");

        // Force re-check all notification types
        StartCoroutine(DelayedNotificationRefreshAfterRestore());
    }

    /// <summary>
    /// Delayed refresh để đảm bảo tất cả systems đã sẵn sàng sau restoration
    /// </summary>
    private IEnumerator DelayedNotificationRefreshAfterRestore()
    {
        // Đợi một chút để các systems khác hoàn thành initialization
        yield return new WaitForSeconds(0.5f);

        // Re-initialize tracking data để đảm bảo có baseline đúng
        InitializeTrackingData();

        yield return new WaitForSeconds(0.1f);

        // Force check tất cả notification types
        CheckScoreNotification();
        CheckBaloNotification();
        CheckPlayerStatsNotification();

        // TaskManager cần special handling
        if (TaskManager.Instance != null)
        {
            taskNotificationEnabled = true;
            taskManagerWasNull = false;
            CheckTaskNotification();
        }

        Debug.Log("[GameManager] Completed notification refresh after restoration");
    }

    // Giữ nguyên các method khác...
    private void CheckAndHandlePostExamStateRestore()
    {
        bool shouldRestore = PlayerPrefs.GetInt("ShouldRestoreStateAfterExam", 0) == 1;
        bool hasState = GameStateManager.HasSavedState();

        Debug.Log($"[GameManager] CheckAndHandlePostExamStateRestore:");
        Debug.Log($"  - ShouldRestoreStateAfterExam flag: {shouldRestore}");
        Debug.Log($"  - HasSavedState: {hasState}");

        if (shouldRestore)
        {
            // **QUAN TRỌNG: Xóa flag ngay lập tức để tránh loop**
            PlayerPrefs.DeleteKey("ShouldRestoreStateAfterExam");
            PlayerPrefs.Save();

            Debug.Log("[GameManager] Đã xóa flag và bắt đầu khôi phục...");
            StartCoroutine(FastRestoreStateCoroutine());
        }
        else
        {
            // Kiểm tra legacy flag nếu có
            CheckAndHandlePostExamTimeAdvance();
        }
    }

    private IEnumerator FastRestoreStateCoroutine()
    {
        Debug.Log("[GameManager] Bắt đầu fast restore...");

        bool needWait = (GameClock.Ins == null) || (FindFirstObjectByType<ClockUI>() == null);
        if (needWait)
        {
            yield return StartCoroutine(FastWaitForComponents());
        }

        bool hasStateToRestore = GameStateManager.HasSavedState();

        if (hasStateToRestore)
        {
            yield return StartCoroutine(GameStateManager.RestorePostExamState());
            if (GameClock.Ins != null)
            {
                GameClock.Ins.JumpToNextSessionStart();
                Debug.Log("[GameManager] Đã chuyển sang ca tiếp theo (Chốt hạ)");
            }
            RefreshAllNotificationStatesAfterRestore();
            yield return null;

            if (GameUIManager.Ins != null)
            {
                if (GameUIManager.Ins.dialogueRoot != null)
                    GameUIManager.Ins.dialogueRoot.SetActive(true);

                Debug.Log("[GameManager] Trigger hiển thị kết quả thi...");
                GameUIManager.Ins.CheckAndShowPostExamMessage();
            }
        }
        else
        {
            if (GameClock.Ins != null) GameClock.Ins.JumpToNextSessionStart();
            yield return new WaitForSeconds(0.5f);
            if (GameUIManager.Ins != null) GameUIManager.Ins.CheckAndShowPostExamMessage();
        }
    }

    private IEnumerator FastWaitForComponents()
    {
        Debug.Log("[GameManager] FastWait cho components...");

        float startTime = Time.unscaledTime;
        const float maxWait = 1f;

        while (Time.unscaledTime - startTime < maxWait)
        {
            bool hasGameClock = GameClock.Ins != null;
            bool hasClockUI = FindFirstObjectByType<ClockUI>() != null;

            if (hasGameClock && hasClockUI)
            {
                Debug.Log($"[GameManager] ✓ Components ready after {Time.unscaledTime - startTime:F2}s");
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning($"[GameManager] ⚠ Timeout after {maxWait}s - proceeding anyway");
    }

    public void SyncNotificationSystemAfterRestore()
    {
        Debug.Log("[GameManager] Syncing notification system after GameStateManager restore...");

        // Force refresh IconNotificationManager reference
        RefreshIconNotificationManager();

        // Re-enable task notifications if TaskManager is available
        if (TaskManager.Instance != null)
        {
            taskNotificationEnabled = true;
            taskManagerWasNull = false;
            taskManagerRetryCount = 0;

            try
            {
                bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                SetIconNotification(IconType.Task, hasActiveTasks);
                Debug.Log($"[GameManager] Task notification synced after restore: {(hasActiveTasks ? "SHOW" : "HIDE")}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameManager] Error syncing task notification after restore: {ex.Message}");
                SetIconNotification(IconType.Task, false);
            }
        }

        // Force check other notifications
        CheckScoreNotification();
        CheckBaloNotification();
        CheckPlayerStatsNotification();

        Debug.Log("[GameManager] Notification system sync completed");
    }

    private void InitializeIconNotifications()
    {
        // Initialize all icon types as not having notifications
        foreach (IconType iconType in System.Enum.GetValues(typeof(IconType)))
        {
            iconNotificationStates[iconType] = false;
        }

        // **SỬA: Improved IconNotificationManager finding với retry logic**
        if (iconNotificationManager == null)
        {
            // Try multiple methods to find IconNotificationManager
            iconNotificationManager = FindFirstObjectByType<IconNotificationManager>();

            if (iconNotificationManager == null)
            {
                // Try finding in GameUIManager as well
                var gameUIManager = GameUIManager.Ins;
                if (gameUIManager != null)
                {
                    iconNotificationManager = gameUIManager.GetComponentInChildren<IconNotificationManager>(true);
                }
            }

            if (iconNotificationManager == null)
            {
                // Try finding in the entire scene including inactive objects
                var allIconManagers = Resources.FindObjectsOfTypeAll<IconNotificationManager>();
                if (allIconManagers.Length > 0)
                {
                    iconNotificationManager = allIconManagers[0];
                    Debug.Log($"[GameManager] Found IconNotificationManager in inactive objects: {iconNotificationManager.name}");
                }
            }

            if (iconNotificationManager != null)
            {
                Debug.Log($"[GameManager] IconNotificationManager found: {iconNotificationManager.name}");
            }
            else
            {
                Debug.LogWarning("[GameManager] IconNotificationManager not found - notifications will not display visually");
            }
        }

        Debug.Log("[GameManager] Icon notifications initialized - TaskManager check will be done separately");
    }

    private void InitializeTrackingData()
    {
        if (hasInitializedData) return;

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

    // **SỬA: Completely rewritten task notification checking với comprehensive error handling**
    private void CheckTaskNotification()
    {
        // **SỬA: Don't check if system is not ready**
        if (!taskNotificationEnabled)
        {
            // **SỬA: Only hide notification if it's currently showing**
            if (GetIconNotification(IconType.Task))
            {
                SetIconNotification(IconType.Task, false);
                // Debug log only when state changes
                if (Time.frameCount % 600 == 0) // Log occasionally to avoid spam
                {
                    Debug.Log("[GameManager] Task notifications disabled - hiding notification");
                }
            }
            return;
        }

        // **SỬA: Robust TaskManager checking với multiple validation layers**
        try
        {
            // Check if TaskManager instance exists
            if (TaskManager.Instance == null)
            {
                Debug.LogWarning("[GameManager] TaskManager.Instance is null - disabling task notifications temporarily");
                taskNotificationEnabled = false;
                SetIconNotification(IconType.Task, false);
                taskManagerWasNull = true;
                return;
            }

            // Check if TaskManager GameObject is still valid
            if (TaskManager.Instance.gameObject == null)
            {
                Debug.LogWarning("[GameManager] TaskManager.Instance.gameObject is null - disabling task notifications temporarily");
                taskNotificationEnabled = false;
                SetIconNotification(IconType.Task, false);
                taskManagerWasNull = true;
                return;
            }

            // **SỬA: Get task state with comprehensive error handling**
            bool hasActiveTasks = false;
            int taskCount = 0;

            try
            {
                hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                taskCount = TaskManager.Instance.GetActiveTaskCount();
            }
            catch (MissingReferenceException)
            {
                Debug.LogWarning("[GameManager] TaskManager reference is missing - disabling task notifications temporarily");
                taskNotificationEnabled = false;
                SetIconNotification(IconType.Task, false);
                taskManagerWasNull = true;
                return;
            }
            catch (NullReferenceException)
            {
                Debug.LogWarning("[GameManager] TaskManager null reference - disabling task notifications temporarily");
                taskNotificationEnabled = false;
                SetIconNotification(IconType.Task, false);
                taskManagerWasNull = true;
                return;
            }

            // **SỬA: Successfully got task data, update notification**
            SetIconNotification(IconType.Task, hasActiveTasks);

            // **SỬA: Reduced logging frequency to avoid spam**
            if (Time.frameCount % 1200 == 0) // Log every 1200 frames (~20 seconds at 60fps)
            {
                Debug.Log($"[GameManager] Task notification status: {(hasActiveTasks ? "SHOW" : "HIDE")} ({taskCount} tasks)");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameManager] Unexpected error checking task notification: {ex.Message}");
            Debug.LogError($"[GameManager] Exception type: {ex.GetType().Name}");
            Debug.LogError($"[GameManager] Stack trace: {ex.StackTrace}");

            // Disable temporarily on any unexpected error
            taskNotificationEnabled = false;
            SetIconNotification(IconType.Task, false);
            taskManagerWasNull = true;
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

    // Giữ nguyên các method khác như cũ...
    public void SetIconNotification(IconType iconType, bool showNotification)
    {
        if (!iconNotificationStates.ContainsKey(iconType))
        {
            iconNotificationStates[iconType] = false;
        }

        if (iconNotificationStates[iconType] != showNotification)
        {
            iconNotificationStates[iconType] = showNotification;

            // **SỬA: Improved UI update with retry logic**
            if (iconNotificationManager != null)
            {
                try
                {
                    iconNotificationManager.SetNotificationVisible(iconType, showNotification);
                    Debug.Log($"[GameManager] Notification {iconType}: {(showNotification ? "SHOW" : "HIDE")}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameManager] Error setting notification for {iconType}: {ex.Message}");
                    // Try to re-find IconNotificationManager
                    iconNotificationManager = FindFirstObjectByType<IconNotificationManager>();
                }
            }
            else
            {
                // **SỬA: Try to find IconNotificationManager if it's null**
                iconNotificationManager = FindFirstObjectByType<IconNotificationManager>();

                if (iconNotificationManager != null)
                {
                    try
                    {
                        iconNotificationManager.SetNotificationVisible(iconType, showNotification);
                        Debug.Log($"[GameManager] Notification {iconType}: {(showNotification ? "SHOW" : "HIDE")} (found IconNotificationManager)");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[GameManager] Error setting notification for {iconType} after re-finding manager: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[GameManager] IconNotificationManager is still null - cannot show notification for {iconType}. State saved: {(showNotification ? "SHOW" : "HIDE")}");
                }
            }

            // **THÊM: Validate NotificationPopupSpawner when notification state changes**
            ValidateNotificationPopupSpawner();

            // Trigger event
            OnIconNotificationChanged?.Invoke(iconType);
        }
    }

    public bool GetIconNotification(IconType iconType)
    {
        return iconNotificationStates.ContainsKey(iconType) && iconNotificationStates[iconType];
    }

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

    // **SỬA: Improved handling of task UI close event với better error handling**
    public void OnTaskUIClosed()
    {
        // **SỬA: Always refresh notification state when task UI is closed với comprehensive error handling**
        try
        {
            if (TaskManager.Instance != null && TaskManager.Instance.gameObject != null)
            {
                bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                SetIconNotification(IconType.Task, hasActiveTasks);
                Debug.Log($"[GameManager] Task UI closed - notification refreshed: {(hasActiveTasks ? "SHOW" : "HIDE")} ({TaskManager.Instance.GetActiveTaskCount()} tasks)");
            }
            else
            {
                SetIconNotification(IconType.Task, false);
                Debug.LogWarning("[GameManager] TaskManager not available when Task UI closed - setting notification to HIDE");

                // Reset TaskManager tracking to trigger reconnection
                taskManagerWasNull = true;
                taskNotificationEnabled = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameManager] Error refreshing task notification on UI close: {ex.Message}");
            SetIconNotification(IconType.Task, false);

            // Reset TaskManager tracking on error
            taskManagerWasNull = true;
            taskNotificationEnabled = false;
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
                // **SỬA: Improved TaskManager check với comprehensive error handling**
                try
                {
                    if (TaskManager.Instance != null && TaskManager.Instance.gameObject != null)
                    {
                        bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                        int taskCount = TaskManager.Instance.GetActiveTaskCount();
                        SetIconNotification(IconType.Task, hasActiveTasks);
                        Debug.Log($"[GameManager] Task notification refreshed via TaskManager: {(hasActiveTasks ? "SHOW" : "HIDE")} ({taskCount} tasks)");

                        // Re-enable notifications if they were disabled
                        taskNotificationEnabled = true;
                        taskManagerWasNull = false;
                    }
                    else
                    {
                        SetIconNotification(IconType.Task, false);
                        Debug.LogWarning("[GameManager] TaskManager not available during refresh - task notification set to false");

                        // Trigger reconnection attempt
                        taskManagerWasNull = true;
                        taskNotificationEnabled = false;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameManager] Error refreshing task notification: {ex.Message}");
                    SetIconNotification(IconType.Task, false);

                    // Reset tracking variables on error
                    taskManagerWasNull = true;
                    taskNotificationEnabled = false;
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
        try
        {
            if (TaskManager.Instance != null && TaskManager.Instance.gameObject != null)
            {
                int taskCount = TaskManager.Instance.GetActiveTaskCount();
                bool hasTasks = TaskManager.Instance.HasPendingTasks();
                Debug.Log($"[GameManager] TaskManager Status: Available, {taskCount} active tasks, has pending: {hasTasks}");
                Debug.Log($"[GameManager] taskNotificationEnabled: {taskNotificationEnabled}, taskManagerWasNull: {taskManagerWasNull}");
            }
            else
            {
                Debug.LogWarning("[GameManager] TaskManager Status: NOT AVAILABLE");
                Debug.Log($"[GameManager] taskNotificationEnabled: {taskNotificationEnabled}, taskManagerWasNull: {taskManagerWasNull}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameManager] Error checking TaskManager status: {ex.Message}");
        }
    }

    /// <summary>
    /// **SỬA: Improved Force reconnect to TaskManager**
    /// </summary>
    [ContextMenu("Force Reconnect TaskManager")]
    public void ForceReconnectTaskManager()
    {
        Debug.Log("[GameManager] Forcing TaskManager reconnect...");

        // Reset all tracking variables
        taskManagerWasNull = true;
        taskNotificationEnabled = false;
        taskManagerRetryCount = 0;
        taskManagerCheckTimer = TASK_MANAGER_CHECK_INTERVAL; // Trigger immediate check

        // Force immediate check
        CheckTaskManagerAvailability();

        Debug.Log("[GameManager] TaskManager reconnect forced - status will be checked immediately");
    }

    /// <summary>
    /// ĐÃ SỬA: Phương thức debug để đồng bộ thông báo nhiệm vụ với TaskManager với improved error handling
    /// </summary>
    [ContextMenu("Đồng Bộ Thông Báo Nhiệm Vụ Với TaskManager")]
    public void SyncTaskNotificationWithTaskManager()
    {
        try
        {
            if (TaskManager.Instance != null && TaskManager.Instance.gameObject != null)
            {
                bool hasActiveTasks = TaskManager.Instance.HasPendingTasks();
                int taskCount = TaskManager.Instance.GetActiveTaskCount();

                taskNotificationEnabled = true; // Re-enable if it was disabled
                taskManagerWasNull = false;
                taskManagerRetryCount = 0;
                SetIconNotification(IconType.Task, hasActiveTasks);

                Debug.Log($"[GameManager] Đã đồng bộ thông báo nhiệm vụ: {(hasActiveTasks ? "HIỆN" : "ẨN")} " +
                         $"(TaskManager báo cáo {taskCount} nhiệm vụ đang hoạt động)");
            }
            else
            {
                taskNotificationEnabled = false;
                taskManagerWasNull = true;
                SetIconNotification(IconType.Task, false);
                Debug.LogWarning("[GameManager] TaskManager không khả dụng - đặt thông báo nhiệm vụ thành false");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameManager] Lỗi khi đồng bộ thông báo TaskManager: {ex.Message}");
            taskNotificationEnabled = false;
            taskManagerWasNull = true;
            SetIconNotification(IconType.Task, false);
        }
    }

    /// <summary>
    /// ĐÃ SỬA: Phương thức debug để kiểm tra trạng thái thông báo tất cả icon với improved TaskManager checking
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

        // **SỬA: Comprehensive TaskManager checking**
        try
        {
            if (TaskManager.Instance != null && TaskManager.Instance.gameObject != null)
            {
                int taskCount = TaskManager.Instance.GetActiveTaskCount();
                bool hasTasks = TaskManager.Instance.HasPendingTasks();
                Debug.Log($"[GameManager] TaskManager: {taskCount} nhiệm vụ, có nhiệm vụ chờ: {hasTasks}");
                Debug.Log($"[GameManager] taskNotificationEnabled: {taskNotificationEnabled}");
                Debug.Log($"[GameManager] taskManagerWasNull: {taskManagerWasNull}");
                Debug.Log($"[GameManager] taskManagerRetryCount: {taskManagerRetryCount}");
            }
            else
            {
                Debug.LogWarning("[GameManager] TaskManager: KHÔNG KHẢ DỤNG");
                Debug.Log($"[GameManager] TaskManager.Instance: {(TaskManager.Instance != null ? "NOT NULL" : "NULL")}");
                if (TaskManager.Instance != null)
                {
                    Debug.Log($"[GameManager] TaskManager.Instance.gameObject: {(TaskManager.Instance.gameObject != null ? "NOT NULL" : "NULL")}");
                }
                Debug.Log($"[GameManager] taskNotificationEnabled: {taskNotificationEnabled}");
                Debug.Log($"[GameManager] taskManagerWasNull: {taskManagerWasNull}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameManager] Lỗi khi kiểm tra trạng thái TaskManager: {ex.Message}");
            Debug.Log($"[GameManager] taskNotificationEnabled: {taskNotificationEnabled}");
            Debug.Log($"[GameManager] taskManagerWasNull: {taskManagerWasNull}");
        }
    }

    /// <summary>
    /// **MỚI: Manually refresh IconNotificationManager reference**
    /// </summary>
    [ContextMenu("Refresh IconNotificationManager Reference")]
    public void RefreshIconNotificationManager()
    {
        var oldManager = iconNotificationManager;

        // Try multiple methods to find IconNotificationManager
        iconNotificationManager = FindFirstObjectByType<IconNotificationManager>();

        if (iconNotificationManager == null)
        {
            // Try finding in GameUIManager as well
            var gameUIManager = GameUIManager.Ins;
            if (gameUIManager != null)
            {
                iconNotificationManager = gameUIManager.GetComponentInChildren<IconNotificationManager>(true);
            }
        }

        if (iconNotificationManager == null)
        {
            // Try finding in the entire scene including inactive objects
            var allIconManagers = Resources.FindObjectsOfTypeAll<IconNotificationManager>();
            if (allIconManagers.Length > 0)
            {
                iconNotificationManager = allIconManagers[0];
            }
        }

        if (iconNotificationManager != null)
        {
            if (oldManager != iconNotificationManager)
            {
                Debug.Log($"[GameManager] IconNotificationManager reference updated: {iconNotificationManager.name}");

                // Refresh all current notification states
                foreach (var kvp in iconNotificationStates)
                {
                    if (kvp.Value) // Only update active notifications
                    {
                        try
                        {
                            iconNotificationManager.SetNotificationVisible(kvp.Key, kvp.Value);
                            Debug.Log($"[GameManager] Refreshed notification state for {kvp.Key}: {(kvp.Value ? "SHOW" : "HIDE")}");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[GameManager] Error refreshing notification for {kvp.Key}: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                Debug.Log("[GameManager] IconNotificationManager reference is already up to date");
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] IconNotificationManager still not found after manual refresh");
        }
    }

    /// <summary>
    /// **DEBUG METHODS cho việc kiểm tra post-exam flow**
    /// </summary>
    [ContextMenu("TEST: Giả lập quay về từ ExamScene với State Restore")]
    public void TestReturnFromExamSceneWithStateRestore()
    {
        PlayerPrefs.SetInt("ShouldRestoreStateAfterExam", 1);
        PlayerPrefs.Save();
        CheckAndHandlePostExamStateRestore();
        Debug.Log("[GameManager] Đã kích hoạt test khôi phục trạng thái sau thi");
    }

    [ContextMenu("TEST: Giả lập quay về từ ExamScene (Legacy)")]
    public void TestReturnFromExamScene()
    {
        PlayerPrefs.SetInt("ShouldAdvanceTimeAfterExam", 1);
        PlayerPrefs.Save();
        CheckAndHandlePostExamTimeAdvance();
        Debug.Log("[GameManager] Đã kích hoạt test chuyển ca sau thi (legacy)");
    }

    [ContextMenu("TEST: Kiểm tra trạng thái đã lưu")]
    public void TestCheckSavedState()
    {
        bool hasSavedState = GameStateManager.HasSavedState();
        string examSubject = GameStateManager.GetSavedExamSubject();

        Debug.Log($"[GameManager] Có trạng thái đã lưu: {hasSavedState}");
        if (hasSavedState)
        {
            Debug.Log($"[GameManager] Môn thi: {examSubject}");
        }
    }

    [ContextMenu("TEST: Xóa trạng thái đã lưu")]
    public void TestClearSavedState()
    {
        GameStateManager.ClearSavedState();
        Debug.Log("[GameManager] Đã xóa trạng thái đã lưu");
    }

    /// <summary>
    /// **MỚI: Clear exam timestamp để test lại flow**
    /// </summary>
    [ContextMenu("TEST: Clear Exam Timestamp")]
    public void TestClearExamTimestamp()
    {
        lastProcessedExamTimestamp = 0;
        PlayerPrefs.DeleteKey("EXAM_COMPLETED_TIMESTAMP");
        PlayerPrefs.Save();
        Debug.Log("[GameManager] Đã xóa exam timestamp - có thể test lại exam flow");
    }

    /// <summary>
    /// **MỚI: Kiểm tra exam timestamp hiện tại**
    /// </summary>
    [ContextMenu("TEST: Check Exam Timestamp Status")]
    public void TestCheckExamTimestampStatus()
    {
        int savedTimestamp = PlayerPrefs.GetInt("EXAM_COMPLETED_TIMESTAMP", 0);
        Debug.Log("=== EXAM TIMESTAMP STATUS ===");
        Debug.Log($"[GameManager] Saved timestamp: {savedTimestamp}");
        Debug.Log($"[GameManager] Last processed timestamp: {lastProcessedExamTimestamp}");
        Debug.Log($"[GameManager] Will process next exam: {(savedTimestamp != lastProcessedExamTimestamp)}");
    }

    /// <summary>
    /// **MỚI: Validate NotificationPopupSpawner and attempt recovery if needed**
    /// </summary>
    private void ValidateNotificationPopupSpawner()
    {
        // Chỉ kiểm tra occasionally để tránh performance impact
        if (Time.frameCount % 300 != 0) return; // Check every 5 seconds at 60fps

        if (NotificationPopupSpawner.Ins == null)
        {
            Debug.LogWarning("[GameManager] NotificationPopupSpawner.Ins is null - attempting recovery...");

            // Try to find NotificationPopupSpawner in scene
            var spawner = FindFirstObjectByType<NotificationPopupSpawner>();
            if (spawner != null)
            {
                Debug.Log("[GameManager] Found NotificationPopupSpawner in scene - forcing re-registration...");
                spawner.ForceReregisterWithGameManager();
            }
        }
    }

    /// <summary>
    /// **MỚI: Manual method để force validate NotificationPopupSpawner**
    /// </summary>
    [ContextMenu("Force Validate NotificationPopupSpawner")]
    public void ForceValidateNotificationPopupSpawner()
    {
        ValidateNotificationPopupSpawner();
    }

    // Legacy flag: chỉ chuyển ca (không khôi phục vị trí)
    private void CheckAndHandlePostExamTimeAdvance()
    {
        if (PlayerPrefs.GetInt("ShouldAdvanceTimeAfterExam", 0) == 1)
        {
            PlayerPrefs.DeleteKey("ShouldAdvanceTimeAfterExam");
            PlayerPrefs.Save();
            StartCoroutine(AdvanceTimeAfterExamCoroutine());
        }
    }

    private IEnumerator AdvanceTimeAfterExamCoroutine()
    {
        yield return null;
        AdvanceToNextSession();
    }

    private void AdvanceToNextSession()
    {
        Debug.Log("[GameManager] Thực hiện chuyển ca...");

        var clockUI = FindFirstObjectByType<ClockUI>();
        if (clockUI != null)
        {
            clockUI.JumpToNextSessionNow();
            Debug.Log("[GameManager] ✓ Đã chuyển ca qua ClockUI");
        }
        else if (GameClock.Ins != null)
        {
            GameClock.Ins.NextSlot();
            Debug.Log("[GameManager] ✓ Đã chuyển ca qua GameClock");
        }
        else
        {
            Debug.LogError("[GameManager] ✗ Không thể chuyển ca - thiếu ClockUI/GameClock");
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
    Score,
    Setting,
    Schedule
}