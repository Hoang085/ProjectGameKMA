using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using HHH.Common;

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
    [SerializeField] private Button btnCloseDialogue; // Nút đóng dialogue
    
    [Header("Note Popup")]
    public NotePopup notePopupPrefab;
    public Transform popupParent;
    private bool _dialogueOpen;
    public bool IsDialogueOpen => _dialogueOpen;

    [Header("BtnListIcon")]
    [SerializeField] private Button btnPlayerIcon;
    [SerializeField] private Button btnBaloIcon;
    [SerializeField] private Button btnTaskIcon;
    [SerializeField] private Button btnScoreIcon;
    [SerializeField] private Button btnScheduleIcon; // Thêm button lịch học
    [SerializeField] private Button btnSettingIcon;  // Thêm button cài đặt

    [Header("Backpack/Balo")]
    public BackpackUIManager backpackUIManager;

    [Header("Quiz System")]
    public QuizGameManager quizGameManager;

    [Header("End Of Semester")]
    [SerializeField] private EndOfSemesterNotice endOfSemesterNotice; // component trên object trên

    // ========== THEO DÕI TRẠNG THÁI UI ==========
    /// <summary>
    /// Track number of open popups (from PopupManager)
    /// </summary>
    private int _openPopupCount = 0;
    
    /// <summary>
    /// Check if any popup is currently open
    /// </summary>
    public bool IsAnyPopupOpen => _openPopupCount > 0;
    
    /// <summary>
    /// Kiểm tra có bất kỳ UI nào đang mở không (bao gồm cả dialogue và popups)
    /// </summary>
    public bool IsAnyUIOpen => _dialogueOpen || IsAnyStatUIOpen || IsQuizOpen || IsAnyPopupOpen;

    /// <summary>
    /// Kiểm tra có UI thống kê nào đang mở không
    /// </summary>
    public bool IsAnyStatUIOpen;
    
    /// <summary>
    /// Kiểm tra có Quiz đang mở không
    /// </summary>
    public bool IsQuizOpen => quizGameManager != null && quizGameManager.gameObject.activeInHierarchy;

    private TeacherAction _activeTeacher;
    
    // **THÊM: Lưu subjectKey để sử dụng sau khi class kết thúc**
    private string _cachedSubjectKey;
    
    public void BindTeacher(TeacherAction t) { _activeTeacher = t; }
    public void UnbindTeacher(TeacherAction t) { if (_activeTeacher == t) _activeTeacher = null; }

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
        
        // **QUAN TRỌNG: Lấy và lưu subjectKey TRƯỚC KHI bắt đầu class routine**
        _cachedSubjectKey = GetCurrentSubjectKeyFromTeacher();
        
        if (string.IsNullOrWhiteSpace(_cachedSubjectKey))
        {
            Debug.LogError("[GameUIManager] Không thể lấy subjectKey từ teacher! Không thể bắt đầu lớp học.");
            OpenDialogue(_activeTeacher.titleText, "Lỗi: Không tìm thấy môn học cho ca này. Vui lòng kiểm tra cấu hình.");
            return;
        }
        
        Debug.Log($"[GameUIManager] Đã lưu subjectKey: {_cachedSubjectKey}");
        
        // **SỬA: Bắt đầu quiz NGAY LẬP TỨC thay vì đợi class routine kết thúc**
        CloseDialogue();
        
        // Bắt đầu Quiz ngay lập tức (sau delay ngắn để đóng dialogue)
        StartCoroutine(StartQuizImmediately(_cachedSubjectKey, 0.2f));
    }
    
    /// <summary>
    /// **MỚI: Bắt đầu quiz ngay lập tức khi nhấn "Điểm danh và học"**
    /// </summary>
    private System.Collections.IEnumerator StartQuizImmediately(string subjectKey, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        
        if (quizGameManager != null)
        {
            Debug.Log($"[GameUIManager] Bắt đầu Quiz ngay lập tức cho môn: {subjectKey}");
            quizGameManager.StartQuiz(subjectKey);
            
            // **SAU KHI QUIZ HOÀN THÀNH, gọi teacher để xử lý logic điểm danh**
            quizGameManager.OnQuizCompleted = OnQuizCompletedHandler;
        }
        else
        {
            Debug.LogError("[GameUIManager] QuizGameManager chưa được gán!");
            OpenDialogue("Lỗi", "Lỗi: QuizGameManager chưa được cấu hình. Không thể bắt đầu Quiz.");
        }
        
        _cachedSubjectKey = null;
    }
    
    /// <summary>
    /// **MỚI: Xử lý khi quiz hoàn thành - tiếp tục với class routine**
    /// </summary>
    private void OnQuizCompletedHandler(int correctCount, int totalCount)
    {
        Debug.Log($"[GameUIManager] Quiz completed with score: {correctCount}/{totalCount}");
        
        // **SỬA: Gọi hàm mới để hoàn thành logic học KHÔNG bao gồm quiz**
        if (_activeTeacher != null)
        {
            _activeTeacher.CompleteClassAfterQuiz();
        }
    }
    
    /// <summary>
    /// Lấy subjectKey từ TeacherAction dựa trên môn học hiện tại (ca hiện tại)
    /// </summary>
    private string GetCurrentSubjectKeyFromTeacher()
    {
        if (_activeTeacher == null)
        {
            Debug.LogError("[GameUIManager] Active teacher is null! Cannot get subject key.");
            return null;
        }

        if (_activeTeacher.subjects == null || _activeTeacher.subjects.Count == 0)
        {
            Debug.LogError($"[GameUIManager] Teacher '{_activeTeacher.name}' has no subjects configured!");
            return null;
        }

        // Tìm môn đang học ở ca hiện tại (giống logic trong TeacherAction.TryFindSubjectForNow)
        if (_activeTeacher.semesterConfig != null && GameClock.Ins != null)
        {
            var today = GameClock.Ins.Weekday;
            var slot1Based = GameClock.Ins.SlotIndex1Based;

            foreach (var subj in _activeTeacher.subjects)
            {
                if (string.IsNullOrWhiteSpace(subj.subjectName)) continue;
                
                if (ScheduleResolver.IsSessionMatch(_activeTeacher.semesterConfig, subj.subjectName, today, slot1Based))
                {
                    // Ưu tiên subjectKeyForNotes, fallback về subjectName
                    string key = !string.IsNullOrWhiteSpace(subj.subjectKeyForNotes) 
                        ? subj.subjectKeyForNotes 
                        : MakeQuizKey(subj.subjectName);
                    
                    Debug.Log($"[GameUIManager] Tìm thấy môn hiện tại: {subj.subjectName} với key: {key}");
                    return key;
                }
            }
        }
        else
        {
            if (_activeTeacher.semesterConfig == null)
                Debug.LogError($"[GameUIManager] Teacher '{_activeTeacher.name}' has no SemesterConfig!");
            if (GameClock.Ins == null)
                Debug.LogError("[GameUIManager] GameClock instance is null!");
        }

        // Không tìm thấy môn cho ca hiện tại
        Debug.LogError($"[GameUIManager] Không tìm thấy môn học nào cho ca hiện tại! " +
                      $"Teacher: {_activeTeacher.name}, " +
                      $"Day: {(GameClock.Ins != null ? GameClock.Ins.Weekday.ToString() : "N/A")}, " +
                      $"Slot: {(GameClock.Ins != null ? GameClock.Ins.SlotIndex1Based.ToString() : "N/A")}");
        return null;
    }

    /// <summary>
    /// Tạo quiz key từ subject name (loại bỏ khoảng trắng, lowercase)
    /// </summary>
    private string MakeQuizKey(string subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName)) return "ToanCaoCap";
        return subjectName.Trim().Replace(" ", "").ToLowerInvariant();
    }

    // ========== XỬ LÝ SỰ KIỆN CLICK ICON ==========
    public void OnClick_PlayerIcon()
    {
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Player UI khi đang trong dialogue");
            return;
        }

        if (GameManager.Ins != null)
            GameManager.Ins.OnIconClicked(IconType.Player);

        CloseAllUIs(); 
        PopupManager.Ins.OnShowScreen(PopupName.PlayerStat);
        Debug.Log("[GameUIManager] Đã mở PlayerStatsUI (popup)");
    }

    public void OnClick_BaloIcon()
    {
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Balo UI khi đang trong dialogue");
            return;
        }

        if (GameManager.Ins != null)
            GameManager.Ins.OnIconClicked(IconType.Balo);

        CloseAllUIs(); // đóng UI legacy
        PopupManager.Ins.OnShowScreen(PopupName.BaloPlayer);   //  mở popup Balo
        Debug.Log("[GameUIManager] Đã mở BaloPlayer (popup)");
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

        CloseAllUIs(); // đóng UI legacy
        PopupManager.Ins.OnShowScreen(PopupName.TaskPlayer);   //  mở popup Balo
        Debug.Log("[GameUIManager] Đã mở Task UI");
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

        CloseAllUIs(); // đóng UI legacy
        PopupManager.Ins.OnShowScreen(PopupName.ScoreSubject);
        Debug.Log("[GameUIManager] Đã mở Score UI");
    }

    public void OnClick_ScheduleIcon()
    {
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Schedule UI khi đang trong dialogue");
            return;
        }
        CloseAllUIs();
        PopupManager.Ins.OnShowScreen(PopupName.ScheduleUI);
        Debug.Log("[GameUIManager] Đã mở Schedule UI");
    }

    public void OnClick_SettingIcon()
    {
        if (_dialogueOpen)
        {
            Debug.Log("[GameUIManager] Không thể mở Setting UI khi đang trong dialogue");
            return;
        }
        CloseAllUIs();
        PopupManager.Ins.OnShowScreen(PopupName.Setting);
        Debug.Log("[GameUIManager] Đã mở Setting UI");

    }

    /// <summary>
    /// Đóng tất cả UI thống kê (trừ dialogue và interact prompt)
    /// </summary>
    public void CloseAllUIs()
    {
        Debug.Log("[GameUIManager] Đóng tất cả UI thống kê");
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
        
        // Đăng ký sự kiện cho nút đóng dialogue
        if (btnCloseDialogue != null)
            btnCloseDialogue.onClick.AddListener(OnClick_CloseDialogue);
    }

    void Start()
    {

    }

    void Update()
    {
        // Cho phép đóng dialogue bằng phím ESC hoặc chuột phải
        if (_dialogueOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                OnClick_CloseDialogue();
            }
        }
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
    protected override void OnDestroy()
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

        if (btnCloseDialogue != null)
            btnCloseDialogue.onClick.RemoveListener(OnClick_CloseDialogue);

        if (GameClock.Ins != null)
            GameClock.Ins.OnTermChanged -= HandleTermChanged_EOS;
    }

    private void HandleTermChanged_EOS()
    {
        if (endOfSemesterNotice == null) return;

        int term = GameClock.Ins != null ? GameClock.Ins.Term : 1;
        EndOfSemesterNotice.TryShowForTerm(term);
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
        
        // Unbind teacher nếu có
        if (_activeTeacher != null)
        {
            UnbindTeacher(_activeTeacher);
        }
    }

    /// <summary>
    /// Xử lý sự kiện click nút đóng dialogue
    /// </summary>
    public void OnClick_CloseDialogue()
    {
        if (!_dialogueOpen) return;
        
        Debug.Log("[GameUIManager] Đóng dialogue");
        CloseDialogue();
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
        var popup = Instantiate(notePopupPrefab);
        popup.gameObject.SetActive(false); 

        Transform targetParent = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (var canvas in canvases)
        {
            if (canvas.gameObject.scene.name != null &&
                canvas.gameObject.scene.name.Equals("DontDestroyOnLoad"))
            {
                targetParent = canvas.transform;
                Debug.Log($"[GameUIManager] Tìm thấy Canvas trong DontDestroyOnLoad: {canvas.name}");
                break;
            }
        }

        if (targetParent == null)
        {
            foreach (var canvas in canvases)
            {
                if (canvas.gameObject.scene.name != null &&
                    !canvas.gameObject.scene.name.Equals("DontDestroyOnLoad"))
                {
                    targetParent = canvas.transform;
                    Debug.Log($"[GameUIManager] Fallback: Tìm thấy Canvas trong scene: {canvas.name}");
                    break;
                }
            }
        }

        if (targetParent != null)
        {
            popup.transform.SetParent(targetParent, false);
            Debug.Log($"[GameUIManager] Đã gán NotePopup vào Canvas: {targetParent.name}");
        }
        else
        {
            Debug.LogWarning("[GameUIManager] Không tìm thấy Canvas, NotePopup sẽ ở root!");
        }

        RectTransform rectTransform = popup.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Đặt anchor ở giữa màn hình
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            rectTransform.anchoredPosition = new Vector2(0f, -100f);

            Debug.Log($"[GameUIManager] Đã điều chỉnh vị trí NotePopup: {rectTransform.anchoredPosition}");
        }

        popup.transform.SetAsLastSibling();
        Debug.Log($"[GameUIManager] Đã đặt NotePopup làm last sibling (index: {popup.transform.GetSiblingIndex()})");

        popup.gameObject.SetActive(true);
        return popup;
    }

    /// <summary>
    /// Called by BasePopUp when a popup is opened
    /// </summary>
    public void OnPopupOpened()
    {
        _openPopupCount++;
        Debug.Log($"[GameUIManager] Popup opened. Total open popups: {_openPopupCount}");
    }

    /// <summary>
    /// Called by BasePopUp when a popup is closed
    /// </summary>
    public void OnPopupClosed()
    {
        _openPopupCount--;
        if (_openPopupCount < 0) _openPopupCount = 0; // Safety check
        Debug.Log($"[GameUIManager] Popup closed. Total open popups: {_openPopupCount}");
    }
}