using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GUIManagerG3 : SingletonG3<GUIManagerG3>
{
    public GameObject mainMenuUI;
    public GameObject gameplay;
    public Image timeBar;
    public PauseDialogG3 pauseDialog;
    public TimeoutDialog timeoutDialog;
    public GameoverDialogG3 gameoverDialog;

    public TextMeshProUGUI scoreText;

    public override void Awake()
    {
        MakeSingleton(false);
    }

    public void ShowGameplay(bool isShow)
    {
        if (gameplay)
        {
            gameplay.SetActive(isShow);
        }

        if (mainMenuUI)
        {
            mainMenuUI.SetActive(!isShow);
        }
    }

    public void UpdateTimeBar(float curTime, float totalTime)
    {
        float rate = curTime / totalTime;
        if (timeBar)
        {
            timeBar.fillAmount = rate;
        }
    }

    public void UpdateScore(int score)
    {
        if (scoreText)
            scoreText.text = score.ToString("0");
    }

    public void BackToScene()
    {
        Debug.Log("[GUIManagerG3] Saving state before returning to GameScene...");
        GameStateManager.SavePreMiniGameState("Game1");
        PlayerPrefs.SetInt("ShouldRestoreStateAfterMiniGame", 1);
        PlayerPrefs.SetInt("ADVANCE_SLOT_AFTER_EXAM", 1);
        PlayerPrefs.SetInt("DEDUCT_STAMINA_AFTER_MINIGAME", 1);
        PlayerPrefs.Save();

        Debug.Log("[GUIManagerG3] State saved and flags set");
        SceneLoader.Load("GameScene");
    }
}
