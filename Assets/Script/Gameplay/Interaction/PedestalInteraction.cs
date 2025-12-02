using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PedestalInteraction : MonoBehaviour
{
    [Header("UI hiển thị khi đứng gần")]
    public GameObject interactPlayingUI;

    [Header("Tag của Player")]
    public string playerTag = "Player";

    [Header("Phím tương tác")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Chống bấm lặp")]
    [SerializeField] private float interactCooldown = 0.2f;

    [Header("Video (Optional)")]
    [SerializeField] private bool playVideo = true;
    [SerializeField] private VideoProfile videoProfile;
    [SerializeField] private VideoPopupUI videoPopup;

    [Header("Thể lực (Stamina) - TRỪ ĐI")]
    [SerializeField] private bool consumeStamina = true;
    [SerializeField] private int staminaConsumeAmount = 30;
    [SerializeField] private string staminaNotificationMessage = "Bạn đã tiêu hao {0} thể lực!";
    [SerializeField] private string notEnoughStaminaMessage = "Không đủ thể lực để thực hiện! Bạn cần ít nhất {0} thể lực.";
    [SerializeField] private string staminaSaveKey = "PLAYER_STAMINA";
    [SerializeField] private int maxStamina = 100;

    [Header("Phần thưởng (Friendly Point)")]
    [SerializeField] private bool addFriendlyPoint = true;
    [SerializeField] private int friendlyPointReward = 10;

    [Header("Chuyển ca sau khi thực hiện")]
    [SerializeField] private bool advanceSlotAfterComplete = true;
    [Tooltip("Thời gian chờ trước khi chuyển ca (giây)")]
    [SerializeField] private float delayBeforeSlotAdvance = 1f;

    private bool _playerInside;
    private float _lastInteractTime;
    private bool _waitingForVideo;

    private void Start()
    {
        if (interactPlayingUI != null)
            interactPlayingUI.SetActive(false);
    }

    private void Awake()
    {
        // Tìm VideoPopupUI kể cả khi đang inactive
        if (!videoPopup)
            videoPopup = Resources.FindObjectsOfTypeAll<VideoPopupUI>().FirstOrDefault();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInside = true;
            if (interactPlayingUI != null)
                interactPlayingUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInside = false;
            if (interactPlayingUI != null)
                interactPlayingUI.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_playerInside)
        {
            _playerInside = false;
            if (interactPlayingUI != null)
                interactPlayingUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (!_playerInside || _waitingForVideo) return;

        if (Input.GetKeyDown(interactKey) && (Time.time - _lastInteractTime) >= interactCooldown)
        {
            _lastInteractTime = Time.time;

            // Kiểm tra đủ thể lực trước khi thực hiện
            if (consumeStamina && !HasEnoughStamina())
            {
                ShowNotEnoughStaminaNotification();
                return;
            }

            // Ẩn prompt khi tương tác
            if (interactPlayingUI != null)
                interactPlayingUI.SetActive(false);

            // Phát video hoặc trừ stamina ngay
            if (playVideo && videoProfile != null)
            {
                StartCoroutine(CoPlayVideoThenConsumeStamina());
            }
            else
            {
                StartCoroutine(CoCompleteInteraction());
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem người chơi có đủ thể lực không
    /// </summary>
    private bool HasEnoughStamina()
    {
        int currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        bool hasEnough = currentStamina >= staminaConsumeAmount;
        Debug.Log($"[PedestalInteraction] Stamina check: {currentStamina}/{maxStamina} (required: {staminaConsumeAmount}) = {hasEnough}");
        return hasEnough;
    }

    /// <summary>
    /// Phát video rồi mới trừ thể lực và chuyển ca
    /// </summary>
    private IEnumerator CoPlayVideoThenConsumeStamina()
    {
        // Đảm bảo có VideoPopupUI
        if (!videoPopup)
            videoPopup = Resources.FindObjectsOfTypeAll<VideoPopupUI>().FirstOrDefault();

        if (!videoPopup)
        {
            Debug.LogWarning($"[PedestalInteraction] {name}: Không tìm thấy VideoPopupUI trong scene!");
            if (consumeStamina) ConsumePlayerStamina();
            AdvanceSlotIfEnabled();
            yield break;
        }

        _waitingForVideo = true;

        // Bật VideoPopupUI nếu đang tắt
        bool wasInactive = !videoPopup.gameObject.activeInHierarchy;
        if (wasInactive)
        {
            videoPopup.gameObject.SetActive(true);
            yield return new WaitForEndOfFrame();
        }

        // Phát video
        videoPopup.PlayProfile_Inspector(videoProfile);

        // Đợi video kết thúc
        yield return videoPopup.WaitUntilFinished();

        Debug.Log("[PedestalInteraction] Video đã kết thúc!");

        if (consumeStamina) ConsumePlayerStamina();

        _waitingForVideo = false;

        // Chuyển ca sau khi hoàn thành
        AdvanceSlotIfEnabled();
    }

    /// <summary>
    /// Hoàn thành tương tác (không có video)
    /// </summary>
    private IEnumerator CoCompleteInteraction()
    {
        _waitingForVideo = true;

        if (consumeStamina) ConsumePlayerStamina();

        yield return new WaitForSeconds(0.5f); // Chờ ngắn để hiển thị notification

        _waitingForVideo = false;

        // Chuyển ca sau khi hoàn thành
        AdvanceSlotIfEnabled();
    }

    /// <summary>
    /// Chuyển sang ca tiếp theo nếu được bật
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
            Debug.LogWarning("[PedestalInteraction] GameClock không khả dụng, không thể chuyển ca!");
        }
    }

    /// <summary>
    /// Coroutine chuyển ca với delay
    /// </summary>
    private IEnumerator CoAdvanceSlot()
    {
        if (delayBeforeSlotAdvance > 0)
        {
            yield return new WaitForSeconds(delayBeforeSlotAdvance);
        }

        Debug.Log("[PedestalInteraction] Chuyển sang ca tiếp theo...");
        GameClock.Ins.JumpToNextSessionStart();
    }

    /// <summary>
    /// TRỪ thể lực của người chơi (ngược với InteractableZone)
    /// </summary>
    private void ConsumePlayerStamina()
    {
        int currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        int oldStamina = currentStamina;

        // Trừ thể lực
        currentStamina = Mathf.Clamp(currentStamina - staminaConsumeAmount, 0, maxStamina);

        PlayerPrefs.SetInt(staminaSaveKey, currentStamina);
        if (addFriendlyPoint && GameManager.Ins != null)
        {
            GameManager.Ins.AddFriendlyPoint(friendlyPointReward);
        }
        PlayerPrefs.Save();

        int actualConsumed = oldStamina - currentStamina;

        Debug.Log($"[PedestalInteraction] Stamina: {oldStamina} → {currentStamina} (-{actualConsumed})");
        RefreshPlayerStatsUIIfOpen();
        ShowStaminaNotification(actualConsumed);
    }

    /// <summary>
    /// Refresh PlayerStatsUI nếu đang mở
    /// </summary>
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
                    if (GameManager.Ins != null)
                    {
                        playerStatsUI.SetFriendlyPoint(GameManager.Ins.GetFriendlyPoint());
                    }
                    Debug.Log($"[PedestalInteraction] Đã refresh PlayerStatsUI display");
                }
            }
        }
    }

    /// <summary>
    /// Hiển thị thông báo thể lực bị tiêu hao
    /// </summary>
    private void ShowStaminaNotification(int actualAmount)
    {
        if (NotificationPopupSpawner.Ins != null)
        {
            string message = string.Format(staminaNotificationMessage, actualAmount);
            NotificationPopupSpawner.Ins.Enqueue(message);
            Debug.Log($"[PedestalInteraction] Đã gửi notification: {message}");
        }
        else
        {
            Debug.LogWarning("[PedestalInteraction] NotificationPopupSpawner không khả dụng!");
        }
    }

    /// <summary>
    /// Hiển thị thông báo không đủ thể lực
    /// </summary>
    private void ShowNotEnoughStaminaNotification()
    {
        string message = string.Format(notEnoughStaminaMessage, staminaConsumeAmount);
        
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.Enqueue(message);
            Debug.Log($"[PedestalInteraction] Đã gửi notification: {message}");
        }
        else
        {
            Debug.LogWarning("[PedestalInteraction] NotificationPopupSpawner không khả dụng!");
        }
    }

    [ContextMenu("Test Consume Stamina")]
    public void TestConsumeStamina()
    {
        ConsumePlayerStamina();
    }

    [ContextMenu("Show Current Stamina")]
    public void ShowCurrentStamina()
    {
        int current = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        Debug.Log($"[PedestalInteraction] Current Stamina: {current}/{maxStamina}");
    }
}
