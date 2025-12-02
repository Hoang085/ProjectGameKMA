using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class InteractableZone : MonoBehaviour
{
    [Header("Nhận diện Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Phím tương tác")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Sự kiện UI / logic")]
    public UnityEvent onShowPrompt;   // Ví dụ: hiện label "Nhấn F: Vào trong"
    public UnityEvent onHidePrompt;   // Ẩn label
    public UnityEvent onInteract;     // Mở UI căng tin, play SFX, v.v. (CHỈ nhận các hàm void!)

    [Header("Tích hợp hệ thống hành động sẵn có")]
    [SerializeField] private List<InteractableAction> actions = new List<InteractableAction>();

    [Header("Chống bấm lặp")]
    [SerializeField] private float interactCooldown = 0.2f;

    [Header("Video (Optional)")]
    [SerializeField] private bool playVideo = false;        // Bật/tắt phát video
    [SerializeField] private VideoProfile videoProfile;     // Video profile để phát
    [SerializeField] private VideoPopupUI videoPopup;       // Reference đến VideoPopupUI (có thể đang inactive)

    [Header("Thể lực (Stamina)")]
    [SerializeField] private bool restoreStamina = true;                // Bật/tắt tính năng hồi thể lực
    [SerializeField] private int staminaRestoreAmount = 30;             // Lượng thể lực hồi phục
    [SerializeField] private string staminaNotificationMessage = "Bạn đã được hồi thêm {0} thể lực!";
    [SerializeField] private string staminaFullMessage = "Thể lực của bạn đã đầy!";  // **MỚI: Thông báo khi đầy**
    [SerializeField] private string staminaSaveKey = "PLAYER_STAMINA";
    [SerializeField] private int maxStamina = 100;

    [Header("Chuyển ca sau khi thực hiện")]
    [SerializeField] private bool advanceSlotAfterComplete = true;
    [Tooltip("Thời gian chờ trước khi chuyển ca (giây)")]
    [SerializeField] private float delayBeforeSlotAdvance = 1f;

    private bool _playerInside;
    private float _lastInteractTime;
    private bool _waitingForVideo;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;   // bắt buộc là Trigger
    }

    private void Awake()
    {
        // Tìm kể cả khi VideoPopupUI đang inactive
        if (!videoPopup)
            videoPopup = Resources.FindObjectsOfTypeAll<VideoPopupUI>().FirstOrDefault();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _playerInside = true;

        if (actions != null)
            foreach (var a in actions) if (a) a.OnPlayerEnter();

        onShowPrompt?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _playerInside = false;

        if (actions != null)
            foreach (var a in actions) if (a) a.OnPlayerExit();

        onHidePrompt?.Invoke();
    }

    private void OnDisable()
    {
        if (_playerInside)
        {
            _playerInside = false;
            onHidePrompt?.Invoke();
        }
    }

    private void Update()
    {
        if (!_playerInside || _waitingForVideo) return;

        if (Input.GetKeyDown(interactKey) && (Time.time - _lastInteractTime) >= interactCooldown)
        {
            _lastInteractTime = Time.time;

            // Ẩn prompt khi mở UI
            onHidePrompt?.Invoke();

            // Gọi custom event: mở UI căng tin (chỉ void listeners!)
            onInteract?.Invoke();

            // Gọi các InteractableAction
            if (actions != null)
            {
                foreach (var a in actions)
                {
                    if (!a) continue;
                    a.DoInteract(null);
                }
            }

            // Ấn F lần đầu: nếu cấu hình phát video thì phát NGAY — không cần ấn lần 2
            if (playVideo && videoProfile != null)
            {
                StartCoroutine(CoPlayVideoThenRestore());
            }
            else
            {
                StartCoroutine(CoCompleteInteraction());
            }
        }
    }

    /// <summary>
    /// Tùy chọn: Hàm void để bind vào UnityEvent (nếu bạn muốn gọi phát video qua onInteract).
    /// </summary>
    public void PlayVideoNow()
    {
        if (_waitingForVideo) return;
        if (!playVideo || videoProfile == null) return;

        StartCoroutine(CoPlayVideoThenRestore());
    }

    private IEnumerator CoPlayVideoThenRestore()
    {
        // Đảm bảo có VideoPopupUI, kể cả đang inactive
        if (!videoPopup)
            videoPopup = Resources.FindObjectsOfTypeAll<VideoPopupUI>().FirstOrDefault();

        if (!videoPopup)
        {
            Debug.LogWarning($"[InteractableZone] {name}: Không tìm thấy VideoPopupUI trong scene!");
            if (restoreStamina) RestorePlayerStamina();
            AdvanceSlotIfEnabled();
            yield break;
        }

        _waitingForVideo = true;

        // BẬT GO chứa VideoPopupUI nếu đang tắt
        bool wasInactive = !videoPopup.gameObject.activeInHierarchy;
        if (wasInactive)
        {
            videoPopup.gameObject.SetActive(true);
            // Chờ end of frame để đảm bảo Awake/Start của VideoPopupUI đã chạy
            yield return new WaitForEndOfFrame();
        }

        // Phát video (gọi hàm code – KHÔNG dùng hàm trả về non-void trong UnityEvent)
        videoPopup.PlayProfile_Inspector(videoProfile);

        // Đợi video kết thúc đúng nghĩa
        yield return videoPopup.WaitUntilFinished();

        Debug.Log("[InteractableZone] Video đã kết thúc!");

        if (restoreStamina) RestorePlayerStamina();

        _waitingForVideo = false;

        // **MỚI: Chuyển ca sau khi hoàn thành**
        AdvanceSlotIfEnabled();
    }

    /// <summary>
    /// **MỚI: Hoàn thành tương tác (không có video)**
    /// </summary>
    private IEnumerator CoCompleteInteraction()
    {
        _waitingForVideo = true;

        if (restoreStamina) RestorePlayerStamina();

        yield return new WaitForSeconds(0.5f); // Chờ ngắn để hiển thị notification

        _waitingForVideo = false;

        // **MỚI: Chuyển ca sau khi hoàn thành**
        AdvanceSlotIfEnabled();
    }

    /// <summary>
    /// **MỚI: Chuyển sang ca tiếp theo nếu được bật**
    /// </summary>
    private void AdvanceSlotIfEnabled()
    {
        if (!advanceSlotAfterComplete) return;

        if (GameClock.Ins != null)
        {
            StartCoroutine(CoAdvanceSlot());
        }
        else
        {
            Debug.LogWarning("[InteractableZone] GameClock không khả dụng, không thể chuyển ca!");
        }
    }

    /// <summary>
    /// **MỚI: Coroutine chuyển ca với delay**
    /// </summary>
    private IEnumerator CoAdvanceSlot()
    {
        if (delayBeforeSlotAdvance > 0)
        {
            yield return new WaitForSeconds(delayBeforeSlotAdvance);
        }

        Debug.Log("[InteractableZone] Chuyển sang ca tiếp theo...");
        GameClock.Ins.JumpToNextSessionStart();
    }

    private void RestorePlayerStamina()
    {
        int currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        int oldStamina = currentStamina;

        currentStamina = Mathf.Clamp(currentStamina + staminaRestoreAmount, 0, maxStamina);

        PlayerPrefs.SetInt(staminaSaveKey, currentStamina);
        PlayerPrefs.Save();

        int actualRestored = currentStamina - oldStamina;

        Debug.Log($"[InteractableZone] Stamina: {oldStamina} → {currentStamina} (+{actualRestored})");
        RefreshPlayerStatsUIIfOpen();
        
        // **SỬA: Hiển thị thông báo phù hợp**
        if (actualRestored > 0)
        {
            ShowStaminaNotification(actualRestored);
        }
        else
        {
            ShowStaminaFullNotification();
        }
    }

    private void RefreshPlayerStatsUIIfOpen()
    {
        if (HHH.Common.PopupManager.Ins != null)
        {
            var popup = HHH.Common.PopupManager.Ins.GetPopup(HHH.Common.PopupName.PlayerStat);
            if (popup != null && popup.activeSelf)
            {
                var playerStatsUI = popup.GetComponent<PlayerStatsUI>();
                if (playerStatsUI != null)
                {
                    int currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
                    playerStatsUI.SetStamina(currentStamina, maxStamina);
                    Debug.Log($"[InteractableZone] Đã refresh PlayerStatsUI display");
                }
            }
        }
    }

    private void ShowStaminaNotification(int actualAmount)
    {
        if (NotificationPopupSpawner.Ins != null)
        {
            string message = string.Format(staminaNotificationMessage, actualAmount);
            NotificationPopupSpawner.Ins.Enqueue(message);
            Debug.Log($"[InteractableZone] Đã gửi notification: {message}");
        }
        else
        {
            Debug.LogWarning("[InteractableZone] NotificationPopupSpawner không khả dụng!");
        }
    }
    
    /// <summary>
    /// **MỚI: Hiển thị thông báo khi thể lực đã đầy**
    /// </summary>
    private void ShowStaminaFullNotification()
    {
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.Enqueue(staminaFullMessage);
            Debug.Log($"[InteractableZone] Đã gửi notification: {staminaFullMessage}");
        }
        else
        {
            Debug.LogWarning("[InteractableZone] NotificationPopupSpawner không khả dụng!");
        }
    }

    [ContextMenu("Test Restore Stamina")]
    public void TestRestoreStamina()
    {
        RestorePlayerStamina();
    }

    [ContextMenu("Show Current Stamina")]
    public void ShowCurrentStamina()
    {
        int current = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        Debug.Log($"[InteractableZone] Current Stamina: {current}/{maxStamina}");
    }
}
