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

    private bool _playerInRange = false;
    private bool _dialogueOpen = false;

    public bool IsDialogueOpen => _dialogueOpen;

    private void Start()
    {
        if (interactPlayingUI != null)
            interactPlayingUI.SetActive(false);

        if (dialogueFriendUI != null)
            dialogueFriendUI.SetActive(false);

        if (playGameButton != null)
        {
            playGameButton.onClick.RemoveAllListeners();
            playGameButton.onClick.AddListener(OnPlayGameBtnClick);
        }
        else
        {
            Debug.LogWarning($"[NPCFriendInteraction] Chưa gán Button cho NPC: {gameObject.name}");
        }
    }

    private void OnDestroy()
    {
        if (playGameButton != null)
        {
            playGameButton.onClick.RemoveListener(OnPlayGameBtnClick);
        }
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

        Debug.Log("[NPCFriendInteraction] Dialogue closed");
    }

    public void OnPlayGameBtnClick()
    {
        if (string.IsNullOrEmpty(targetGameScene))
        {
            Debug.LogError("[NPCFriendInteraction] Chưa nhập tên Scene (targetGameScene) trong Inspector!");
            return;
        }

        Debug.Log($"[NPCFriendInteraction] Chuẩn bị vào {targetGameScene}...");
        GameStateManager.SavePreExamState(targetGameScene);
        OnCloseDialog();
        SceneLoader.Load(targetGameScene);
    }
}