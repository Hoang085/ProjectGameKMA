using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;

public class ExamUIManager : MonoBehaviour
{
    [Header("Deps")]
    [Tooltip("Gắn component ExamLoader (đã đọc JSON)")]
    public ExamLoader loader;

    [Header("Header")]
    public TMP_Text examTitleText;
    public TMP_Text titleSubjectText;
    public TMP_Text clockText;

    [Header("Progress")]
    public TMP_Text txtDone;
    public TMP_Text txtRemain;
    public Image barDone;

    [Header("Question")]
    public TMP_Text txtQuestion;

    [Header("Options (A-D)")]
    public Toggle[] optionToggles = new Toggle[4];
    public Text[] optionLabels = new Text[4];

    [Header("Footer")]
    public Button btnPrev;
    public Button btnNext;
    public Button btnSubmit;

    [Header("Final Exam Dialog")]
    public GameObject examPanel;
    public GameObject finalDialog;
    public TMP_Text dlgCorrectCount;
    public TMP_Text dlgTotalCount;
    public TMP_Text dlgScoreHe10;
    public TMP_Text dlgScoreHe4;
    public TMP_Text dlgResultText;
    public Button dlgCloseButton;

    // ---------- runtime ----------
    private ExamData _exam;
    private int _index;
    private int[] _userAnswers;  // -1 = chưa chọn

    // timer runtime
    private float _remain;
    private bool _running;
    private bool _submitted;

    private PointConversion _pointConversion;

