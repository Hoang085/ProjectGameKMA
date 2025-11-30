using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameoverDialogG2 : DialogG2
{
    public Text playerScoreText;
    public Text aiScoreText;
    public Text resultText;

    public override void Show(bool isShow)
    {
        base.Show(isShow);

        var ai = FindAnyObjectByType<AIOpponentScore>();
        if (ai != null)
            ai.StopAutoScore();

        int player = GameManagerG2.Ins.Score;
        int opponent = ai ? ai.CurrentScore : 0;

        if (playerScoreText)
            playerScoreText.text = "Player: " + player;

        if (aiScoreText)
            aiScoreText.text = "AI: " + opponent;

        if (resultText)
        {
            if (player > opponent)
                resultText.text = "YOU WIN";
            else if (player < opponent)
                resultText.text = "YOU LOSE";
            else
                resultText.text = "DRAW";
        }

        AudioControllerG2.Ins.PlaySound(AudioControllerG2.Ins.gameover);
    }

    public void BackToScene()
    {
        Debug.Log("[GameoverDialogG2] Saving state before returning to GameScene...");
        GameStateManager.SavePreMiniGameState("Game1");
        PlayerPrefs.SetInt("ShouldRestoreStateAfterMiniGame", 1);
        PlayerPrefs.SetInt("ADVANCE_SLOT_AFTER_EXAM", 1);
        PlayerPrefs.SetInt("DEDUCT_STAMINA_AFTER_MINIGAME", 1);
        PlayerPrefs.Save();

        Debug.Log("[GameoverDialogG2] State saved and flags set");
        SceneLoader.Load("GameScene");
    }
}
