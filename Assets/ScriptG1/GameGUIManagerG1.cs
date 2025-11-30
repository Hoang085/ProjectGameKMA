using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameGUIManagerG1 : SingletonG1<GameGUIManagerG1>
{
    public GameObject homeGUI;
    public GameObject gameGUI;


    public DialogG1 gameDialog;
    public DialogG1 pauseDialog;

    public Image fireRateFilled;
    public Text timerText;
    public Text killedCountingText;

    private DialogG1 _curDialog;

    public DialogG1 CurDialog { get => _curDialog; set => _curDialog = value; }

    public override void Awake()
    {
        MakeSingleton(false);
    }

    public void ShowGameGUI(bool isShow)
    {
        if (gameGUI != null)
        {
            gameGUI.SetActive(isShow);
        }

        if (homeGUI != null)
        {
            homeGUI.SetActive(!isShow);
        }
    }

    public void UpdateTimer(string time)
    {
        if (timerText)
            timerText.text = time;
    }

    public void UpdateKilledCounting(int killed)
    {
        if (killedCountingText)
            killedCountingText.text = "x" + killed.ToString();
    }

    public void UpdateFireRate(float rate)
    {
        if (fireRateFilled)
            fireRateFilled.fillAmount = rate;
    }

    public void PauseGame()
    {
        Time.timeScale = 0;

        if (pauseDialog)
        {
            pauseDialog.Show(true);
            pauseDialog.UpdateDialog("GAME PAUSE","PAUSE");
            _curDialog = pauseDialog;
        }
    }

    public void ResumeGame()
    {
        Time.timeScale = 1;

        if (_curDialog)
        {
            _curDialog.Show(false);
        }
    }

    public void BackToScene()
    {
        ResumeGame();

        SceneLoader.Load("GameScene");
    }
}