    // ---------- Lifecycle ----------
    void Awake()
    {
        _pointConversion = new PointConversion();

        if (btnPrev) btnPrev.onClick.AddListener(OnPrev);
        if (btnNext) btnNext.onClick.AddListener(OnNext);
        if (btnSubmit) btnSubmit.onClick.AddListener(OnSubmit);

        // Toggle: giữ chọn đáp án hiện tại
        for (int i = 0; i < optionToggles.Length; i++)
        {
            int captured = i;
            if (optionToggles[i] != null)
            {
                optionToggles[i].onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        OnSelectOption(captured);
                    }
                    else
                    {
                        if (_userAnswers != null &&
                            _index >= 0 &&
                            _index < (_userAnswers?.Length ?? 0) &&
                            _userAnswers[_index] == captured)
                        {
                            optionToggles[captured].isOn = true;
                        }
                    }
                });
            }
        }

        if (dlgCloseButton) dlgCloseButton.onClick.AddListener(CloseFinalDialog);
        if (examPanel) examPanel.SetActive(true);
        if (finalDialog) finalDialog.SetActive(false);
    }

    void Start()
    {
        if (!loader) loader = GetComponent<ExamLoader>();
        LoadFromLoader();
    }

    void Update()
    {
        if (!_running || _submitted) return;

        _remain -= Time.deltaTime;
        if (_remain <= 0f)
        {
            _remain = 0f;
            UpdateClockLabel();
            _running = false;
            AutoSubmitWhenTimeUp();
            return;
        }
        UpdateClockLabel();
    }

    // ---------- Load & Render ----------
    public void LoadFromLoader()
    {
        if (!loader)
        {
            Debug.LogError("ExamUIManager: thiếu ExamLoader.");
            return;
        }

        _exam = loader.LoadExam();
        if (_exam == null || _exam.questions == null || _exam.questions.Length == 0)
        {
            Debug.LogError("ExamUIManager: đề rỗng hoặc không hợp lệ.");
            return;
        }

        // Chuẩn bị đáp án người dùng
        _userAnswers = new int[_exam.questions.Length];
        for (int i = 0; i < _userAnswers.Length; i++) _userAnswers[i] = -1;

        // Header
        if (examTitleText) examTitleText.text = string.IsNullOrEmpty(_exam.examTitle) ? "Đề thi" : _exam.examTitle;
        if (titleSubjectText) titleSubjectText.text = _exam.subjectName;

        // Timer từ JSON: nếu <= 0 thì tắt đếm ngược
        _submitted = false;
        _remain = Mathf.Max(0, _exam.durationSeconds);
        _running = _exam.durationSeconds > 0;
        UpdateClockLabel();

        if (finalDialog) finalDialog.SetActive(false);

        _index = 0;
        Render();
    }

    void Render()
    {
        var total = _exam.questions.Length;
        var q = _exam.questions[_index];

        if (txtQuestion) txtQuestion.text = q.text;

        for (int i = 0; i < 4; i++)
        {
            if (i < q.options.Length)
            {
                if (optionLabels[i]) optionLabels[i].text = q.options[i];

                if (optionToggles[i])
                {
                    var tg = optionToggles[i];
                    var ev = tg.onValueChanged;
                    tg.onValueChanged = new Toggle.ToggleEvent();
                    tg.isOn = (_userAnswers[_index] == i);
                    tg.onValueChanged = ev;
                    tg.gameObject.SetActive(true);
                }
            }
            else
            {
                if (optionToggles[i]) optionToggles[i].gameObject.SetActive(false);
            }
        }

        int done = AnsweredCount();
        int remain = total - done;

        if (txtDone) txtDone.text = $"Số câu đã làm: {done}/{total}";
        if (txtRemain) txtRemain.text = $"Số câu còn lại: {remain}/{total}";
        if (barDone) barDone.fillAmount = total > 0 ? (done / (float)total) : 0f;

        if (btnPrev) btnPrev.interactable = !_submitted && _index > 0;
        if (btnNext) btnNext.interactable = !_submitted && _index < total - 1;
        if (btnSubmit) btnSubmit.interactable = !_submitted;
    }

    int AnsweredCount()
    {
        int c = 0;
        for (int i = 0; i < _userAnswers.Length; i++)
            if (_userAnswers[i] >= 0) c++;
        return c;
    }

    void UpdateClockLabel()
    {
        if (!clockText) return;

        if (_exam == null || _exam.durationSeconds <= 0)
        {
            clockText.text = "";
            return;
        }

        int total = Mathf.Max(0, Mathf.RoundToInt(_remain));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        clockText.text = h > 0 ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    void AutoSubmitWhenTimeUp()
    {
        if (_submitted) return;
        Debug.Log("Hết giờ, tự động nộp bài.");
        OnSubmit();
    }

    // ---------- Events ----------
    void OnSelectOption(int optionIndex)
    {
        if (_submitted) return;
        _userAnswers[_index] = optionIndex;
        Render();
    }

    void OnPrev()
    {
        if (_submitted) return;
        if (_index > 0) { _index--; Render(); }
    }

    void OnNext()
    {
        if (_submitted) return;
        if (_index < _exam.questions.Length - 1) { _index++; Render(); }
    }

    void OnSubmit()
    {
        if (_submitted) return;
        _submitted = true;
        _running = false;

        int correct = 0;
        for (int i = 0; i < _exam.questions.Length; i++)
            if (_userAnswers[i] == _exam.questions[i].correctIndex)
                correct++;

        float score10 = Mathf.Round((correct / (float)_exam.questions.Length) * 100f) / 10f;
        float score4 = _pointConversion.Convert10To4(score10);
        string letter = _pointConversion.LetterFrom10(score10);
        bool pass = score10 >= 4.0f;

        // ====== LƯU LỊCH SỬ VÀ CACHE ======
        string subjectKey = KeyUtil.MakeKey(_exam.subjectName);
        
        // **MỚI: Kiểm tra xem đây có phải là lần thi lại không**
        bool isRetake = PlayerPrefs.GetInt("EXAM_IS_RETAKE", 0) == 1;
        
        var attempt = new ExamAttempt
        {
            subjectKey = subjectKey,
            subjectName = _exam.subjectName,
            examTitle = string.IsNullOrEmpty(_exam.examTitle) ? "Đề thi" : _exam.examTitle,
            score10 = score10,
            score4 = score4,
            letter = letter,
            correct = correct,
            total = _exam.questions.Length,
            durationSeconds = Mathf.Max(0, _exam.durationSeconds),
            takenAtIso = DateTime.UtcNow.ToString("o"),
            takenAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            isRetake = isRetake  // **MỚI: Lưu flag thi lại**
        };
        
        ExamResultStorageFile.AddAttempt(attempt);
        ExamResultStorageFile.DebugPrintAll();
        
        // **MỚI: Lưu kết quả để hiển thị message khi về GameScene**
        PlayerPrefs.SetString("LAST_EXAM_SUBJECT_KEY", subjectKey);
        PlayerPrefs.SetString("LAST_EXAM_SUBJECT_NAME", _exam.subjectName);
        PlayerPrefs.SetFloat("LAST_EXAM_SCORE", score10);
        PlayerPrefs.SetInt("LAST_EXAM_IS_RETAKE", isRetake ? 1 : 0);
        PlayerPrefs.SetInt("LAST_EXAM_PASSED", pass ? 1 : 0);
        
        // **MỚI: Xóa flag thi lại sau khi lưu**
        PlayerPrefs.DeleteKey("EXAM_IS_RETAKE");
        PlayerPrefs.Save();
        // ===================================

        if (btnPrev) btnPrev.interactable = false;
        if (btnNext) btnNext.interactable = false;
        if (btnSubmit) btnSubmit.interactable = false;

        ShowFinalDialog(correct, _exam.questions.Length, score10, score4, pass, letter);
    }


    // ---------- Final dialog ----------
    void ShowFinalDialog(int correct, int total, float he10, float he4, bool pass, string letter)
    {
        if (dlgCorrectCount) dlgCorrectCount.text = correct.ToString();
        if (dlgTotalCount) dlgTotalCount.text = total.ToString();
        if (dlgScoreHe10) dlgScoreHe10.text = he10.ToString("0.0");
        if (dlgScoreHe4) dlgScoreHe4.text = he4.ToString("0.0");
        if (dlgResultText) dlgResultText.text = pass ? $"Đã đạt ({letter})" : $"Chưa đạt ({letter})";

        if (examPanel) examPanel.SetActive(false);
        if (finalDialog) finalDialog.SetActive(true);
    }

    void CloseFinalDialog()
    {
        if (finalDialog) finalDialog.SetActive(false);
        
        // **THÊM: Return to GameScene after closing exam**
        Debug.Log("[ExamUIManager] Closing exam and returning to GameScene...");
        StartCoroutine(ReturnToGameScene());
    }
    
    /// <summary>
    /// **MỚI: Return to GameScene with proper state management**
    /// </summary>
    private IEnumerator ReturnToGameScene()
    {
        // Set flag to indicate we should restore state after exam
        PlayerPrefs.SetInt("ShouldRestoreStateAfterExam", 1);
        PlayerPrefs.SetInt("ADVANCE_SLOT_AFTER_EXAM", 1);
        PlayerPrefs.Save();
        
        Debug.Log("[ExamUIManager] Set ShouldRestoreStateAfterExam flag - GameManager will handle restoration");
        
        // Wait a frame to ensure PlayerPrefs are saved
        yield return null;
        
        // Load GameScene
        Debug.Log("[ExamUIManager] Loading GameScene...");
        SceneLoader.Load("GameScene");
    }
}
