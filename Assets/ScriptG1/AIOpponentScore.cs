using UnityEngine;
using TMPro;
using System.Collections;

public class AIOpponentScore : MonoBehaviour
{
    [Header("Cấu hình tăng điểm")]
    public int addScore = 1;
    public float minInterval = 0.5f;
    public float maxInterval = 1.5f;

    [Header("UI hiển thị điểm máy")]
    public TextMeshProUGUI aiScoreText;

    public int CurrentScore { get; private set; }

    Coroutine autoScoreRoutine;

    public void StartAutoScore()
    {
        CurrentScore = 0;
        UpdateUI();

        if (autoScoreRoutine != null)
            StopCoroutine(autoScoreRoutine);

        autoScoreRoutine = StartCoroutine(AutoIncreaseScore());
    }

    public void StopAutoScore()
    {
        if (autoScoreRoutine != null)
        {
            StopCoroutine(autoScoreRoutine);
            autoScoreRoutine = null;
        }
    }

    IEnumerator AutoIncreaseScore()
    {
        while (true)
        {
            float wait = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(wait);

            int add = addScore;
            CurrentScore += add;
            UpdateUI();
        }
    }

    void UpdateUI()
    {
        if (aiScoreText != null)
            aiScoreText.text = CurrentScore.ToString();
    }
}
