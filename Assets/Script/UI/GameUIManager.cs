using UnityEngine;
using UnityEngine.UI;

// Quan ly giao dien nguoi dung trong game
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
    public bool IsDialogueOpen => _dialogueOpen;

    [Header("Backpack/Balo")]
    public GameObject baloPlayerUI;
    public BackpackUIManager backpackUIManager;

    private TeacherAction _activeTeacher; 
    public void BindTeacher(TeacherAction t) { _activeTeacher = t; }
    public void UnbindTeacher(TeacherAction t) { if (_activeTeacher == t) _activeTeacher = null; }

    public void OnClick_TakeExam()
    {
        if (_activeTeacher == null)
        {
            Debug.LogWarning("[GameUIManager] OnClick_TakeExam nhưng chưa có activeTeacher!");
            return;
        }
        _activeTeacher.UI_TakeExam();
    }

    // Bat dau lop hoc khi nhan nut
    public void OnClick_StartClass()
    {
        if (_activeTeacher == null)
        {
            Debug.LogWarning("[GameUIManager] OnClick_StartClass nhưng chưa có activeTeacher!");
            return;
        }
        Debug.Log($"[GameUIManager] StartClass gọi tới teacher: {_activeTeacher.name}");
        _activeTeacher.UI_StartClass(); // Goi bat dau lop
    }

    // Dong hop thoai khi nhan nut
    public void OnClick_CloseDialogue()
    {
        if (_activeTeacher != null) _activeTeacher.UI_Close();
        else CloseDialogue(); // Dong hop thoai neu khong co giao vien
    }

    // Mo balo
    public void OnClick_OpenBackpack()
    {
        if (!baloPlayerUI) return;
        baloPlayerUI.SetActive(true);
        if (backpackUIManager) backpackUIManager.RefreshNoteButtons(); // Lam moi ghi chu
    }

    // Dong balo
    public void OnClick_CloseBackpack()
    {
        if (!baloPlayerUI) return;
        baloPlayerUI.SetActive(false);
    }

    // Toggle balo (mo/dong)
    public void OnClick_ToggleBackpack()
    {
        if (!baloPlayerUI) return;
        bool show = !baloPlayerUI.activeSelf;
        baloPlayerUI.SetActive(show);
        if (show && backpackUIManager) backpackUIManager.RefreshNoteButtons();
    }

    // Khoi tao singleton, an tat ca UI
    public override void Awake()
    {
        MakeSingleton(false);
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
        if (dialogueRoot) dialogueRoot.SetActive(false);
        if (baloPlayerUI) baloPlayerUI.SetActive(false);
    }

    // Hien thi goi y tuong tac
    public void ShowInteractPrompt(KeyCode key = KeyCode.None)
    {
        if (_dialogueOpen) return;
        var useKey = key == KeyCode.None ? defaultInteractKey : key;
        if (interactPromptText)
            interactPromptText.text = $"Nhấn {useKey}: Nói chuyện";
        if (interactPromptRoot) interactPromptRoot.SetActive(true);
    }

    // An goi y tuong tac
    public void HideInteractPrompt()
    {
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
    }

    // Mo hop thoai
    public void OpenDialogue(string title, string content)
    {
        _dialogueOpen = true;
        HideInteractPrompt();
        if (dialogueNpcNameText) dialogueNpcNameText.text = title;
        if (dialogueContentText) dialogueContentText.text = content;
        if (dialogueRoot) dialogueRoot.SetActive(true);
    }

    // Dong hop thoai
    public void CloseDialogue()
    {
        _dialogueOpen = false;
        if (dialogueRoot) dialogueRoot.SetActive(false);
    }

    // Lay hoac tao popup ghi chu
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
        popup.gameObject.SetActive(true);
        return popup;
    }
}