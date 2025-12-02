using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCFriendInteraction : MonoBehaviour
{
    [Header("UI Tham chiếu")]
    public GameObject interactPlayingUI;
    public GameObject dialogueFriendUI;

    [Header("UI Controls - Setup Nút Bấm")]
    public Button playGameButton;
    public Button closeGameButton;
    public string targetGameScene = "Game1";

    [Header("UI Text Bên Trong Dialogue")]
    public Text titleText;
    public Text contentText;

    [Header("Dữ liệu hội thoại (nhập trong Inspector)")]
    public string npcDisplayName;
    [TextArea(3, 10)]
    public string npcDialogueContent;

    [Header("Player Setup")]
    public string playerTag = "Player";

    [Header("Stamina Requirements")]
    [SerializeField] private int minStaminaRequired = 30;
    [SerializeField] private string notEnoughStaminaMessage = "Không thể chơi! Cậu cần {0} thể lực.";
    [SerializeField] private string staminaSaveKey = "PLAYER_STAMINA";
    [SerializeField] private int maxStamina = 100;

    [Header("Phần thưởng (Friendly Point)")]
    [SerializeField] private bool addFriendlyPoint = true;
    [SerializeField] private int friendlyPointReward = 5;

    private bool _playerInRange = false;
    private bool _dialogueOpen = false;

    public bool IsDialogueOpen => _dialogueOpen;

    private void Start()
    {
        if (interactPlayingUI != null)
            interactPlayingUI.SetActive(false);

        if (dialogueFriendUI != null)
            dialogueFriendUI.SetActive(false);
    }

    private void Update()
    {
        if (_playerInRange && Input.GetKeyDown(KeyCode.F) && !_dialogueOpen)
        {
            OpenDialogue();
        }

        if (_dialogueOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
        {
            OnCloseDialog();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInRange = true;
            if (interactPlayingUI != null && !_dialogueOpen)
                interactPlayingUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInRange = false;
            if (interactPlayingUI != null)
                interactPlayingUI.SetActive(false);
            if (_dialogueOpen)
                OnCloseDialog();
        }
    }

    private void OpenDialogue()
    {
        _dialogueOpen = true;

        if (interactPlayingUI != null)
            interactPlayingUI.SetActive(false);

        if (dialogueFriendUI != null)
            dialogueFriendUI.SetActive(true);

        if (titleText != null)
            titleText.text = npcDisplayName;

        if (contentText != null)
            contentText.text = npcDialogueContent;

        // 🔹 CHỈ NPC đang mở hộp thoại mới gắn listener cho nút
        if (playGameButton != null)
        {
            playGameButton.onClick.RemoveAllListeners();
            playGameButton.onClick.AddListener(OnPlayGameBtnClick);
        }

        if (closeGameButton != null)
        {
            closeGameButton.onClick.RemoveAllListeners();
            closeGameButton.onClick.AddListener(OnCloseGameBtnClick);
        }

        if (GameUIManager.Ins != null)
            GameUIManager.Ins.IsAnyStatUIOpen = true;

        Debug.Log("[NPCFriendInteraction] Dialogue opened");
    }

    public void OnCloseDialog()
    {
        if (!_dialogueOpen) return;

        _dialogueOpen = false;

        if (dialogueFriendUI != null)
            dialogueFriendUI.SetActive(false);

        if (interactPlayingUI != null && _playerInRange)
            interactPlayingUI.SetActive(true);

        if (GameUIManager.Ins != null)
            GameUIManager.Ins.IsAnyStatUIOpen = false;

        // 🔹 Option: clear luôn listener, để NPC khác set lại khi mở
        if (playGameButton != null)
            playGameButton.onClick.RemoveAllListeners();

        if (closeGameButton != null)
            closeGameButton.onClick.RemoveAllListeners();

        Debug.Log("[NPCFriendInteraction] Dialogue closed");
    }


    public void OnPlayGameBtnClick()
    {
        if (string.IsNullOrEmpty(targetGameScene))
        {
            Debug.LogError("[NPCFriendInteraction] Chưa nhập tên Scene (targetGameScene) trong Inspector!");
            return;
        }

        // **MỚI: KIỂM TRA STAMINA TRƯỚC KHI CHO PHÉP CHƠI**
        if (!HasEnoughStamina())
        {
            // **ĐÓNG DIALOG TRƯỚC KHI HIỂN THỊ NOTIFICATION**
            OnCloseDialog();
            ShowNotEnoughStaminaNotification();
            return;
        }

        if (addFriendlyPoint && GameManager.Ins != null)
        {
            GameManager.Ins.AddFriendlyPoint(friendlyPointReward);
            Debug.Log($"[NPCFriendInteraction] Đã cộng {friendlyPointReward} điểm thân thiện trước khi vào game.");
        }

        Debug.Log($"[NPCFriendInteraction] Chuẩn bị vào {targetGameScene}...");
        GameStateManager.SavePreExamState(targetGameScene);
        OnCloseDialog();
        SceneLoader.Load(targetGameScene);
    }

    /// <summary>
    /// **MỚI: Kiểm tra xem người chơi có đủ thể lực không**
    /// </summary>
    private bool HasEnoughStamina()
    {
        int currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        bool hasEnough = currentStamina >= minStaminaRequired;
        Debug.Log($"[NPCFriendInteraction] Stamina check: {currentStamina}/{maxStamina} (required: {minStaminaRequired}) = {hasEnough}");
        return hasEnough;
    }
    
    /// <summary>
    /// **MỚI: Hiển thị thông báo không đủ thể lực**
    /// </summary>
    private void ShowNotEnoughStaminaNotification()
    {
        string message = string.Format(notEnoughStaminaMessage, minStaminaRequired);
        
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.Enqueue(message);
            Debug.Log($"[NPCFriendInteraction] Đã gửi notification: {message}");
        }
        else
        {
            Debug.LogWarning("[NPCFriendInteraction] NotificationPopupSpawner không khả dụng!");
        }
    }

    public void OnCloseGameBtnClick()
    {
        OnCloseDialog();
    }
}