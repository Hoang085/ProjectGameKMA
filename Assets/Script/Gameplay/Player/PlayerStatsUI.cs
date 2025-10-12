using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class PlayerStatsUI : MonoBehaviour
{
    [Header("Text động hiển thị")]
    [SerializeField] private TMP_Text tittleNameText;   // "Sinh viên năm X"
    [SerializeField] private TMP_Text numberStamina;    // 100 / 100
    [SerializeField] private TMP_Text numberMoney;      // 100.000 VND
    [SerializeField] private TMP_Text numberGPA;        // 4.0 ( Xuất sắc )
    [SerializeField] private TMP_Text numberTraining;   // Điểm rèn luyện
    [SerializeField] private TMP_Text numberFriendly;   // Điểm thân thiện

    [Header("Nguồn thời gian (để lấy Term)")]
    [SerializeField] private ClockUI clockUI;
    [SerializeField, Min(1)] private int termsPerYear = 2;

    [Header("Stamina")]
    [SerializeField, Min(1)] private int maxStamina = 100;
    [SerializeField, Min(0)] private int staminaPerClass = 30;
    [SerializeField] private string staminaSaveKey = "PLAYER_STAMINA";
    private int currentStamina;

    [Header("GPA Settings")]
    [SerializeField] private bool autoUpdateGPA = true;
    [SerializeField, Min(1f)] private float gpaUpdateInterval = 5f;

    [Header("GPA Ranking System")]
    [SerializeField] private bool showGPARanking = true;

    private int lastSemesterDisplayed = -1;
    private float lastGpaUpdateTime = 0f;
    private float currentGPA = 0f;
    private string currentRank = "";

    void Start()
    {
        if (!clockUI) clockUI = FindFirstObjectByType<ClockUI>();
        currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        ClampAndPaintStamina();

        UpdateSemesterTitle(true);
        if (GameClock.Ins)
        {
            GameClock.Ins.OnTermChanged += OnTermChanged;
            GameClock.Ins.OnDayChanged += OnDayChanged;
        }
        RegisterToAllTeacherActions();

        UpdateGPA();
        SetMoney(100000);
        SetTrainingPoint(0);
        SetFriendlyPoint(0);
    }

    void Update()
    {
        if (autoUpdateGPA && Time.time - lastGpaUpdateTime >= gpaUpdateInterval)
        {
            UpdateGPA();
            lastGpaUpdateTime = Time.time;
        }
    }

    void OnDestroy()
    {
        if (GameClock.Ins)
        {
            GameClock.Ins.OnTermChanged -= OnTermChanged;
            GameClock.Ins.OnDayChanged -= OnDayChanged;
        }
        UnregisterFromAllTeacherActions();
        SaveStamina();
    }

    // ======== Đăng ký TeacherAction ========
    void RegisterToAllTeacherActions()
    {
        var teacherActions = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        foreach (var teacher in teacherActions)
            if (teacher.onClassFinished != null)
                teacher.onClassFinished.AddListener(ConsumeStaminaForClass);
    }

    void UnregisterFromAllTeacherActions()
    {
        var teacherActions = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        foreach (var teacher in teacherActions)
            if (teacher?.onClassFinished != null)
                teacher.onClassFinished.RemoveListener(ConsumeStaminaForClass);
    }

    // ======== GPA Ranking ========
    public string GetGPARanking(float gpa)
    {
        if (gpa >= 3.6f) return "Xuất sắc";
        if (gpa >= 3.2f) return "Giỏi";
        if (gpa >= 2.5f) return "Khá";
        if (gpa >= 2.0f) return "Trung bình";
        return "Yếu";
    }

    public Color GetRankColor(string rank) => rank switch
    {
        "Xuất sắc" => new Color(1f, 0.84f, 0f),
        "Giỏi" => new Color(0f, 1f, 0f),
        "Khá" => new Color(0f, 0.7f, 1f),
        "Trung bình" => new Color(1f, 0.65f, 0f),
        "Yếu" => new Color(1f, 0.2f, 0.2f),
        _ => Color.white
    };

    public string GetRankDescription(string rank) => rank switch
    {
        "Xuất sắc" => "GPA: 3.6 - 4.0",
        "Giỏi" => "GPA: 3.2 - 3.59",
        "Khá" => "GPA: 2.5 - 3.19",
        "Trung bình" => "GPA: 2.0 - 2.49",
        "Yếu" => "GPA: Dưới 2.0",
        _ => "Chưa xác định"
    };

    // ======== GPA Logic ========
    public void UpdateGPA()
    {
        try
        {
            var gpa = CalculateGPAFromExamResults();
            currentGPA = gpa;
            currentRank = GetGPARanking(gpa);
            PaintGPA();
        }
        catch { }
    }

    float CalculateGPAFromExamResults()
    {
        var examDB = ExamResultStorageFile.Load();
        if (examDB?.entries == null || examDB.entries.Count == 0) return 0f;

        var latestScores = examDB.entries
            .Where(e => !string.IsNullOrEmpty(e.subjectKey))
            .GroupBy(e => e.subjectKey)
            .Select(g => g.OrderByDescending(e => e.takenAtUnix).First().score4)
            .ToList();

        if (latestScores.Count == 0) return 0f;
        return Mathf.Clamp(latestScores.Average(), 0f, 4f);
    }

    void PaintGPA()
    {
        if (!numberGPA) return;
        numberGPA.text = showGPARanking && !string.IsNullOrEmpty(currentRank)
            ? $"{currentGPA:F1} ( {currentRank} )"
            : currentGPA.ToString("F1");
        numberGPA.color = GetRankColor(currentRank);
    }

    // ======== Semester / Year ========
    private void OnTermChanged() => UpdateSemesterTitle(true);

    // ======== Day Change Handler ========
    private void OnDayChanged()
    {
        ResetStaminaForNewDay();
        UpdateSemesterTitle(true);
    }

    private void ResetStaminaForNewDay()
    {
        currentStamina = maxStamina;
        PaintStamina();
        SaveStamina();
        Debug.Log("[PlayerStatsUI] Stamina reset to full for new day");
    }

    private void UpdateSemesterTitle(bool force)
    {
        if (!tittleNameText || !GameClock.Ins) return;
        int semester = GameClock.Ins.Term;
        if (!force && semester == lastSemesterDisplayed) return;

        lastSemesterDisplayed = semester;
        int year = Mathf.CeilToInt((float)semester / termsPerYear);
        tittleNameText.text = $"Sinh Viên Năm {year}";
    }

    // ======== Stamina ========
    public void SetMoney(int money) { if (numberMoney) numberMoney.text = $"{money:N0} VND"; }
    public void SetTrainingPoint(int point) { if (numberTraining) numberTraining.text = point.ToString(); }
    public void SetFriendlyPoint(int point) { if (numberFriendly) numberFriendly.text = point.ToString(); }

    public void SetStamina(int current, int max)
    {
        maxStamina = Mathf.Max(1, max);
        currentStamina = Mathf.Clamp(current, 0, maxStamina);
        PaintStamina();
        SaveStamina();
    }

    public void AddStamina(int amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + Mathf.Max(0, amount), 0, maxStamina);
        PaintStamina();
        SaveStamina();
    }

    public bool TrySpendStamina(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (currentStamina < amount) return false;
        currentStamina -= amount;
        PaintStamina();
        SaveStamina();
        return true;
    }

    public void ConsumeStaminaForClass()
    {
        TrySpendStamina(staminaPerClass);
    }

    private void ClampAndPaintStamina()
    {
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        PaintStamina();
    }

    private void PaintStamina()
    {
        if (numberStamina) numberStamina.text = $"{currentStamina} / {maxStamina}";
    }

    private void SaveStamina()
    {
        PlayerPrefs.SetInt(staminaSaveKey, currentStamina);
        PlayerPrefs.Save();
    }
}