using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : Singleton<GameUIManager>
{
    [Header("Prompt")]
    public GameObject interactPromptRoot;
    public Text interactPromptText;
    public KeyCode defaultInteractKey = KeyCode.F;

    [Header("Dialogue")]
    public GameObject dialogueRoot;
    public Text dialogueNpcNameText;
    public Text dialogueContentText;

    private bool _dialogueOpen;

    public override void Awake()
    {
        MakeSingleton(false);
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
        if (dialogueRoot) dialogueRoot.SetActive(false);
    }

    public void ShowInteractPrompt(string npcName, KeyCode key = KeyCode.None)
    {
        if (_dialogueOpen) return;
        var useKey = key == KeyCode.None ? defaultInteractKey : key;
        if (interactPromptText)
            interactPromptText.text = $"Nhấn {useKey}: Nói chuyện";
        if (interactPromptRoot) interactPromptRoot.SetActive(true);
    }

    public void HideInteractPrompt()
    {
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
    }

    public void OpenDialogue(string npcName, string content)
    {
        _dialogueOpen = true;
        HideInteractPrompt();

        if (dialogueNpcNameText) dialogueNpcNameText.text = npcName;
        if (dialogueContentText) dialogueContentText.text = content;

        if (dialogueRoot) dialogueRoot.SetActive(true);
    }

    public void CloseDialogue()
    {
        _dialogueOpen = false;
        if (dialogueRoot) dialogueRoot.SetActive(false);
    }
}
