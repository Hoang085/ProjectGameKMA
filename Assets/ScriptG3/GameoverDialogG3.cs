using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameoverDialogG3 : DialogG3
{
    public Text resultText;

    public override void Show(bool isShow)
    {
        base.Show(isShow);

        int playerMoves = 0;
        if (GameManagerG3.Ins != null)
            playerMoves = GameManagerG3.Ins.TotalMoving;

        var ai = FindAnyObjectByType<AIOpponentScore>(FindObjectsInactive.Include);
        int aiScore = 0;
        if (ai != null)
        {
            ai.StopAutoScore();
            aiScore = ai.CurrentScore;
        }

        if (resultText)
        {
            if (playerMoves < aiScore)
                resultText.text = "YOU WIN";
            else if (playerMoves > aiScore)
                resultText.text = "YOU LOSE";
            else
                resultText.text = "DRAW";
        }
    }
}
