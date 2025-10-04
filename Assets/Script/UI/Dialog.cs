using UnityEngine;

public class Dialog : MonoBehaviour
{
    private static Dialog instance;

    public static Dialog Instance { get => instance; set => instance = value; }

    public virtual void ShowUI(bool isShow)
    {
        gameObject.SetActive(isShow);
    }

    public virtual void Close()
    {
        gameObject.SetActive(false);
    }
}
