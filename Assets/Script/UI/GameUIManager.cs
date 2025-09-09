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

    // ==== Context giáo viên đang tương tác ====
    private TeacherAction _activeTeacher;
    public void BindTeacher(TeacherAction t) { _activeTeacher = t; }
    public void UnbindTeacher(TeacherAction t) { if (_activeTeacher == t) _activeTeacher = null; }

    // ==== Các hàm để gán vào Button OnClick ====
    public void OnClick_StartClass() { _activeTeacher?.UI_StartClass(); }
    public void OnClick_CloseDialogue()
    {
        if (_activeTeacher != null) _activeTeacher.UI_Close();
        else CloseDialogue(); // fallback
    }

    public override void Awake()
    {
        MakeSingleton(false);
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
        if (dialogueRoot) dialogueRoot.SetActive(false);
    }

    // Gợi ý: đổi 'npcName' -> 'promptText' để hiển thị đúng nội dung prompt từ NPC
    public void ShowInteractPrompt(string promptText, KeyCode key = KeyCode.None)
    {
        if (_dialogueOpen) return;
        var useKey = key == KeyCode.None ? defaultInteractKey : key;
        if (interactPromptText)
            interactPromptText.text = $"Nhấn {useKey}: {promptText}";
        if (interactPromptRoot) interactPromptRoot.SetActive(true);
    }

    public void HideInteractPrompt()
    {
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
    }

    public void OpenDialogue(string title, string content)
    {
        _dialogueOpen = true;
        HideInteractPrompt();

        // KHÔNG tự override 'title' bằng môn từ thời khoá biểu – dùng đúng tham số truyền vào
        if (dialogueNpcNameText) dialogueNpcNameText.text = title;
        if (dialogueContentText) dialogueContentText.text = content;

        if (dialogueRoot) dialogueRoot.SetActive(true);
    }

    public void CloseDialogue()
    {
        _dialogueOpen = false;
        if (dialogueRoot) dialogueRoot.SetActive(false);
    }
}
