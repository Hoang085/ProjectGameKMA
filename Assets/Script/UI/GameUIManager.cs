using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

// Quan ly giao dien nguoi dung trong game
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

    [Header("ShowStatsUI")]
    [SerializeField] private GameObject playerUI;
    [SerializeField] private GameObject baloUI;
    [SerializeField] private GameObject taskUI;
    [SerializeField] private GameObject scoreUI;
    [SerializeField] private GameObject scheduleUI; // Thêm UI cho lịch học
    [SerializeField] private GameObject settingUI;  // Thêm UI cho cài đặt

    [Header("BtnListIcon")]
    [SerializeField] private Button btnPlayerIcon;
    [SerializeField] private Button btnBaloIcon;
    [SerializeField] private Button btnTaskIcon;
    [SerializeField] private Button btnScoreIcon;
    [SerializeField] private Button btnScheduleIcon; // Thêm button lịch học
    [SerializeField] private Button btnSettingIcon;  // Thêm button cài đặt

    [Header("Backpack/Balo")]
    public BackpackUIManager backpackUIManager;

    [Header("End Of Semester")]
    [SerializeField] private GameObject endOfSemesterNoticeObj;      // có thể để inactive trong Hierarchy
    [SerializeField] private EndOfSemesterNotice endOfSemesterNotice; // component trên object trên

    // ========== THEO DÕI TRẠNG THÁI UI ==========
    /// <summary>
    /// Kiểm tra có bất kỳ UI nào đang mở không (bao gồm cả dialogue)
    /// </summary>
    public bool IsAnyUIOpen => _dialogueOpen || IsAnyStatUIOpen;

    /// <summary>
    /// Kiểm tra có UI thống kê nào đang mở không
    /// </summary>
    public bool IsAnyStatUIOpen =>
        (playerUI != null && playerUI.activeSelf) ||
        (baloUI != null && baloUI.activeSelf) ||
        (taskUI != null && taskUI.activeSelf) ||
        (scoreUI != null && scoreUI.activeSelf);

    private TeacherAction _activeTeacher;
    public void BindTeacher(TeacherAction t) { _activeTeacher = t; }
    public void UnbindTeacher(TeacherAction t) { if (_activeTeacher == t) _activeTeacher = null; }

    // ===== UPDATED: USE TASKMANAGER INSTEAD OF TASKPLAYERUI =====
    /// <summary>
    /// Get TaskPlayerUI component (for backward compatibility)
    /// </summary>
    public TaskPlayerUI GetTaskPlayerUI()
    {
        if (taskUI != null)
        {
            return taskUI.GetComponent<TaskPlayerUI>();
        }
        return null;
    }

    /// <summary>
    /// Get task count from TaskManager (new unified source)
    /// </summary>
    public int GetActiveTaskCount()
    {
        if (TaskManager.Instance != null)
        {
            return TaskManager.Instance.GetActiveTaskCount();
        }
        return 0;
    }

    /// <summary>
    /// Check if TaskManager has pending tasks
    /// </summary>
    public bool HasPendingTasks()
    {
        if (TaskManager.Instance != null)
        {
            return TaskManager.Instance.HasPendingTasks();
        }
        return false;
    }

    /// <summary>
    /// Refresh task notification through GameManager
    /// </summary>
    public void RefreshTaskNotification()
    {
        if (GameManager.Ins != null)
        {
            GameManager.Ins.RefreshIconNotification(IconType.Task);
        }
    }

    /// <summary>
    /// Called when a new task is added - NO LONGER NEEDED (TaskManager handles this)
    /// </summary>
    public void OnTaskAdded()
    {
        // TaskManager now handles notification directly
        Debug.Log("[GameUIManager] Task added - TaskManager handles notification");
    }

    /// <summary>
    /// Called when a task is completed/removed - NO LONGER NEEDED (TaskManager handles this)
    /// </summary>
    public void OnTaskCompleted()
    {
        // TaskManager now handles notification directly
        Debug.Log("[GameUIManager] Task completed - TaskManager handles notification");
    }

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
        _activeTeacher.UI_StartClass(); // Gọi bắt đầu lớp

        // 🔐 LƯU TRẠNG THÁI TRƯỚC KHI RỜI GAMESCENE
        GameStateManager.SavePreExamState($"CLASS:{_activeTeacher.name}");

        // 🔁 Đặt flag để GameManager biết phải khôi phục khi quay về từ MiniGame
        PlayerPrefs.SetInt("ShouldRestoreStateAfterMiniGame", 1);
        PlayerPrefs.Save();

        // ⏱️ Bảo đảm không bị pause dở dang
        Time.timeScale = 1f;

        //StartCoroutine(DelayedLoadMiniGame());
    }

    private IEnumerator DelayedLoadMiniGame()
    {
        yield return new WaitForSeconds(2.5f); // chờ 2–3s tùy animation của bạn
        SceneManager.LoadScene("MiniGameScene1");
    }

    // ========== XỬ LÝ SỰ KIỆN CLICK ICON ==========
    public void OnClick_PlayerIcon()
    {
        // Ngăn click khi dialogue đang mở
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Player UI khi đang trong dialogue");
            return;
        }

        // Clear notification when icon is clicked
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconClicked(IconType.Player);
        }

        CloseAllUIs();
        if (playerUI != null)
        {
            playerUI.SetActive(true);
            Debug.Log("[GameUIManager] Đã mở Player UI");
        }
    }

    public void OnClick_BaloIcon()
    {
        // Ngăn click khi dialogue đang mở
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Balo UI khi đang trong dialogue");
            return;
        }

        // Clear notification when icon is clicked
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconClicked(IconType.Balo);
        }

        CloseAllUIs();
        if (!baloUI) return;
        baloUI.SetActive(true);
        if (backpackUIManager) backpackUIManager.RefreshNoteButtons();
        Debug.Log("[GameUIManager] Đã mở Balo UI");
    }

    public void OnClick_TaskIcon()
    {
        // Ngăn click khi dialogue đang mở
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Task UI khi đang trong dialogue");
            return;
        }

        // Clear notification when icon is clicked
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconClicked(IconType.Task);
        }

        CloseAllUIs();
        if (taskUI != null)
        {
            taskUI.SetActive(true);
            Debug.Log("[GameUIManager] Đã mở Task UI");
        }
    }

    public void OnClick_ScoreIcon()
    {
        // Ngăn click khi dialogue đang mở
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Score UI khi đang trong dialogue");
            return;
        }

        // Clear notification when icon is clicked
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconClicked(IconType.Score);
        }

        CloseAllUIs();
        if (scoreUI != null)
        {
            scoreUI.SetActive(true);
            Debug.Log("[GameUIManager] Đã mở Score UI");
        }
    }

    public void OnClick_ScheduleIcon()
    {
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Schedule UI khi đang trong dialogue");
            return;
        }
        CloseAllUIs();
        if (scheduleUI != null)
        {
            scheduleUI.SetActive(true);
            Debug.Log("[GameUIManager] Đã mở Schedule UI");
        }
    }

    public void OnClick_SettingIcon()
    {
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Setting UI khi đang trong dialogue");
            return;
        }
        CloseAllUIs();
        if (settingUI != null)
        {
            settingUI.SetActive(true);
            Debug.Log("[GameUIManager] Đã mở Setting UI");
        }
    }

    /// <summary>
    /// Đóng tất cả UI thống kê (trừ dialogue và interact prompt)
    /// </summary>
    public void CloseAllUIs()
    {
        bool taskUIWasOpen = (taskUI != null && taskUI.activeSelf);

        if (playerUI != null) playerUI.SetActive(false);
        if (baloUI != null) baloUI.SetActive(false);
        if (taskUI != null) taskUI.SetActive(false);
        if (scoreUI != null) scoreUI.SetActive(false);
        if (scheduleUI != null) scheduleUI.SetActive(false); // Đóng Schedule UI
        if (settingUI != null) settingUI.SetActive(false);   // Đóng Setting UI

        // Refresh task notification when task UI is closed
        if (taskUIWasOpen && GameManager.Ins != null)
        {
            GameManager.Ins.RefreshIconNotification(IconType.Task);
        }
    }

    public override void Awake()
    {
        MakeSingleton(false);

        // Đăng ký sự kiện đổi kỳ
        if (GameClock.Ins != null)
            GameClock.Ins.OnTermChanged += HandleTermChanged_EOS;

        // Ẩn các UI khi khởi tạo
        if (interactPromptRoot) interactPromptRoot.SetActive(false);
        if (dialogueRoot) dialogueRoot.SetActive(false);
        CloseAllUIs();

        SetupIconButtonEvents();
    }

    void Start()
    {

    }

    /// <summary>
    /// Thiết lập sự kiện click cho các nút icon
    /// </summary>
    private void SetupIconButtonEvents()
    {
        if (btnPlayerIcon != null)
            btnPlayerIcon.onClick.AddListener(OnClick_PlayerIcon);

        if (btnBaloIcon != null)
            btnBaloIcon.onClick.AddListener(OnClick_BaloIcon);

        if (btnTaskIcon != null)
            btnTaskIcon.onClick.AddListener(OnClick_TaskIcon);

        if (btnScoreIcon != null)
            btnScoreIcon.onClick.AddListener(OnClick_ScoreIcon);

        if (btnScheduleIcon != null)
            btnScheduleIcon.onClick.AddListener(OnClick_ScheduleIcon);

        if (btnSettingIcon != null)
            btnSettingIcon.onClick.AddListener(OnClick_SettingIcon);
    }

    /// <summary>
    /// Hủy đăng ký sự kiện khi destroy object để tránh memory leak
    /// </summary>
    private void OnDestroy()
    {
        if (btnPlayerIcon != null)
            btnPlayerIcon.onClick.RemoveListener(OnClick_PlayerIcon);

        if (btnBaloIcon != null)
            btnBaloIcon.onClick.RemoveListener(OnClick_BaloIcon);

        if (btnTaskIcon != null)
            btnTaskIcon.onClick.RemoveListener(OnClick_TaskIcon);

        if (btnScoreIcon != null)
            btnScoreIcon.onClick.RemoveListener(OnClick_ScoreIcon);

        if (btnScheduleIcon != null)
            btnScheduleIcon.onClick.RemoveListener(OnClick_ScheduleIcon);

        if (btnSettingIcon != null)
            btnSettingIcon.onClick.RemoveListener(OnClick_SettingIcon);

        if (GameClock.Ins != null)
            GameClock.Ins.OnTermChanged -= HandleTermChanged_EOS;
    }

    private void HandleTermChanged_EOS()
    {
        if (endOfSemesterNotice == null) return;

        // Bật object nếu đang tắt để đảm bảo script hoạt động
        if (endOfSemesterNoticeObj != null && !endOfSemesterNoticeObj.activeSelf)
            endOfSemesterNoticeObj.SetActive(true);

        int term = GameClock.Ins != null ? GameClock.Ins.Term : 1;
        endOfSemesterNotice.TryShowForTerm(term);
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