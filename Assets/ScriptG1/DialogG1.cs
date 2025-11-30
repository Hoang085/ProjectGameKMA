using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogG1 : MonoBehaviour
{
    public Text tittleText;
    public Text contentText;

    public void Show(bool isShow)
    {
        gameObject.SetActive(isShow);
    }

    public void UpdateDialog(string title, string content)
    {
        if (tittleText != null)
        {
            tittleText.text = title;
        }

        if (contentText != null)
        {
            contentText.text = content;
        }
    }
}
