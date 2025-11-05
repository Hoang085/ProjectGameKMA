using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuizGameManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text titleText;
    public TMP_Text questionText;
    public Button[] answerButtons;
    public TMP_Text resultText;
    public GameObject root;

    [Header("Config")]
    [Min(1)] public int questionsPerSession = 3;
    public string subjectKey = "ToanCaoCap";
    public bool pauseGameDuringQuiz = true;

    [Header("Events")]
    public Action<int, int> OnQuizCompleted;
    public Action<bool> OnQuizResult; // MỚI: true = đạt (≥2 câu), false = không đạt (< 2 câu)

    [Header("Thresholds")]
    [Tooltip("Số câu đúng tối thiểu để hoàn thành buổi học (mặc định = 2)")]
    [Min(1)] public int minCorrectToPass = 2;

    private QuizFile quizFile;
    private List<int> sessionIndices;
    private int sessionCursor;
    private int currentIdx;
    private int correctAnswers = 0;
    private float previousTimeScale;
    private Coroutine delayCoroutine;

    void Awake()
    {
        if (!root) root = gameObject;
        resultText?.gameObject.SetActive(false);
        foreach (var b in answerButtons) b.onClick.RemoveAllListeners();
        root.SetActive(false);
    }

    public void StartQuiz(string overrideSubjectKey = null)
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        if (!string.IsNullOrWhiteSpace(overrideSubjectKey))
            subjectKey = overrideSubjectKey;

        var ta = Resources.Load<TextAsset>($"Quiz/{subjectKey}");
        if (!ta) { return; }

        var hash = $"{ta.text.Length}_{ta.text.GetHashCode()}";
        quizFile = JsonUtility.FromJson<QuizFile>(ta.text);
        if (quizFile == null || quizFile.questions == null || quizFile.questions.Count == 0)
        {
            return;
        }

        QuizProgressStore.EnsureOrder(subjectKey, quizFile.questions.Count, hash);
        sessionIndices = QuizProgressStore.TakeNext(subjectKey, questionsPerSession);
        sessionCursor = 0;
        correctAnswers = 0;

        if (pauseGameDuringQuiz)
        {
            previousTimeScale = Time.timeScale;
        }

        root.SetActive(true);
        resultText?.gameObject.SetActive(false);
        ShowCurrent();
        
        if (pauseGameDuringQuiz)
        {
            StartCoroutine(PauseGameAfterUIShown(0.1f));
        }
    }
    
    private IEnumerator PauseGameAfterUIShown(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 0f;
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
            // Use rich text instead of Unicode symbols
            resultText.text = correct 
                ? "<color=green><size=150%></size></color> <b>Chính xác!</b>" 
                : $"<color=red><size=150%></size></color> <b>Sai</b> — Đáp án đúng: <b>{(char)('A' + q.correctIndex)}</b>";
            resultText.gameObject.SetActive(true);
        }

        foreach (var b in answerButtons) b.interactable = false;

        if (delayCoroutine != null) StopCoroutine(delayCoroutine);
        // Giảm từ 3s xuống 2s để gameplay mượt mà hơn, vẫn đủ thời gian đọc
        delayCoroutine = StartCoroutine(DelayedNextStep(1f));
    }

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
        // Đánh giá kết quả: >= minCorrectToPass câu đúng = đạt
        bool passed = correctAnswers >= minCorrectToPass;
        
        // Use rich text formatting for completion message với màu sắc tùy theo kết quả
        string resultColor = passed ? "green" : "red";
        string resultMessage = passed 
            ? "Bạn đã hoàn thành buổi học hôm nay!" 
            : $"Bạn chưa đạt yêu cầu tối thiểu {minCorrectToPass} câu đúng.\nBuổi học này được tính là <b>vắng mặt</b>.";
        
        questionText.text = $"<color={resultColor}>{resultMessage}</color>\n\n<size=120%>Điểm: <color=yellow>{correctAnswers}</color>/{questionsPerSession}</size>";
        if (titleText) titleText.text = passed ? "<color=green>Hoàn thành!</color>" : "<color=red>Chưa đạt</color>";
        
        foreach (var b in answerButtons) b.gameObject.SetActive(false);

        // Gọi event cũ (giữ tương thích ngược)
        OnQuizCompleted?.Invoke(correctAnswers, questionsPerSession);
        
        // MỚI: Gọi event mới để TeacherAction biết kết quả đạt/không đạt
        OnQuizResult?.Invoke(passed);

        if (delayCoroutine != null) StopCoroutine(delayCoroutine);
        // Tăng thời gian hiển thị kết quả cuối cùng lên 4s để đọc kỹ
        delayCoroutine = StartCoroutine(DelayedCloseQuiz(4f));
    }

    private IEnumerator DelayedCloseQuiz(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        CloseQuiz();
    }

    public void CloseQuiz()
    {
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
            delayCoroutine = null;
        }
        
        if (pauseGameDuringQuiz)
        {
            Time.timeScale = previousTimeScale;
        }
        
        foreach (var b in answerButtons) b.gameObject.SetActive(true);
        
        if (root != gameObject)
        {
            root.SetActive(false);
        }
        gameObject.SetActive(false);
    }

    public void ForceCloseQuiz()
    {
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
            delayCoroutine = null;
        }
        CloseQuiz();
    }
}
