using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using HHH.Common;

[DisallowMultipleComponent]
public class PlayerStatsUI : BasePopUp
{
    [Header("Text động hiển thị")]
    [SerializeField] private TMP_Text tittleNameText;   
    [SerializeField] private TMP_Text numberStamina;    // 100 / 100
    [SerializeField] private TMP_Text numberGPA;        // 4.0 
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

    public override void OnInitScreen()
    {
        base.OnInitScreen();
        
        if (!clockUI) clockUI = FindFirstObjectByType<ClockUI>();
        
        // Load stamina từ PlayerPrefs
        currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        Debug.Log($"[PlayerStatsUI] Loaded stamina from PlayerPrefs: {currentStamina}/{maxStamina} (key: {staminaSaveKey})");
        
        ClampAndPaintStamina();

        UpdateSemesterTitle(true);
        if (GameClock.Ins)
        {
            GameClock.Ins.OnTermChanged += OnTermChanged;
            GameClock.Ins.OnDayChanged += OnDayChanged;
        }
        
        // Đăng ký vào các TeacherAction
        RegisterToAllTeacherActions();

        UpdateGPA();
        int currentFriendly = PlayerPrefs.GetInt(GameManager.FRIENDLY_POINT_KEY, 0);
        SetFriendlyPoint(currentFriendly);
    }

    /// <summary>
    /// Refresh UI mỗi khi popup được mở để hiển thị giá trị mới nhất
    /// </summary>
    public override void OnShowScreen()
    {
        base.OnShowScreen();
        
        // Đọc lại stamina từ PlayerPrefs để đảm bảo hiển thị giá trị mới nhất
        int oldStamina = currentStamina;
        currentStamina = PlayerPrefs.GetInt(staminaSaveKey, maxStamina);
        Debug.Log($"[PlayerStatsUI] Refreshed stamina: {oldStamina} -> {currentStamina}/{maxStamina}");
        
        ClampAndPaintStamina();
        
        // Refresh các thông tin khác
        UpdateSemesterTitle(true);
        UpdateGPA();
        if (GameManager.Ins != null)
        {
            SetFriendlyPoint(GameManager.Ins.GetFriendlyPoint());
        }
        else
        {
            SetFriendlyPoint(PlayerPrefs.GetInt(GameManager.FRIENDLY_POINT_KEY, 0));
        }
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

    void RegisterToAllTeacherActions()
    {
        var teacherActions = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        Debug.Log($"[PlayerStatsUI] Found {teacherActions.Length} TeacherAction(s) in scene");
        
        foreach (var teacher in teacherActions)
        {
            if (teacher == null)
            {
                Debug.LogWarning($"[PlayerStatsUI] Null TeacherAction found!");
                continue;
            }
            
            if (teacher.onClassStarted != null)
            {
                teacher.onClassStarted.AddListener(ConsumeStaminaForClass);
                Debug.Log($"[PlayerStatsUI] Registered to {teacher.name}.onClassStarted (Total listeners: {teacher.onClassStarted.GetPersistentEventCount()})");
            }
            else
            {
                Debug.LogError($"[PlayerStatsUI] {teacher.name}.onClassStarted is NULL!");
            }
        }
        
        Debug.Log($"[PlayerStatsUI] REGISTRATION END");
    }

    void UnregisterFromAllTeacherActions()
    {
        var teacherActions = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        Debug.Log($"[PlayerStatsUI] Unregistering from {teacherActions.Length} TeacherAction(s)");
        
        foreach (var teacher in teacherActions)
        {
            if (teacher?.onClassStarted != null)
                teacher.onClassStarted.RemoveListener(ConsumeStaminaForClass);
        }
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
        "Giởi" => "GPA: 3.2 - 3.59",
        "Khá" => "GPA: 2.5 - 3.19",
        "Trung bình" => "GPA: 2.0 - 2.49",
        "Yếu" => "GPA: Dưới 2.0",
        _ => "Chưa xác định"
    };

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

        // Nhóm theo môn học và lấy điểm thi gần nhất
        var latestScores = examDB.entries
            .Where(e => !string.IsNullOrEmpty(e.subjectKey))
            .GroupBy(e => e.subjectKey) // Nhóm theo môn
            .Select(g => g.OrderByDescending(e => e.takenAtUnix).First()) // Lấy lần thi gần nhất
            .Select(e => e.score4)
            .ToList();

        if (latestScores.Count == 0) return 0f;

        // GPA = Tổng điểm hệ 4 / Tổng số môn đã thi
        float gpa = latestScores.Average();

        return Mathf.Clamp(gpa, 0f, 4f);
    }

    void PaintGPA()
    {
        if (!numberGPA) return;
        numberGPA.text = showGPARanking && !string.IsNullOrEmpty(currentRank)
            ? $"{currentGPA:F1} ( {currentRank} )"
            : currentGPA.ToString("F1");
        numberGPA.color = GetRankColor(currentRank);
    }

    private void OnTermChanged() => UpdateSemesterTitle(true);

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
        Debug.Log($"[PlayerStatsUI] TrySpendStamina({amount}) - Current: {currentStamina}, Max: {maxStamina}");
        
        amount = Mathf.Max(0, amount);
        if (currentStamina < amount)
        {
            Debug.LogWarning($"[PlayerStatsUI] NOT ENOUGH STAMINA! Need: {amount}, Have: {currentStamina}");
            return false;
        }
        
        int oldStamina = currentStamina;
        currentStamina -= amount;
        
        Debug.Log($"[PlayerStatsUI] Stamina spent! {oldStamina} -> {currentStamina} (spent: {amount})");
        
        PaintStamina();
        SaveStamina();
        
        // Verify save
        int savedValue = PlayerPrefs.GetInt(staminaSaveKey, -1);
        Debug.Log($"[PlayerStatsUI] Verified PlayerPrefs[{staminaSaveKey}] = {savedValue}");
        
        return true;
    }

    public void ConsumeStaminaForClass()
    {
        bool success = TrySpendStamina(staminaPerClass);
    }

    private void ClampAndPaintStamina()
    {
        int oldStamina = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        
        if (oldStamina != currentStamina)
        {
            Debug.Log($"[PlayerStatsUI] ClampAndPaintStamina: Clamped {oldStamina} -> {currentStamina}");
        }
        
        PaintStamina();
    }

    private void PaintStamina()
    {
        if (numberStamina)
        {
            string newText = $"{currentStamina} / {maxStamina}";
            Debug.Log($"[PlayerStatsUI] PaintStamina: Setting text to '{newText}'");
            numberStamina.text = newText;
        }
        else
        {
            Debug.LogError("[PlayerStatsUI] PaintStamina: numberStamina TMP_Text is NULL!");
        }
    }

    private void SaveStamina()
    {
        Debug.Log($"[PlayerStatsUI] SaveStamina: Saving {currentStamina} to key '{staminaSaveKey}'");
        PlayerPrefs.SetInt(staminaSaveKey, currentStamina);
        PlayerPrefs.Save();
        
        // Verify save
        int verified = PlayerPrefs.GetInt(staminaSaveKey, -1);
        if (verified != currentStamina)
        {
            Debug.LogError($"[PlayerStatsUI] SAVE FAILED! Expected: {currentStamina}, Got: {verified}");
        }
        else
        {
            Debug.Log($"[PlayerStatsUI] Save verified: {verified}");
        }
    }
}