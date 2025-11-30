using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCFriendInteraction : MonoBehaviour
{
    [Header("UI Tham chiếu")]
    public GameObject interactPlayingUI;
    public GameObject dialogueFriendUI;

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
    private bool _dialogueOpen = false; // Track dialogue state

    // Public property to check if dialogue is open
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

        // Allow closing dialogue with ESC or right-click
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

        // Notify GameUIManager that a dialogue is open
        if (GameUIManager.Ins != null)
            GameUIManager.Ins.IsAnyStatUIOpen = true;

        Debug.Log("[NPCFriendInteraction] Dialogue opened - Camera and player movement locked");
    }

    public void OnCloseDialog()
    {
        if (!_dialogueOpen) return;

        _dialogueOpen = false;

        if (dialogueFriendUI != null)
            dialogueFriendUI.SetActive(false);

        // Re-show interact prompt if player is still in range
        if (interactPlayingUI != null && _playerInRange)
            interactPlayingUI.SetActive(true);

        // Notify GameUIManager that dialogue is closed
        if (GameUIManager.Ins != null)
            GameUIManager.Ins.IsAnyStatUIOpen = false;

        Debug.Log("[NPCFriendInteraction] Dialogue closed - Camera and player movement unlocked");
    }
}
