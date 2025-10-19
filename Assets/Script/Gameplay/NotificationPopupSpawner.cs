using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class NotificationPopupSpawner : MonoBehaviour
{
    public static NotificationPopupSpawner Ins { get; private set; }

    [Header("Prefab & Parent")]
    [SerializeField] private GameObject popupPrefab;
    [SerializeField] private Transform popupParent;

    [Header("Queue")]
    [SerializeField, Min(0.05f)] private float delayBetween = 0.25f;

    [Header("PointRed Notification Settings")]
    [SerializeField] private bool enablePointRedNotifications = true;

    [Header("Icon-specific Settings")]
    [SerializeField] private Sprite scoreIcon;
    [SerializeField] private Sprite taskIcon;
    [SerializeField] private Sprite baloIcon;
    [SerializeField] private Sprite playerIcon;

    [Header("Canvas Management")]
    [SerializeField] private bool findCanvasAutomatically = true;

    private readonly Queue<(string msg, Sprite icon)> _queue = new();
    private bool _running;
    private bool _eventRegistered = false;
    private Canvas _targetCanvas;
    private Transform _originalParent;

    private void Awake()
    {
        Ins = this;

        // Store original parent before potentially being moved to DontDestroyOnLoad
        _originalParent = popupParent;

        // Find or validate canvas
        FindValidCanvas();
    }

    private void Start()
    {
        // Ensure we have a valid canvas reference after scene setup
        if (_targetCanvas == null)
        {
            FindValidCanvas();
        }

        // Start the GameManager registration process
        StartCoroutine(RegisterWithGameManager());
    }

    /// <summary>
    /// Find a valid Canvas to spawn notifications
    /// </summary>
    private void FindValidCanvas()
    {
        // First, try to use the assigned popupParent if it's in a Canvas
        if (popupParent != null)
        {
            _targetCanvas = popupParent.GetComponentInParent<Canvas>();
            if (_targetCanvas != null)
            {
                Debug.Log($"[NotificationPopupSpawner] Using assigned parent canvas: {_targetCanvas.name}");
                return;
            }
        }

        // If findCanvasAutomatically is enabled, search for a suitable Canvas
        if (findCanvasAutomatically)
        {
            // Look for Canvas in scene
            Canvas[] canvases = FindObjectsOfType<Canvas>();

            foreach (Canvas canvas in canvases)
            {
                // Prefer Canvas that's not in DontDestroyOnLoad and has higher sortOrder
                if (canvas.gameObject.scene.name != "DontDestroyOnLoad")
                {
                    _targetCanvas = canvas;

                    // Try to find a suitable parent within this canvas
                    Transform canvasTransform = canvas.transform;

                    // Look for common UI container names
                    Transform uiContainer = canvasTransform.Find("UI") ??
                                          canvasTransform.Find("HUD") ??
                                          canvasTransform.Find("Notifications") ??
                                          canvasTransform.Find("Popups");

                    if (uiContainer != null)
                    {
                        popupParent = uiContainer;
                    }
                    else
                    {
                        // Use canvas directly as parent
                        popupParent = canvasTransform;
                    }

                    Debug.Log($"[NotificationPopupSpawner] Auto-found canvas: {canvas.name}, using parent: {popupParent.name}");
                    return;
                }
            }

            Debug.LogWarning("[NotificationPopupSpawner] No suitable Canvas found in scene!");
        }
    }

    /// <summary>
    /// Refresh canvas reference (call this when scene changes or canvas might have changed)
    /// </summary>
    public void RefreshCanvasReference()
    {
        FindValidCanvas();
    }

    /// <summary>
    /// Đăng ký với GameManager với retry mechanism
    /// </summary>
    private IEnumerator RegisterWithGameManager()
    {
        int maxAttempts = 20;
        int attempt = 0;
        float retryInterval = 0.25f;

        while (attempt < maxAttempts && !_eventRegistered)
        {
            if (GameManager.Ins != null)
            {
                try
                {
                    GameManager.Ins.OnIconNotificationChanged += OnIconNotificationChanged;
                    _eventRegistered = true;
                    Debug.Log("[NotificationPopupSpawner] ✓ Successfully registered with GameManager");
                    break;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[NotificationPopupSpawner] Failed to register with GameManager (attempt {attempt + 1}): {ex.Message}");
                }
            }

            attempt++;
            yield return new WaitForSeconds(retryInterval);
        }

        if (!_eventRegistered)
        {
            Debug.LogError("[NotificationPopupSpawner] Failed to register with GameManager after all attempts - notifications will not work");
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký an toàn
        if (_eventRegistered && GameManager.Ins != null)
        {
            try
            {
                GameManager.Ins.OnIconNotificationChanged -= OnIconNotificationChanged;
                _eventRegistered = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NotificationPopupSpawner] Error unregistering from GameManager: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xử lý khi có PointRed được bật lên
    /// </summary>
    private void OnIconNotificationChanged(IconType iconType)
    {
        if (!enablePointRedNotifications) return;

        // Kiểm tra an toàn GameManager
        if (GameManager.Ins == null) return;

        try
        {
            // Chỉ spawn popup khi PointRed được BẬT (không phải tắt)
            bool isNotificationVisible = GameManager.Ins.GetIconNotification(iconType);

            if (isNotificationVisible)
            {
                SpawnPointRedNotification(iconType);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NotificationPopupSpawner] Error handling notification change for {iconType}: {ex.Message}");
        }
    }

    /// <summary>
    /// Spawn popup thông báo cho PointRed cụ thể
    /// </summary>
    private void SpawnPointRedNotification(IconType iconType)
    {
        string message = GetNotificationMessage(iconType);
        Sprite icon = GetNotificationIcon(iconType);

        Enqueue(message, icon);
    }

    /// <summary>
    /// Lấy tin nhắn thông báo theo loại icon
    /// </summary>
    private string GetNotificationMessage(IconType iconType)
    {
        return iconType switch
        {
            IconType.Score => "Bạn có điểm mới!",
            IconType.Task => "Bạn có nhiệm vụ mới!",
            IconType.Balo => "Bạn có ghi chú mới trong balo!",
            IconType.Player => "Thông tin sinh viên đã cập nhật!",
            _ => "Bạn có thông báo mới!"
        };
    }

    /// <summary>
    /// Lấy icon thông báo theo loại
    /// </summary>
    private Sprite GetNotificationIcon(IconType iconType)
    {
        return iconType switch
        {
            IconType.Score => scoreIcon,
            IconType.Task => taskIcon,
            IconType.Balo => baloIcon,
            IconType.Player => playerIcon,
            _ => null
        };
    }

    /// <summary>
    /// Thêm một thông báo mới vào hàng đợi
    /// </summary>
    public void Enqueue(string message, Sprite icon = null)
    {
        if (!popupPrefab || string.IsNullOrWhiteSpace(message))
            return;

        // Ensure we have a valid canvas before enqueuing
        if (_targetCanvas == null || popupParent == null)
        {
            RefreshCanvasReference();

            if (_targetCanvas == null)
            {
                Debug.LogWarning("[NotificationPopupSpawner] No valid Canvas found - notification will not be displayed");
                return;
            }
        }

        _queue.Enqueue((message, icon));
        if (!_running)
            StartCoroutine(Run());
    }

    /// <summary>
    /// Trigger thủ công PointRed notification (cho testing hoặc external call)
    /// </summary>
    public void TriggerPointRedNotification(IconType iconType)
    {
        if (!enablePointRedNotifications) return;

        SpawnPointRedNotification(iconType);
    }

    /// <summary>
    /// Trigger thủ công với message custom
    /// </summary>
    public void TriggerCustomNotification(string message, Sprite icon = null)
    {
        Enqueue(message, icon);
    }

    /// <summary>
    /// Force re-register với GameManager (cho trường hợp mất kết nối)
    /// </summary>
    public void ForceReregisterWithGameManager()
    {
        if (_eventRegistered && GameManager.Ins != null)
        {
            try
            {
                GameManager.Ins.OnIconNotificationChanged -= OnIconNotificationChanged;
            }
            catch { }
        }

        _eventRegistered = false;
        StartCoroutine(RegisterWithGameManager());
    }

    private System.Collections.IEnumerator Run()
    {
        _running = true;
        while (_queue.Count > 0)
        {
            var (msg, icon) = _queue.Dequeue();

            // Double-check canvas and parent before spawning
            if (_targetCanvas == null || popupParent == null)
            {
                RefreshCanvasReference();
            }

            var parent = popupParent ? popupParent : transform;

            // Spawn prefab
            var popupObj = Instantiate(popupPrefab, parent, false);

            // Căn top-center cho UI
            var rt = popupObj.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchorMin = new Vector2(0.52f, 1f);
                rt.anchorMax = new Vector2(0.52f, 1f);
                rt.pivot = new Vector2(0.5f, 0.4f);
                rt.anchoredPosition = new Vector2(0, -50f);
            }

            // Nếu prefab có component NotificationPopupUI thì gọi Setup
            var popupUI = popupObj.GetComponent<NotificationPopupUI>();
            if (popupUI != null)
                popupUI.Setup(msg, icon);

            yield return new WaitForSeconds(delayBetween);
        }
        _running = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Popup")]
    private void _Test() => Enqueue("Bạn có thông báo mới!");

    [ContextMenu("Test Score PointRed")]
    private void _TestScorePointRed() => TriggerPointRedNotification(IconType.Score);

    [ContextMenu("Test Task PointRed")]
    private void _TestTaskPointRed() => TriggerPointRedNotification(IconType.Task);

    [ContextMenu("Test Balo PointRed")]
    private void _TestBaloPointRed() => TriggerPointRedNotification(IconType.Balo);

    [ContextMenu("Test Player PointRed")]
    private void _TestPlayerPointRed() => TriggerPointRedNotification(IconType.Player);

    [ContextMenu("Force Re-register with GameManager")]
    private void _TestForceReregister() => ForceReregisterWithGameManager();

    [ContextMenu("Refresh Canvas Reference")]
    private void _TestRefreshCanvas() => RefreshCanvasReference();

    [ContextMenu("Check Registration Status")]
    private void _TestCheckRegistration()
    {
        Debug.Log($"[NotificationPopupSpawner] Event registered: {_eventRegistered}");
        Debug.Log($"[NotificationPopupSpawner] GameManager.Ins available: {GameManager.Ins != null}");
        Debug.Log($"[NotificationPopupSpawner] Queue count: {_queue.Count}");
        Debug.Log($"[NotificationPopupSpawner] Running: {_running}");
        Debug.Log($"[NotificationPopupSpawner] Target Canvas: {(_targetCanvas != null ? _targetCanvas.name : "NULL")}");
        Debug.Log($"[NotificationPopupSpawner] Popup Parent: {(popupParent != null ? popupParent.name : "NULL")}");
    }
#endif
}