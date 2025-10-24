using UnityEngine;
using UnityEngine.SceneManagement;
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

    // trạng thái cho vòng đăng ký vô hạn + scene changes
    private Coroutine _registerRoutine;

    private void Awake()
    {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;

        // Sống xuyên scene để không mất đăng ký:
        if (transform.root == transform) DontDestroyOnLoad(gameObject);

        FindValidCanvas();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        // ràng buộc lại mỗi lần enable (hoặc lần đầu)
        TryEnsureRegistration(reset: true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_eventRegistered && GameManager.Ins != null)
        {
            try { GameManager.Ins.OnIconNotificationChanged -= OnIconNotificationChanged; }
            catch { }
            _eventRegistered = false;
        }

        if (_registerRoutine != null)
        {
            StopCoroutine(_registerRoutine);
            _registerRoutine = null;
        }
    }

    private void Start()
    {
        // bảo đảm có Canvas
        if (_targetCanvas == null) FindValidCanvas();
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Mỗi khi đổi scene: tìm Canvas mới (nếu cần) + đăng ký lại với GM
        FindValidCanvas();
        TryEnsureRegistration(reset: true);
    }

    private void TryEnsureRegistration(bool reset = false)
    {
        if (reset && _registerRoutine != null)
        {
            StopCoroutine(_registerRoutine);
            _registerRoutine = null;
        }

        if (_eventRegistered && GameManager.Ins != null) return;

        _registerRoutine ??= StartCoroutine(RegisterLoop());
    }

    /// <summary>
    /// Đăng ký với GameManager theo “vòng vô hạn” (không bỏ cuộc).
    /// Tự động khôi phục sau khi quay về GameScene.
    /// </summary>
    private IEnumerator RegisterLoop()
    {
        while (true)
        {
            if (!_eventRegistered && GameManager.Ins != null)
            {
                bool ok = false;
                try
                {
                    GameManager.Ins.OnIconNotificationChanged -= OnIconNotificationChanged; // idempotent
                    GameManager.Ins.OnIconNotificationChanged += OnIconNotificationChanged;
                    ok = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[NotificationPopupSpawner] Register failed: {ex.Message}");
                }

                if (ok)
                {
                    _eventRegistered = true;
                    Debug.Log("[NotificationPopupSpawner] ✓ Registered with GameManager");
                }
            }

            // Nếu đã đăng ký thì “ngủ” sâu hơn để đỡ tốn CPU
            yield return new WaitForSeconds(_eventRegistered ? 2f : 0.25f);

            // Nếu GM biến mất (đổi scene…), tự đánh dấu chưa đăng ký để đăng ký lại
            if (GameManager.Ins == null) _eventRegistered = false;
        }
    }

    /// <summary>
    /// Tìm Canvas hợp lệ để spawn notification
    /// </summary>
    private void FindValidCanvas()
    {
        // Ưu tiên parent được chỉ định
        if (popupParent != null)
        {
            _targetCanvas = popupParent.GetComponentInParent<Canvas>();
            if (_targetCanvas != null)
            {
                // Debug.Log($"[NotificationPopupSpawner] Using assigned parent canvas: {_targetCanvas.name}");
                return;
            }
        }

        if (!findCanvasAutomatically) return;

        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            // Ưu tiên Canvas thuộc scene hiện tại (không phải DontDestroy)
            if (canvas.gameObject.scene.handle != 0)
            {
                _targetCanvas = canvas;

                // Thử kiếm node phổ biến trong Canvas
                var ct = canvas.transform;
                var ui = ct.Find("UI") ?? ct.Find("HUD") ?? ct.Find("Notifications") ?? ct.Find("Popups");
                popupParent = ui != null ? ui : ct;

                // Debug.Log($"[NotificationPopupSpawner] Auto-found canvas: {canvas.name}, parent: {popupParent.name}");
                return;
            }
        }

        Debug.LogWarning("[NotificationPopupSpawner] No suitable Canvas found in scene!");
    }

    private void OnDestroy()
    {
        if (_eventRegistered && GameManager.Ins != null)
        {
            try { GameManager.Ins.OnIconNotificationChanged -= OnIconNotificationChanged; }
            catch (System.Exception ex) { Debug.LogWarning($"[NotificationPopupSpawner] Error unregister: {ex.Message}"); }
        }
        _eventRegistered = false;
    }

    private void OnIconNotificationChanged(IconType iconType)
    {
        if (!enablePointRedNotifications) return;
        if (GameManager.Ins == null) return;

        try
        {
            bool visible = GameManager.Ins.GetIconNotification(iconType);
            if (visible) SpawnPointRedNotification(iconType);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NotificationPopupSpawner] Handle change error {iconType}: {ex.Message}");
        }
    }

    private void SpawnPointRedNotification(IconType iconType)
    {
        Enqueue(GetNotificationMessage(iconType), GetNotificationIcon(iconType));
    }

    private string GetNotificationMessage(IconType iconType) => iconType switch
    {
        IconType.Score => "Bạn có điểm mới!",
        IconType.Task => "Bạn có nhiệm vụ mới!",
        IconType.Balo => "Bạn có ghi chú mới trong balo!",
        IconType.Player => "Thông tin sinh viên đã cập nhật!",
        _ => "Bạn có thông báo mới!"
    };

    private Sprite GetNotificationIcon(IconType iconType) => iconType switch
    {
        IconType.Score => scoreIcon,
        IconType.Task => taskIcon,
        IconType.Balo => baloIcon,
        IconType.Player => playerIcon,
        _ => null
    };

    public void Enqueue(string message, Sprite icon = null)
    {
        if (!popupPrefab || string.IsNullOrWhiteSpace(message)) return;

        if (_targetCanvas == null || popupParent == null)
        {
            FindValidCanvas();
            if (_targetCanvas == null)
            {
                Debug.LogWarning("[NotificationPopupSpawner] No valid Canvas - skip popup");
                return;
            }
        }

        _queue.Enqueue((message, icon));
        if (!_running) StartCoroutine(Run());
    }

    public void TriggerPointRedNotification(IconType iconType)
    {
        if (!enablePointRedNotifications) return;
        SpawnPointRedNotification(iconType);
    }

    public void TriggerCustomNotification(string message, Sprite icon = null)
    {
        Enqueue(message, icon);
    }

    public void ForceReregisterWithGameManager()
    {
        _eventRegistered = false;
        TryEnsureRegistration(reset: true);
    }

    private IEnumerator Run()
    {
        _running = true;
        while (_queue.Count > 0)
        {
            var (msg, icon) = _queue.Dequeue();

            if (_targetCanvas == null || popupParent == null) FindValidCanvas();

            var parent = popupParent ? popupParent : transform;
            var popupObj = Instantiate(popupPrefab, parent, false);

            var rt = popupObj.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchorMin = new Vector2(0.52f, 1f);
                rt.anchorMax = new Vector2(0.52f, 1f);
                rt.pivot = new Vector2(0.5f, 0.4f);
                rt.anchoredPosition = new Vector2(0, -50f);
            }

            var popupUI = popupObj.GetComponent<NotificationPopupUI>();
            if (popupUI != null) popupUI.Setup(msg, icon);

            yield return new WaitForSeconds(delayBetween);
        }
        _running = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Popup")] private void _Test() => Enqueue("Bạn có thông báo mới!");
    [ContextMenu("Test Score PointRed")] private void _TestScorePointRed() => TriggerPointRedNotification(IconType.Score);
    [ContextMenu("Test Task PointRed")] private void _TestTaskPointRed() => TriggerPointRedNotification(IconType.Task);
    [ContextMenu("Test Balo PointRed")] private void _TestBaloPointRed() => TriggerPointRedNotification(IconType.Balo);
    [ContextMenu("Test Player PointRed")] private void _TestPlayerPointRed() => TriggerPointRedNotification(IconType.Player);
    [ContextMenu("Force Re-register with GameManager")] private void _TestForceReregister() => ForceReregisterWithGameManager();
    [ContextMenu("Refresh Canvas Reference")] private void _TestRefreshCanvas() => FindValidCanvas();
#endif
}
