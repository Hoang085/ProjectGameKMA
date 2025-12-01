using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseDialogG3 : DialogG3
{
    public override void Show(bool isShow)
    {
        base.Show(isShow);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        Close();
    }
}
