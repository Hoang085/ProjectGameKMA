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

    [Header("Note Popup")]
    public NotePopup notePopupPrefab;
    public Transform popupParent;

    private bool _dialogueOpen;
    public bool IsDialogueOpen => _dialogueOpen; // tiện dùng ngoài

    // ==== Balo ====
    [Header("Backpack/Balo")]
    public GameObject baloPlayerUI;
    public BackpackUIManager backpackUIManager;

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

    // ==== Icon balo & nút Close trong balo ====
    public void OnClick_OpenBackpack()              // gán cho icon balo
    {
        if (!baloPlayerUI) return;
        baloPlayerUI.SetActive(true);
        // mỗi lần mở thì refresh danh sách note
        if (backpackUIManager) backpackUIManager.RefreshNoteButtons();
    }

    public void OnClick_CloseBackpack()             // gán cho nút Close trong balo
    {
        if (!baloPlayerUI) return;
        baloPlayerUI.SetActive(false);
    }

    // (tuỳ chọn) nếu thích một nút toggle
    public void OnClick_ToggleBackpack()
    {
        if (!baloPlayerUI) return;
        bool show = !baloPlayerUI.activeSelf;
        baloPlayerUI.SetActive(show);
        if (show && backpackUIManager) backpackUIManager.RefreshNoteButtons();
    }

    public override void Awake()
    {
        MakeSingleton(false);
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
        if (dialogueRoot) dialogueRoot.SetActive(false);
        if (baloPlayerUI) baloPlayerUI.SetActive(false); // balo mặc định đóng
    }

    // Gợi ý: đổi 'npcName' -> 'promptText' để hiển thị đúng nội dung prompt từ NPC
    public void ShowInteractPrompt(KeyCode key = KeyCode.None)
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

    public void OpenDialogue(string title, string content)
    {
        _dialogueOpen = true;
        HideInteractPrompt();

        if (dialogueNpcNameText) dialogueNpcNameText.text = title;
        if (dialogueContentText) dialogueContentText.text = content;

        if (dialogueRoot) dialogueRoot.SetActive(true);
    }

    public void CloseDialogue()
    {
        _dialogueOpen = false;
        if (dialogueRoot) dialogueRoot.SetActive(false);
    }

    public NotePopup GetOrCreateNotePopup()
    {
        if (NotePopup.Instance) return NotePopup.Instance;

        if (!notePopupPrefab)
        {
            Debug.LogError("[GameUIManager] notePopupPrefab chưa được gán. Kéo prefab vào GameUIManager.");
            return null;
        }

        var parent = popupParent ? popupParent : this.transform;
        var popup = Instantiate(notePopupPrefab, parent, false);
        popup.gameObject.SetActive(true); // bản thân popup có 'root' để ẩn/hiện nội dung
        return popup;
    }
}
