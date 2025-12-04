using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;

public class CheatGamePopupUI : MonoBehaviour
{
    [SerializeField] private Button btnClose;

    [Header("Apply Cheat Score")]
    [SerializeField] private Button btnApplyCheat;
    [SerializeField] private TMP_InputField inputScore;

    [Header("Apply Cheat Stamina")]
    [SerializeField] private Button btnApplyStamina;
    [SerializeField] private TMP_InputField inputStamina;

    [Header("Apply Cheat Friendly Point")]
    [SerializeField] private Button btnApplyFriendlyPoint;
    [SerializeField] private TMP_InputField inputFriendlyPoint;

    [Header("Jump To Term (Học Kì)")]
    [Tooltip("Danh sách các nút nhảy tới học kì (Kì 1 - Kì 10)")]
    [SerializeField] private Button[] btnJumpToTerms = new Button[10];

    [Header("Skip Learning Process")]
    [Tooltip("Nút bỏ qua quá trình học - nhảy tới tuần 5 và đánh dấu hoàn thành tất cả môn")]
    [SerializeField] private Button btnSkipLearning;

    [Header("Data Configuration")]
    [SerializeField] private List<SemesterConfig> allSemesterConfigs;

    private void Start()
    {
        if (btnClose != null)
            btnClose.onClick.AddListener(OnclickClose);

        if (btnApplyCheat != null)
            btnApplyCheat.onClick.AddListener(OnClickApplyCheat);

        if (btnApplyStamina != null)
            btnApplyStamina.onClick.AddListener(OnClickApplyStamina);

        if (btnApplyFriendlyPoint != null)
            btnApplyFriendlyPoint.onClick.AddListener(OnClickApplyFriendlyPoint);

        if (btnSkipLearning != null)
            btnSkipLearning.onClick.AddListener(OnClickSkipLearning);

        // Gắn listener cho các nút nhảy học kì
        SetupJumpToTermButtons();
    }

    /// <summary>
    /// Cheat bỏ qua quá trình học
    /// Nhảy tới Tuần 5, Chủ Nhật, 7h, Ca 1 và đánh dấu tất cả môn đã hoàn thành
    /// </summary>
    private void OnClickSkipLearning()
    {
        if (GameClock.Ins == null)
        {
            Debug.LogError("[Cheat] Không tìm thấy GameClock!");
            return;
        }

        // Lấy kỳ hiện tại
        int currentTerm = GameClock.Ins.Term;

        // Lấy năm học từ CalendarConfig
        CalendarConfig config = GameClock.Ins.config;
        if (config == null)
        {
            Debug.LogError("[Cheat] CalendarConfig chưa được gán trong GameClock!");
            return;
        }

        int termsPerYear = Mathf.Max(1, config.termsPerYear);
        int currentYear = ((currentTerm - 1) / termsPerYear) + 1;

        GameClock.Ins.SetTime(
            year: currentYear,
            term: currentTerm,
            week: 5,
            dayIndex1Based: 7, // Chủ Nhật
            slot: DaySlot.MorningA
        );

        // Đặt thời gian về 7h sáng
        GameClock.Ins.SetMinuteOfDay(GameClock.Ins.tSession1, syncSlot: true);

        Debug.Log($"[Cheat] Đã nhảy tới Tuần 5, Chủ Nhật, 7h, Ca 1 (Kỳ {currentTerm})");

        // Lấy SemesterConfig cho kỳ hiện tại
        int configIndex = currentTerm - 1;
        if (allSemesterConfigs == null || configIndex < 0 || configIndex >= allSemesterConfigs.Count || allSemesterConfigs[configIndex] == null)
        {
            Debug.LogError($"[Cheat] Chưa setup Config cho Kì {currentTerm} trong Inspector!");
            return;
        }

        SemesterConfig targetConfig = allSemesterConfigs[configIndex];
        if (targetConfig.Subjects == null || !targetConfig.Subjects.Any())
        {
            Debug.LogWarning($"[Cheat] Kì {currentTerm} không có môn học nào trong Config.");
            return;
        }

        // Đánh dấu tất cả các môn đã hoàn thành đủ số buổi
        MarkAllSubjectsComplete(targetConfig, currentTerm);

        // Tạo lịch thi cho tất cả các môn
        CreateExamSchedulesForAllSubjects();

        // Hiển thị thông báo
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.Enqueue($"Đã bỏ qua quá trình học!");
        }

