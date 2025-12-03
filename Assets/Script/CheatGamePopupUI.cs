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
        attempt.subjectKey = subjectName.Trim().ToLowerInvariant();
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
}