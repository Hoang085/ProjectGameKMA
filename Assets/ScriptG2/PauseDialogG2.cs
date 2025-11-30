using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseDialogG2 : DialogG2
{
    public override void Show(bool isShow)
    {
        base.Show(isShow);
        Time.timeScale = 0f;
        GameManagerG2.Ins.ChangeState(GameState.Pause);
    }

    public override void Close()
    {
        base.Close();
        Time.timeScale = 1f;
    }

    public void BackHome_Replay()
    {
        Close();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Resume()
    {
        GameManagerG2.Ins.ChangeState(GameManagerG2.Ins.PrevState);
        Close();
    }
}
