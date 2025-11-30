using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerG1 : SingletonG1<GameManagerG1>
{
    [Header("Config spawn chim")]
    public Bird[] birdPrefabs;
    public float spawnTime;
    public int timeLimit;

    [Header("Đối thủ máy")]
    public AIOpponentScore aiOpponent;

    private int _curTimeLimit;
    private int _birdKilled;
    private bool _isGameover;

    public int CurTimeLimit { get => _curTimeLimit; set => _curTimeLimit = value; }
    public int BirdKilled { get => _birdKilled; set => _birdKilled = value; }

    public override void Awake()
    {
        MakeSingleton(false);
        ResetGameState();
    }

    void ResetGameState()
    {
        _curTimeLimit = timeLimit;
        _birdKilled = 0;
        _isGameover = false;
    }

    public void Start()
    {
        GameGUIManagerG1.Ins.ShowGameGUI(false);
        GameGUIManagerG1.Ins.UpdateKilledCounting(_birdKilled);
        GameGUIManagerG1.Ins.UpdateTimer(InToTime(_curTimeLimit));
    }

    public void PlayGame()
    {
        // reset trước khi chơi
        StopAllCoroutines();
        ResetGameState();

        GameGUIManagerG1.Ins.UpdateKilledCounting(_birdKilled);
        GameGUIManagerG1.Ins.UpdateTimer(InToTime(_curTimeLimit));
        GameGUIManagerG1.Ins.ShowGameGUI(true);

        // cho máy bắt đầu auto tăng điểm
        if (aiOpponent != null)
            aiOpponent.StartAutoScore();

        StartCoroutine(TimeCountDown());
        StartCoroutine(GameSpawn());
    }

    private IEnumerator TimeCountDown()
    {
        while (_curTimeLimit > 0)
        {
            yield return new WaitForSeconds(1f);
            _curTimeLimit--;

            if (_curTimeLimit <= 0)
            {
                _isGameover = true;

                // dừng bot
                if (aiOpponent != null)
                    aiOpponent.StopAutoScore();

                int aiScore = aiOpponent != null ? aiOpponent.CurrentScore : 0;

                // so sánh điểm để lấy title
                string title;
                if (_birdKilled > aiScore)
                    title = "YOU WIN";
                else if (_birdKilled < aiScore)
                    title = "YOU LOSE";
                else
                    title = "DRAW";

                // BODY chỉ còn điểm của mình và của bot
                string body = $"YOU: x{_birdKilled}\n\nBOT: x{aiScore}";

                GameGUIManagerG1.Ins.gameDialog.UpdateDialog(title, body);
                GameGUIManagerG1.Ins.gameDialog.Show(true);
                GameGUIManagerG1.Ins.CurDialog = GameGUIManagerG1.Ins.gameDialog;
            }

            GameGUIManagerG1.Ins.UpdateTimer(InToTime(_curTimeLimit));
        }
    }

    private IEnumerator GameSpawn()
    {
        while (!_isGameover)
        {
            SpawnBird();
            yield return new WaitForSeconds(spawnTime);
        }
    }

    private void SpawnBird()
    {
        Vector3 spawnPos = Vector3.zero;

        float randCheck = Random.Range(0f, 1f);

        if (randCheck >= 0.5f)
        {
            spawnPos = new Vector3(11f, Random.Range(-1f, 3f), 0f);
        }
        else
        {
            spawnPos = new Vector3(-11f, Random.Range(-1f, 3f), 0f);
        }

        if (birdPrefabs != null && birdPrefabs.Length > 0)
        {
            int randIdx = Random.Range(0, birdPrefabs.Length);

            if (birdPrefabs[randIdx] != null)
            {
                Bird birdClone = Instantiate(birdPrefabs[randIdx], spawnPos, Quaternion.identity);
            }
        }
    }

    private string InToTime(int time)
    {
        float minute = Mathf.Floor(time / 60);
        float second = Mathf.RoundToInt(time % 60);

        return minute.ToString("00") + ":" + second.ToString("00");
    }
}