        // Refresh UI
        RefreshRelatedUIs(currentTerm);

        OnclickClose();
    }

    /// <summary>
    /// Đánh dấu tất cả môn đã hoàn thành đủ số buổi học
    /// </summary>
    private void MarkAllSubjectsComplete(SemesterConfig semesterConfig, int currentTerm)
    {
        // Tìm tất cả TeacherAction trong scene
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        
        foreach (var subject in semesterConfig.Subjects)
        {
            if (subject == null || string.IsNullOrEmpty(subject.Name)) continue;

            // Tìm TeacherAction tương ứng với môn học
            TeacherAction matchingTeacher = null;
            SubjectEntry matchingSubjectEntry = null;

            foreach (var teacher in teachers)
            {
                if (teacher == null || teacher.subjects == null) continue;

                foreach (var subjectEntry in teacher.subjects)
                {
                    if (subjectEntry == null) continue;

                    // So sánh tên môn (case-insensitive)
                    if (string.Equals(subjectEntry.subjectName, subject.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingTeacher = teacher;
                        matchingSubjectEntry = subjectEntry;
                        break;
                    }
                }

                if (matchingTeacher != null) break;
            }

            if (matchingSubjectEntry == null)
            {
                Debug.LogWarning($"[Cheat] Không tìm thấy SubjectEntry cho môn {subject.Name}");
                continue;
            }

            // Tính số buổi cần học (threshold)
            int maxSessions = matchingSubjectEntry.maxSessions;
            int requiredSessions = GetLearningSessionThreshold(maxSessions);

            // Lưu progress vào PlayerPrefs
            SaveSubjectProgress(matchingSubjectEntry, currentTerm, requiredSessions);

            Debug.Log($"[Cheat] Đã đánh dấu môn {subject.Name} hoàn thành ({requiredSessions}/{maxSessions} buổi)");
        }
    }

    /// <summary>
    /// Lưu progress môn học vào PlayerPrefs
    /// </summary>
    private void SaveSubjectProgress(SubjectEntry subject, int term, int sessionCount)
    {
        string subjectKey = GetStableSubjectKey(subject);
        
        // Lưu vào key chính
        string mainKey = $"T{term}_SUBJ_{subjectKey}_session";
        PlayerPrefs.SetInt(mainKey, sessionCount);

        // Lưu vào legacy keys để tương thích
        string legacyKey1 = $"T{term}_SUBJ_{NormalizeKey(subject.subjectName)}_session";
        string legacyKey2 = $"SUBJ_{NormalizeKey(subject.subjectName)}_session";
        
        PlayerPrefs.SetInt(legacyKey1, sessionCount);
        PlayerPrefs.SetInt(legacyKey2, sessionCount);
        
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Lấy ngưỡng số buổi cần học để được thi
    /// </summary>
    private int GetLearningSessionThreshold(int actualMaxSessions)
    {
        if (actualMaxSessions >= 18) return 15;
        if (actualMaxSessions == 15) return 15;
        if (actualMaxSessions >= 12) return 10;
        return actualMaxSessions;
    }

    /// <summary>
    /// Tạo lịch thi cho tất cả các môn đã hoàn thành
    /// </summary>
    private void CreateExamSchedulesForAllSubjects()
    {
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        
        foreach (var teacher in teachers)
        {
            if (teacher == null || teacher.subjects == null) continue;

            foreach (var subject in teacher.subjects)
            {
                if (subject == null || string.IsNullOrEmpty(subject.subjectName)) continue;

                // Gọi CheckAndCreateExamScheduleIfFinished() để tạo lịch thi
                teacher.CheckAndCreateExamScheduleIfFinished(subject);
            }
        }

        Debug.Log("[Cheat] Đã tạo lịch thi cho tất cả các môn");
    }

    /// <summary>
    /// Lấy stable subject key giống TeacherAction
    /// </summary>
    private string GetStableSubjectKey(SubjectEntry s)
    {
        var baseKey = !string.IsNullOrWhiteSpace(s.subjectKeyForNotes) ? s.subjectKeyForNotes : s.subjectName;
        return NormalizeKey(baseKey);
    }

    /// <summary>
    /// Normalize key giống TeacherAction
    /// </summary>
    private static string NormalizeKey(string s) => (s ?? "").Trim().ToLowerInvariant();

    private void SetupJumpToTermButtons()
    {
        for (int i = 0; i < btnJumpToTerms.Length; i++)
        {
            if (btnJumpToTerms[i] != null)
            {
                int termIndex = i + 1;
                btnJumpToTerms[i].onClick.RemoveAllListeners();
                btnJumpToTerms[i].onClick.AddListener(() => OnClickJumpToTerm(termIndex));
                
                Debug.Log($"[Cheat] Đã gắn listener cho nút Kì {termIndex}");
            }
        }
    }

    private void OnClickJumpToTerm(int targetTerm)
    {
        if (GameClock.Ins == null)
        {
            Debug.LogError("[Cheat] Không tìm thấy GameClock!");
            return;
        }

        // Lấy thông tin từ CalendarConfig
        CalendarConfig config = GameClock.Ins.config;
        if (config == null)
        {
            Debug.LogError("[Cheat] CalendarConfig chưa được gán trong GameClock!");
            return;
        }

        // Tính toán năm học dựa trên học kì
        int termsPerYear = Mathf.Max(1, config.termsPerYear);
        int targetYear = ((targetTerm - 1) / termsPerYear) + 1;

        // Đặt về tuần 1, ngày 1, ca 1 của học kì mới
        GameClock.Ins.SetTime(
            year: targetYear,
            term: targetTerm,
            week: 1,
            dayIndex1Based: 1,
            slot: DaySlot.MorningA
        );

        // Đặt lại thời gian về đầu ngày (07:00)
        GameClock.Ins.SetMinuteOfDay(GameClock.Ins.tSession1, syncSlot: true);

        Debug.Log($"[Cheat] Đã nhảy tới Kì {targetTerm}");

        // Hiển thị thông báo nếu có NotificationPopupSpawner
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.Enqueue($"Đã nhảy tới Kì {targetTerm}!");
        }

        // Refresh các UI liên quan nếu cần
        RefreshRelatedUIs(targetTerm);

        OnclickClose();
    }

    private void RefreshRelatedUIs(int currentTerm)
    {
        // Refresh bảng điểm nếu đang mở
        var scoreBoard = FindFirstObjectByType<ScoreSubjectUI>();
        if (scoreBoard != null && scoreBoard.gameObject.activeInHierarchy)
        {
            scoreBoard.SetSemester(currentTerm);
            Debug.Log($"[Cheat] Đã refresh bảng điểm cho Kì {currentTerm}");
        }

        // Refresh ClockUI nếu có
        var clockUI = FindFirstObjectByType<ClockUI>();
        if (clockUI != null)
        {
            Debug.Log("[Cheat] ClockUI sẽ tự động cập nhật qua GameClock events");
        }

        // Refresh ScheduleUI nếu đang mở
        var scheduleUI = FindFirstObjectByType<ScheduleUI>();
        if (scheduleUI != null && scheduleUI.gameObject.activeInHierarchy)
        {
            scheduleUI.RefreshExamScheduleImmediately();
            Debug.Log("[Cheat] Đã refresh ScheduleUI");
        }

        // Trigger notification refresh trong GameManager
        if (GameManager.Ins != null)
        {
            GameManager.Ins.RefreshAllNotificationStatesAfterRestore();
            Debug.Log("[Cheat] Đã refresh notification states");
        }
    }

    public void OnclickClose()
    {
        Destroy(this.gameObject);
    }

    private void OnClickApplyCheat()
    {
        if (inputScore == null || string.IsNullOrEmpty(inputScore.text))
        {
            Debug.LogWarning("[Cheat] Chưa nhập điểm!");
            return;
        }

        if (!float.TryParse(inputScore.text, out float cheatScore))
        {
            Debug.LogWarning("[Cheat] Điểm phải là số!");
            return;
        }

        cheatScore = Mathf.Clamp(cheatScore, 0f, 10f);

        // 2. Lấy Config của kì hiện tại
        int currentTerm = 1;
        if (GameClock.Ins != null)
        {
            currentTerm = GameClock.Ins.Term;
        }
        else
        {
            Debug.LogWarning("[Cheat] Không tìm thấy GameClock, mặc định là Kì 1");
        }

        // Kiểm tra xem list config có đủ không
        int configIndex = currentTerm - 1;

        if (allSemesterConfigs == null || configIndex < 0 || configIndex >= allSemesterConfigs.Count || allSemesterConfigs[configIndex] == null)
        {
            Debug.LogError($"[Cheat] Chưa setup Config cho Kì {currentTerm} trong Inspector hoặc sai Index!");
            return;
        }

        SemesterConfig targetConfig = allSemesterConfigs[configIndex];

        if (targetConfig.Subjects == null || !targetConfig.Subjects.Any())
        {
            Debug.LogWarning($"[Cheat] Kì {currentTerm} không có môn học nào trong Config.");
            return;
        }

        Debug.Log($"[Cheat] Đang hack điểm {cheatScore} cho Kì {currentTerm} ({targetConfig.name})...");

        // 3. Hack điểm từng môn
        foreach (var subj in targetConfig.Subjects)
        {
            if (subj == null || string.IsNullOrEmpty(subj.Name)) continue;
            SaveCheatScore(subj.Name, currentTerm, cheatScore);
        }

        Debug.Log("[Cheat] XONG! Đã hack full điểm.");

        // Refresh lại UI bảng điểm nếu đang mở (Optional)
        var scoreBoard = FindFirstObjectByType<ScoreSubjectUI>();
        if (scoreBoard != null && scoreBoard.gameObject.activeInHierarchy)
        {
            scoreBoard.SetSemester(currentTerm); // Refresh lại bảng
        }

        OnclickClose();
    }

    private void SaveCheatScore(string subjectName, int term, float score10)
    {
        ExamAttempt attempt = new ExamAttempt();

        attempt.subjectName = subjectName;
        
        // Sử dụng KeyUtil.MakeKey() giống TeacherAction để đảm bảo key khớp
        attempt.subjectKey = KeyUtil.MakeKey(subjectName);
        
        attempt.semesterIndex = term;

        attempt.score10 = score10;

        attempt.score4 = PointConversion.Convert10To4(score10);
        attempt.letter = PointConversion.LetterFrom10(score10);

        attempt.examTitle = "HACKED";
        attempt.correct = (int)score10;
        attempt.total = 10;

        attempt.takenAtIso = DateTime.UtcNow.ToString("o");
        attempt.takenAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        attempt.isRetake = false;
        attempt.isBanned = false;

        ExamResultStorageFile.AddAttempt(attempt);
        
        Debug.Log($"[CheatGamePopupUI] Đã hack điểm {subjectName} (key: {attempt.subjectKey}) = {score10}");
    }

    private void OnClickApplyStamina()
    {
        if (inputStamina == null || string.IsNullOrEmpty(inputStamina.text))
        {
            Debug.LogWarning("[Cheat] Vui lòng nhập số thể lực!");
            return;
        }

        if (int.TryParse(inputStamina.text, out int newStamina))
        {
            newStamina = Mathf.Clamp(newStamina, 0, 100);

            PlayerPrefs.SetInt("PLAYER_STAMINA", newStamina);
            PlayerPrefs.Save();

            Debug.Log($"[Cheat] Đã set Thể Lực = {newStamina}");

            var statsUI = FindFirstObjectByType<PlayerStatsUI>();
            OnclickClose();
        }
        else
        {
            Debug.LogWarning("[Cheat] Thể lực phải là số nguyên!");
        }
    }

    private void OnClickApplyFriendlyPoint()
    {
        if (inputFriendlyPoint == null || string.IsNullOrEmpty(inputFriendlyPoint.text))
        {
            Debug.LogWarning("[Cheat] Vui lòng nhập điểm thân thiện!");
            return;
        }

        if (!int.TryParse(inputFriendlyPoint.text, out int friendlyPoint))
        {
            Debug.LogWarning("[Cheat] Điểm thân thiện phải là số nguyên!");
            return;
        }

        friendlyPoint = Mathf.Clamp(friendlyPoint, 0, 1000);

        PlayerPrefs.SetInt(GameManager.FRIENDLY_POINT_KEY, friendlyPoint);
        PlayerPrefs.Save();

        Debug.Log($"[Cheat] Đã set Điểm Thân Thiện = {friendlyPoint}");

        OnclickClose();
    }
}