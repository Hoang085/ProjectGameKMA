using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GUIManagerG2 : SingletonG2<GUIManagerG2>
{
    public GameObject mainMenu;
    public GameObject gameplay;
    public Text waitingText;
    public Text scoreText;
    public Text homeBestScoreText;
    public DialogG2 gameoverDialog;

    protected override void Awake()
    {
        MakeSingleton(false);
    }

    protected override void Start()
    {
        ShowGameplay(false);
    }

    public void ShowGameplay(bool isShow)
    {
        if (gameplay)
        {
            gameplay.SetActive(isShow);
        }

        if (mainMenu)
        {
            mainMenu.SetActive(!isShow);
        }
    }

    public void UpdateScore(int score)
    {
        if (scoreText)
        {
            scoreText.text = "Score: " + score.ToString("000000");
        }
    }

    public void UpdateHomeScore(int score)
    {
        if (homeBestScoreText)
        {
            homeBestScoreText.text = score.ToString("000000");
        }
    }

    public void ShowWaitingText(bool isShow)
    {
        if (waitingText)
        {
            waitingText.gameObject.SetActive(isShow);
        }
    }
}
