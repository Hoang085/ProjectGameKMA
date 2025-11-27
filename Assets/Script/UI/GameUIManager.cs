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
    [SerializeField] private Button btnScheduleIcon;
    [SerializeField] private Button btnSettingIcon; 
    [SerializeField] private Button btnTutorialIcon; 
    
    [Header("Exam Buttons (in Dialogue)")]
    [Tooltip("Nút 'Thi lại' - cho thi lại khi đã thi trượt")]
    [SerializeField] private Button btnExamAgain;

    [Header("Quiz System")]
    public QuizGameManager quizGameManager;

    [Header("Tutorial")]
    [SerializeField] private TutorialPlayer tutorialPlayer; 
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
    
    private string _cachedSubjectKey;
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

    public void OnClick_TakeExam()
    {
        if (_activeTeacher == null)
        {
            return;
        }
        _activeTeacher.UI_TakeExam();
    }
    
    public void OnClick_TakeRetakeExam()
    {
        if (_activeTeacher == null)
        {
            return;
        }
        _activeTeacher.UI_TakeRetakeExam();
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
    
    public void StartQuizForSubject(string subjectKey)
    {
        if (string.IsNullOrWhiteSpace(subjectKey))
        {
            return;
        }
        
        CloseDialogue(unbindTeacher: false);
        StartCoroutine(StartQuizImmediately(subjectKey, 0.2f));
    }
    
    private System.Collections.IEnumerator StartQuizImmediately(string subjectKey, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (quizGameManager == null)
        {
            yield break;
        }
        if (!quizGameManager.gameObject.activeInHierarchy)
        {
            quizGameManager.gameObject.SetActive(true);
            yield return null;
        }
        
        Debug.Log($"[GameUIManager] Starting quiz for subject: {subjectKey}");
        quizGameManager.OnQuizCompleted = OnQuizCompletedHandler;
        quizGameManager.OnQuizResult = OnQuizResultHandler;
        
        Debug.Log("[GameUIManager] Event handlers registered, calling StartQuiz()...");
        quizGameManager.StartQuiz(subjectKey);
        Debug.Log("[GameUIManager] StartQuiz() called successfully");
    }
    
    private void OnQuizCompletedHandler(int correctCount, int totalCount)
    {
        if (_activeTeacher != null)
        {
            // Truyền kết quả pass/fail vào CompleteClassAfterQuiz
            _activeTeacher.CompleteClassAfterQuiz(_lastQuizPassed);
        }
    }
    
    private void OnQuizResultHandler(bool passed)
    {
        _lastQuizPassed = passed;
        Debug.Log($"[GameUIManager] Quiz result: {(passed ? "PASSED" : "FAILED")}");
    }
    
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

    public void OnClick_TutorialIcon()
    {
        if (_dialogueOpen)
        {
            return;
        }
        
        ShowTutorial();
    }

    public void CloseDialogUI()
    {
        gameObject.SetActive(false);
    }

    public override void Awake()
    {
        MakeSingleton(false);

        if (GameClock.Ins != null)
            GameClock.Ins.OnTermChanged += HandleTermChanged_EOS;

        if (interactPromptRoot) interactPromptRoot.SetActive(false);
        if (dialogueRoot) dialogueRoot.SetActive(false);

        SetupIconButtonEvents();
 
        if (btnCloseDialogue != null)
            btnCloseDialogue.onClick.AddListener(OnClick_CloseDialogue);
        
        if (btnExamAgain != null)
            btnExamAgain.onClick.AddListener(OnClick_TakeRetakeExam);
    }

    void Start()
    {
        //CheckAndShowPostExamMessage();
        CheckAndShowTutorial();
    }

    void Update()
    {
        if (PlayerPrefs.GetInt("HAS_SEEN_TUTORIAL", 0) == 0)
        {
            return;
        }

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

        if (btnTutorialIcon != null)
            btnTutorialIcon.onClick.AddListener(OnClick_TutorialIcon);
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

        if (btnTutorialIcon != null)
            btnTutorialIcon.onClick.RemoveListener(OnClick_TutorialIcon);

        if (btnCloseDialogue != null)
            btnCloseDialogue.onClick.RemoveListener(OnClick_CloseDialogue);
        
        if (btnExamAgain != null)
            btnExamAgain.onClick.RemoveListener(OnClick_TakeRetakeExam);

        if (GameClock.Ins != null)
            GameClock.Ins.OnTermChanged -= HandleTermChanged_EOS;
    }

    private void HandleTermChanged_EOS()
    {
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
    
    public void CheckAndShowPostExamMessage()
    {
        if (!PlayerPrefs.HasKey("LAST_EXAM_SUBJECT_KEY"))
        {
            return;
        }
        
        string subjectKey = PlayerPrefs.GetString("LAST_EXAM_SUBJECT_KEY", "");
        string subjectName = PlayerPrefs.GetString("LAST_EXAM_SUBJECT_NAME", "");
        float score = PlayerPrefs.GetFloat("LAST_EXAM_SCORE", 0f);
        bool isRetake = PlayerPrefs.GetInt("LAST_EXAM_IS_RETAKE", 0) == 1;
        bool passed = PlayerPrefs.GetInt("LAST_EXAM_PASSED", 0) == 1;
        
        PlayerPrefs.DeleteKey("LAST_EXAM_SUBJECT_KEY");
        PlayerPrefs.DeleteKey("LAST_EXAM_SUBJECT_NAME");
        PlayerPrefs.DeleteKey("LAST_EXAM_SCORE");
        PlayerPrefs.DeleteKey("LAST_EXAM_IS_RETAKE");
        PlayerPrefs.DeleteKey("LAST_EXAM_PASSED");
        PlayerPrefs.Save();
        
        string message = "";
        
        if (isRetake)
        {
            PlayerPrefs.DeleteKey($"NEEDS_RETAKE_SCHEDULE_{subjectKey}");
            PlayerPrefs.Save();
            
            if (passed)
            {
                message = $"Chúc mừng! Em đã đạt {score:0.0} điểm trong lần thi lại môn {subjectName}.Điểm cuối cùng của em: {score:0.0}/10";
            }
            else
            {
                message = $"Rất tiếc! Em đã không đạt yêu cầu trong lần thi lại môn {subjectName} (Điểm: {score:0.0}/10).Đây là cơ hội cuối cùng của em và em đã không vượt qua.";
            }
        }
        else
        {
            if (passed)
            {
                message = $"Chúc mừng! Em đã hoàn thành kỳ thi môn {subjectName} với điểm {score:0.0}/10.";
            }
            else
            {
                message = $"Em đã thi trượt môn {subjectName} với điểm {score:0.0}/10 (Yêu cầu: >= 4.0).";
                string retakeTimeInfo = GetRetakeScheduleInfo(subjectKey);
                if (string.IsNullOrEmpty(retakeTimeInfo))
                {
                    Debug.Log($"[GameUIManager] Lịch thi lại chưa có trong PlayerPrefs, tính toán từ SemesterConfig...");
                    retakeTimeInfo = GetNextSessionDescription(subjectKey, subjectName);
                }
                
                if (!string.IsNullOrEmpty(retakeTimeInfo) && retakeTimeInfo != "Chưa xác định" && retakeTimeInfo != "Chưa có lịch (Hết môn)")
                {
                    message += $" Lịch thi lại: {retakeTimeInfo}";
                }
                else
                {
                    message += "Em sẽ có cơ hội thi lại. Lịch thi lại sẽ được tự động tạo vào ca học tiếp theo của môn này.";
                    message += "Xem chi tiết lịch thi lại trong Bảng lịch thi sau khi lịch được tạo.";
                }
                
                PlayerPrefs.SetInt($"NEEDS_RETAKE_SCHEDULE_{subjectKey}", 1);
                PlayerPrefs.Save();
                
                Debug.Log($"[GameUIManager] ✓ Đã set flag NEEDS_RETAKE_SCHEDULE_{subjectKey} và tính toán lịch thi lại: {retakeTimeInfo}");
            }
        }
        
        StartCoroutine(ShowPostExamMessageDelayed(message));
    }

    private IEnumerator ShowPostExamMessageDelayed(string message)
    {
        yield return new WaitForSecondsRealtime(0.5f);
        OpenDialogue("Kết quả thi", message);
    }
    
    private string GetRetakeScheduleInfo(string subjectKey)
    {
        if (GameClock.Ins == null) return null;
        
        int currentTerm = GameClock.Ins.Term;
        string keyPrefix = $"T{currentTerm}_RETAKE_{subjectKey}";

        if (!PlayerPrefs.HasKey(keyPrefix + "_day") || !PlayerPrefs.HasKey(keyPrefix + "_slot1Based"))
        {
            return null;
        }
        
        int week = PlayerPrefs.GetInt(keyPrefix + "_week", 0);
        Weekday day = (Weekday)PlayerPrefs.GetInt(keyPrefix + "_day");
        int slot = PlayerPrefs.GetInt(keyPrefix + "_slot1Based");
        
        string dayVN = DataKeyText.VN_Weekday(day);
        int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slot));
        string timeStr = DataKeyText.FormatHM(startMin);
        
        return $"{dayVN} - Ca {slot} ({timeStr}) - Tuần {week}";
    }
    
    private SemesterConfig FindSemesterConfigForTerm(int term)
    {
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        foreach (var teacher in teachers)
        {
            if (teacher.semesterConfig != null && teacher.semesterConfig.Semester == term)
            {
                return teacher.semesterConfig;
            }
        }    
        return null;
    }
    
    private string GetNextSessionDescription(string subjectKey, string subjectName)
    {
        if (GameClock.Ins == null) return "Chưa xác định";

        int currentTerm = GameClock.Ins.Term;
        var config = FindSemesterConfigForTerm(currentTerm);
        
        if (config == null || config.Subjects == null)
        {
            Debug.LogWarning("[GameUIManager] Không tìm thấy SemesterConfig cho kỳ " + currentTerm);
            return "Chưa xác định";
        }

        int curWeek = GameClock.Ins.Week;
        int curDayInt = (int)GameClock.Ins.Weekday;
        int curSlot = GameClock.Ins.SlotIndex1Based;

        SubjectData targetSubject = null;
        foreach (var subj in config.Subjects)
        {
            if (subj == null || string.IsNullOrWhiteSpace(subj.Name)) continue;
            
            if (ScheduleResolver.NameEquals(subj.Name, subjectName))
            {
                targetSubject = subj;
                break;
            }
        }

        if (targetSubject == null || targetSubject.Sessions == null || targetSubject.Sessions.Length == 0)
        {
            Debug.LogWarning($"[GameUIManager] Không tìm thấy môn {subjectName} trong SemesterConfig");
            return "Chưa xác định";
        }

        int maxWeeks = config.Weeks;
        
        for (int week = curWeek; week <= maxWeeks; week++)
        {
            int startDay = (week == curWeek) ? curDayInt : (int)Weekday.Mon;
            
            for (int d = startDay; d <= (int)Weekday.Sun; d++)
            {
                int startSlot = (week == curWeek && d == curDayInt) ? (curSlot + 1) : 1;
                
                for (int s = startSlot; s <= 5; s++)
                {
                    foreach (var session in targetSubject.Sessions)
                    {
                        if (session == null) continue;
                        if (ScheduleResolver.TryParseWeekday(session.Day, out Weekday sessionDay))
                        {
                            if ((int)sessionDay == d && session.Slot == s)
                            {
                                return FormatSessionString(sessionDay, s, week);
                            }
                        }
                    }
                }
            }
        }
        
        Debug.LogWarning($"[GameUIManager] Không tìm thấy ca nào trong tương lai cho môn {subjectName}");
        return "Chưa có lịch (Hết môn)";
    }
    
    /// <summary>
    /// Format chuỗi hiển thị thông tin ca học
    /// </summary>
    private string FormatSessionString(Weekday day, int slot, int week)
    {
        string dayVN = DataKeyText.VN_Weekday(day);
        int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(slot));
        string timeStr = DataKeyText.FormatHM(startMin);
        return $"{dayVN} - Ca {slot} ({timeStr}) - Tuần {week}";
    }
    
    /// <summary>
    /// Kiểm tra và hiển thị tutorial nếu là người chơi mới
    /// </summary>
    private void CheckAndShowTutorial()
    {
        // Kiểm tra xem người chơi đã xem tutorial chưa
        bool hasSeenTutorial = PlayerPrefs.GetInt("HAS_SEEN_TUTORIAL", 0) == 1;
        
        if (!hasSeenTutorial)
        {
            // Delay nhỏ để UI load xong
            StartCoroutine(ShowTutorialDelayed(0.1f));
        }
    }
    
    /// <summary>
    /// Hiển thị tutorial với delay
    /// </summary>
    private IEnumerator ShowTutorialDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        ShowTutorial();
    }
    
    /// <summary>
    /// Hiển thị tutorial
    /// </summary>
    public void ShowTutorial()
    {
        if (tutorialPlayer == null)
        {
            Debug.LogWarning("[GameUIManager] TutorialPlayer chưa được gán!");
            return;
        }
        
        // Đánh dấu tutorial đang hiển thị NGAY LẬP TỨC để block popups
        // Kích hoạt tutorial
        tutorialPlayer.gameObject.SetActive(true);
        
        Debug.Log("[GameUIManager] ✓ Đã hiển thị tutorial");
    }
    
    /// <summary>
    /// Cập nhật hiển thị nút thi (Thi lại) - được gọi từ TeacherAction
    /// </summary>
    public void UpdateExamButtonsVisibility(bool showRetake)
    {
        if (btnExamAgain != null)
        {
            btnExamAgain.gameObject.SetActive(showRetake);
        }
    }
}