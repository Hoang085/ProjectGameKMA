using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuizGameManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text titleText;          // optional: "Câu hỏi kiểm tra…"
    public TMP_Text questionText;       // ô hiển thị câu hỏi
    public Button[] answerButtons;      // 4 nút A,B,C,D theo thứ tự
    public TMP_Text resultText;         // text báo đúng/sai (ẩn ban đầu)
    public GameObject root;             // Panel tổng (để SetActive)

    [Header("Config")]
    [Min(1)] public int questionsPerSession = 3;
    public string subjectKey = "ToanCaoCap";   // trùng tên file JSON trong Resources/Quiz
    public bool pauseGameDuringQuiz = true;    // Tạm dừng game khi làm quiz

    [Header("Events")]
    public Action<int, int> OnQuizCompleted;   // (correctCount, totalCount)

    // runtime
    private QuizFile quizFile;
    private List<int> sessionIndices;
    private int sessionCursor;
    private int currentIdx;
    private int correctAnswers = 0;
    private float previousTimeScale;
    private Coroutine delayCoroutine; // **MỚI: Để theo dõi coroutine đang chạy**

    void Awake()
    {
        if (!root) root = gameObject;
        resultText?.gameObject.SetActive(false);
        foreach (var b in answerButtons) b.onClick.RemoveAllListeners();
        root.SetActive(false); // ẩn panel cho tới khi mở
    }

    // Gọi hàm này khi bấm nút "Điểm danh & Học"
    public void StartQuiz(string overrideSubjectKey = null)
    {
        // **QUAN TRỌNG: Đảm bảo GameObject này đang active để có thể chạy Coroutine**
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            Debug.Log("[Quiz] Activated QuizGameManager GameObject");
        }
        
        if (!string.IsNullOrWhiteSpace(overrideSubjectKey))
            subjectKey = overrideSubjectKey;

        // Load JSON
        var ta = Resources.Load<TextAsset>($"Quiz/{subjectKey}");
        if (!ta) { Debug.LogError($"[Quiz] Missing Resources/Quiz/{subjectKey}.json"); return; }

        var hash = $"{ta.text.Length}_{ta.text.GetHashCode()}";
        quizFile = JsonUtility.FromJson<QuizFile>(ta.text);
        if (quizFile == null || quizFile.questions == null || quizFile.questions.Count == 0)
        {
            Debug.LogError($"[Quiz] JSON invalid/empty for {subjectKey}");
            return;
        }

        // đảm bảo thứ tự & lấy 3 câu cho buổi này
        QuizProgressStore.EnsureOrder(subjectKey, quizFile.questions.Count, hash);
        sessionIndices = QuizProgressStore.TakeNext(subjectKey, questionsPerSession);
        sessionCursor = 0;
        correctAnswers = 0;

        // **SỬA: CHƯA pause game ngay, đợi UI hiển thị xong đã**
        if (pauseGameDuringQuiz)
        {
            previousTimeScale = Time.timeScale;
            Debug.Log("[Quiz] Will pause game after UI is shown");
        }

        root.SetActive(true);
        resultText?.gameObject.SetActive(false);
        ShowCurrent();
        
        // **PAUSE GAME SAU KHI UI ĐÃ HIỂN THỊ (delay ngắn)**
        if (pauseGameDuringQuiz)
        {
            StartCoroutine(PauseGameAfterUIShown(0.1f));
        }
    }
    
    /// <summary>
    /// **MỚI: Pause game sau khi UI đã hiển thị để tránh bị đơ**
    /// </summary>
    private IEnumerator PauseGameAfterUIShown(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 0f;
        Debug.Log("[Quiz] Game paused after UI shown");
    }

    void ShowCurrent()
    {
        currentIdx = sessionIndices[sessionCursor];
        var q = quizFile.questions[currentIdx];

        if (titleText) titleText.text = $"Câu hỏi kiểm tra quá trình học ({sessionCursor + 1}/{questionsPerSession})";
        questionText.text = q.question;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int choice = i;
            var btn = answerButtons[i];
            var label = btn.GetComponentInChildren<TMP_Text>();
            label.text = q.answers[i];

            btn.interactable = true;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnChoose(choice));
        }
    }

    void OnChoose(int choice)
    {
        var q = quizFile.questions[currentIdx];
        bool correct = choice == q.correctIndex;

        if (correct) correctAnswers++;

        if (resultText)
        {
            resultText.text = correct ? "✓ Chính xác!" : $"✗ Sai — Đáp án đúng: {(char)('A' + q.correctIndex)}";
            resultText.gameObject.SetActive(true);
        }

        foreach (var b in answerButtons) b.interactable = false;

        // **SỬA: Dùng Coroutine với unscaled time thay vì Invoke**
        if (delayCoroutine != null) StopCoroutine(delayCoroutine);
        delayCoroutine = StartCoroutine(DelayedNextStep(1.2f));
    }

    // **MỚI: Coroutine delay sử dụng unscaled time**
    private IEnumerator DelayedNextStep(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        NextStep();
    }

    void NextStep()
    {
        resultText?.gameObject.SetActive(false);
        sessionCursor++;

        if (sessionCursor < questionsPerSession)
        {
            ShowCurrent();
        }
        else
        {
            EndSession();
        }
    }

    void EndSession()
    {
        questionText.text = $"✓ Bạn đã hoàn thành buổi học hôm nay!\n\nĐiểm: {correctAnswers}/{questionsPerSession}";
        if (titleText) titleText.text = "Hoàn thành!";
        
        foreach (var b in answerButtons) b.gameObject.SetActive(false);

        // Trigger event
        OnQuizCompleted?.Invoke(correctAnswers, questionsPerSession);
        Debug.Log($"[Quiz] Completed - Score: {correctAnswers}/{questionsPerSession}");

        // **SỬA: Dùng Coroutine với unscaled time thay vì Invoke**
        if (delayCoroutine != null) StopCoroutine(delayCoroutine);
        delayCoroutine = StartCoroutine(DelayedCloseQuiz(2f));
    }

    // **MỚI: Coroutine delay cho việc đóng quiz**
    private IEnumerator DelayedCloseQuiz(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        CloseQuiz();
    }

    public void CloseQuiz()
    {
        Debug.Log("[Quiz] Closing quiz and resuming game...");
        
        // **MỚI: Stop coroutine nếu đang chạy**
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
            delayCoroutine = null;
        }
        
        // **QUAN TRỌNG: Resume game TRƯỚC KHI tắt GameObject**
        if (pauseGameDuringQuiz)
        {
            Time.timeScale = previousTimeScale;
            Debug.Log("[Quiz] Game resumed - player can move now");
        }
        
        // **QUAN TRỌNG: Đóng dialogue TRƯỚC KHI tắt GameObject**
        if (GameUIManager.Ins != null)
        {
            GameUIManager.Ins.CloseDialogue();
            Debug.Log("[Quiz] Closed dialogue in GameUIManager");
        }
        
        // Reset nút để lần sau hiện lại
        foreach (var b in answerButtons) b.gameObject.SetActive(true);
        
        // **CUỐI CÙNG: Tắt UI (phải làm cuối cùng vì sau đó script sẽ inactive)**
        if (root != gameObject)
        {
            root.SetActive(false);
        }
        gameObject.SetActive(false);
    }

    // Force close nếu cần (cho button đóng thủ công)
    public void ForceCloseQuiz()
    {
        // **SỬA: Stop coroutine thay vì CancelInvoke**
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
            delayCoroutine = null;
        }
        CloseQuiz();
    }
}
