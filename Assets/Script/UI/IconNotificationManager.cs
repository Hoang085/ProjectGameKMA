using UnityEngine;
using System.Collections.Generic;

public class IconNotificationManager : MonoBehaviour
{
    [Header("Notification GameObjects")]
    [SerializeField] private GameObject playerNotification;
    [SerializeField] private GameObject baloNotification;
    [SerializeField] private GameObject taskNotification;
    [SerializeField] private GameObject scoreNotification;

    [Header("Settings")]
    [SerializeField] private bool animateNotifications = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseScale = 1.2f;

    private Dictionary<IconType, GameObject> notificationObjects;
    private Dictionary<IconType, bool> notificationStates;

    void Awake()
    {
        InitializeNotifications();
    }

    void Start()
    {
        // Subscribe to GameManager events
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconNotificationChanged += OnNotificationChanged;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconNotificationChanged -= OnNotificationChanged;
        }
    }

    void Update()
    {
        if (animateNotifications)
        {
            AnimateActiveNotifications();
        }
    }

    private void InitializeNotifications()
    {
        notificationObjects = new Dictionary<IconType, GameObject>
        {
            { IconType.Player, playerNotification },
            { IconType.Balo, baloNotification },
            { IconType.Task, taskNotification },
            { IconType.Score, scoreNotification }
        };

        notificationStates = new Dictionary<IconType, bool>();

        // Initially hide all notifications
        foreach (var kvp in notificationObjects)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetActive(false);
                notificationStates[kvp.Key] = false;
            }
        }
    }

    private void OnNotificationChanged(IconType iconType)
    {
        if (GameManager.Ins != null)
        {
            bool shouldShow = GameManager.Ins.GetIconNotification(iconType);
            SetNotificationVisible(iconType, shouldShow);
        }
    }

    public void SetNotificationVisible(IconType iconType, bool visible)
    {
        if (notificationObjects.ContainsKey(iconType) && notificationObjects[iconType] != null)
        {
            notificationObjects[iconType].SetActive(visible);
            notificationStates[iconType] = visible;

            Debug.Log($"[IconNotificationManager] {iconType} notification {(visible ? "shown" : "hidden")}");
        }
        else
        {
            Debug.LogWarning($"[IconNotificationManager] Notification object for {iconType} is not assigned!");
        }
    }

    private void AnimateActiveNotifications()
    {
        foreach (var kvp in notificationObjects)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy)
            {
                // Simple pulse animation
                float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f) * 0.5f;
                kvp.Value.transform.localScale = Vector3.one * scale;
            }
        }
    }

    /// <summary>
    /// Manually show notification for specific icon (for testing)
    /// </summary>
    [ContextMenu("Test Show All Notifications")]
    public void TestShowAllNotifications()
    {
        foreach (IconType iconType in System.Enum.GetValues(typeof(IconType)))
        {
            SetNotificationVisible(iconType, true);
        }
    }

    /// <summary>
    /// Manually hide all notifications (for testing)
    /// </summary>
    [ContextMenu("Test Hide All Notifications")]
    public void TestHideAllNotifications()
    {
        foreach (IconType iconType in System.Enum.GetValues(typeof(IconType)))
        {
            SetNotificationVisible(iconType, false);
        }
    }

    /// <summary>
    /// Check if any notification is currently visible
    /// </summary>
    public bool HasAnyNotificationVisible()
    {
        foreach (var state in notificationStates.Values)
        {
            if (state) return true;
        }
        return false;
    }

    /// <summary>
    /// Get notification visibility state for specific icon
    /// </summary>
    public bool IsNotificationVisible(IconType iconType)
    {
        return notificationStates.ContainsKey(iconType) && notificationStates[iconType];
    }
}