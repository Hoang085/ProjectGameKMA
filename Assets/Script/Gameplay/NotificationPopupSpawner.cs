using UnityEngine;
using System.Collections.Generic;

public class NotificationPopupSpawner : MonoBehaviour
{
    public static NotificationPopupSpawner Ins { get; private set; }

    [Header("Prefab & Parent")]
    [SerializeField] private GameObject popupPrefab; // giờ là GameObject thay vì NotificationPopupUI
    [SerializeField] private Transform popupParent;  // nếu trống thì dùng chính transform

    [Header("Queue")]
    [SerializeField, Min(0.05f)] private float delayBetween = 0.25f;

    [Header("PointRed Notification Settings")]
    [SerializeField] private bool enablePointRedNotifications = true;

    [Header("Icon-specific Settings")]
    [SerializeField] private Sprite scoreIcon;
    [SerializeField] private Sprite taskIcon;
    [SerializeField] private Sprite baloIcon;
    [SerializeField] private Sprite playerIcon;

    private readonly Queue<(string msg, Sprite icon)> _queue = new();
    private bool _running;

    private void Awake() => Ins = this;

    private void Start()
    {
        // Đăng ký lắng nghe sự kiện từ GameManager
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconNotificationChanged += OnIconNotificationChanged;
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký khi destroy
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconNotificationChanged -= OnIconNotificationChanged;
        }
    }

    /// <summary>
    /// Xử lý khi có PointRed được bật lên
    /// </summary>
    private void OnIconNotificationChanged(IconType iconType)
    {
        if (!enablePointRedNotifications) return;

        // Chỉ spawn popup khi PointRed được BẬT (không phải tắt)
        bool isNotificationVisible = GameManager.Ins?.GetIconNotification(iconType) ?? false;

        if (isNotificationVisible)
        {
            SpawnPointRedNotification(iconType);
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

        Debug.Log($"[NotificationPopupSpawner] PointRed {iconType} được bật - spawn popup: {message}");
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

    private System.Collections.IEnumerator Run()
    {
        _running = true;
        while (_queue.Count > 0)
        {
            var (msg, icon) = _queue.Dequeue();
            var parent = popupParent ? popupParent : transform;

            // Spawn prefab
            var popupObj = Instantiate(popupPrefab, parent, false);

            // Căn top-center cho UI
            var rt = popupObj.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
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
#endif
}