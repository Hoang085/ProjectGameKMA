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
    
    // **MỚI: Lưu kết quả quiz (pass/fail)**
    private bool _lastQuizPassed = true;

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
    }

    /// <summary>
    /// Called when a task is completed/removed - NO LONGER NEEDED (TaskManager handles this)
    /// </summary>
    public void OnTaskCompleted()
    {
    }

    public void OnClick_TakeExam()
    {
        if (_activeTeacher == null)
        {
            return;
        }
        _activeTeacher.UI_TakeExam();
    }

    // Bat dau lop hoc khi nhan nut
    public void OnClick_StartClass()
    {
        if (_activeTeacher == null)
        {
            return;
        }

        _activeTeacher.UI_StartClass();
    }
    
    /// <summary>
    /// **MỚI: Được gọi từ TeacherAction SAU KHI đã kiểm tra đủ điều kiện**
    /// </summary>
    public void StartQuizForSubject(string subjectKey)
    {
        if (string.IsNullOrWhiteSpace(subjectKey))
        {
            OpenDialogue("Lỗi", "Lỗi: Không tìm thấy môn học cho ca này. Vui lòng kiểm tra cấu hình.");
            return;
        }
        
        // **QUAN TRỌNG: Đóng dialogue KHÔNG unbind teacher (vẫn cần teacher để xử lý sau quiz)**
        CloseDialogue(unbindTeacher: false);
        
        // Bắt đầu Quiz sau delay ngắn
        StartCoroutine(StartQuizImmediately(subjectKey, 0.2f));
    }
    
    /// <summary>
    /// **MỚI: Bắt đầu quiz ngay lập tức khi nhấn "Điểm danh và học"**
    /// </summary>
    private System.Collections.IEnumerator StartQuizImmediately(string subjectKey, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        
        if (quizGameManager != null)
        {
            // **QUAN TRỌNG: Đăng ký event handler TRƯỚC KHI start quiz**
            quizGameManager.OnQuizCompleted = OnQuizCompletedHandler;
            quizGameManager.OnQuizResult = OnQuizResultHandler; // MỚI: Đăng ký event kết quả
            
            quizGameManager.StartQuiz(subjectKey);
        }
        else
        {
            OpenDialogue("Lỗi", "Lỗi: QuizGameManager chưa được cấu hình. Không thể bắt đầu Quiz.");
        }
    }
    
    /// <summary>
    /// **MỚI: Xử lý khi quiz hoàn thành - tiếp tục với class routine**
    /// </summary>
    private void OnQuizCompletedHandler(int correctCount, int totalCount)
    {
        if (_activeTeacher != null)
        {
            // Truyền kết quả pass/fail vào CompleteClassAfterQuiz
            _activeTeacher.CompleteClassAfterQuiz(_lastQuizPassed);
        }
    }
    
    /// <summary>
    /// **MỚI: Lưu kết quả quiz (đạt/không đạt)**
    /// </summary>
    private void OnQuizResultHandler(bool passed)
    {
        _lastQuizPassed = passed;
        Debug.Log($"[GameUIManager] Quiz result: {(passed ? "PASSED" : "FAILED")}");
    }
    
    /// <summary>
    /// Lấy subjectKey từ TeacherAction dựa trên môn học hiện tại (ca hiện tại)
    /// </summary>
    private string GetCurrentSubjectKeyFromTeacher()
    {
        if (_activeTeacher == null)
        {
            return null;
        }

        if (_activeTeacher.subjects == null || _activeTeacher.subjects.Count == 0)
        {
            return null;
        }

        if (_activeTeacher.semesterConfig != null && GameClock.Ins != null)
        {
            var today = GameClock.Ins.Weekday;
            var slot1Based = GameClock.Ins.SlotIndex1Based;

            foreach (var subj in _activeTeacher.subjects)
            {
                if (string.IsNullOrWhiteSpace(subj.subjectName)) continue;
                
                if (ScheduleResolver.IsSessionMatch(_activeTeacher.semesterConfig, subj.subjectName, today, slot1Based))
                {
                    string key = !string.IsNullOrWhiteSpace(subj.subjectKeyForNotes) 
                        ? subj.subjectKeyForNotes 
                        : MakeQuizKey(subj.subjectName);
                    
                    return key;
                }
            }
        }

        return null;
    }

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
            return;
        }

        if (GameManager.Ins != null)
            GameManager.Ins.OnIconClicked(IconType.Player);

        PopupManager.Ins.OnShowScreen(PopupName.PlayerStat);
    }

    public void OnClick_BaloIcon()
    {
        if (_dialogueOpen)
        {
            return;
        }

        if (GameManager.Ins != null)
            GameManager.Ins.OnIconClicked(IconType.Balo);

        PopupManager.Ins.OnShowScreen(PopupName.BaloPlayer);
    }

    public void OnClick_TaskIcon()
    {
        if (_dialogueOpen)
        {
            return;
        }

        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconClicked(IconType.Task);
        }

        PopupManager.Ins.OnShowScreen(PopupName.TaskPlayer);
    }

    public void OnClick_ScoreIcon()
    {
        if (_dialogueOpen)
        {
            return;
        }

        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnIconClicked(IconType.Score);
        }

        PopupManager.Ins.OnShowScreen(PopupName.ScoreSubject);
    }

    public void OnClick_ScheduleIcon()
    {
        if (_dialogueOpen)
        {
            return;
        }
        PopupManager.Ins.OnShowScreen(PopupName.ScheduleUI);
    }

    public void OnClick_SettingIcon()
    {
        if (_dialogueOpen)
        {
            return;
        }
        PopupManager.Ins.OnShowScreen(PopupName.Setting);
    }

    /// <summary>
    /// Đóng tất cả UI thống kê (trừ dialogue và interact prompt)
    /// </summary>
    public void CloseDialogUI()
    {
        gameObject.SetActive(false);
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
    public void CloseDialogue(bool unbindTeacher = false)
    {
        _dialogueOpen = false;
        if (dialogueRoot) dialogueRoot.SetActive(false);
        
        // **SỬA: Chỉ unbind teacher khi được yêu cầu rõ ràng**
        if (unbindTeacher && _activeTeacher != null)
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
        
        // **SỬA: Unbind teacher khi người dùng đóng dialogue thủ công**
        CloseDialogue(unbindTeacher: true);
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