using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CircleItem : MonoBehaviour
{
    public GameObject alien;
    public float showTime;

    private int _id;
    private float _curShowTime;

    public int Id { get => _id; set => _id = value; }

    public void ShowAlien(float speed)
    {
        _curShowTime = showTime;
        StartCoroutine(ShowAlienCo(speed));
    }

    private IEnumerator ShowAlienCo(float speed)
    {
        while (_curShowTime > 0 && alien != null)
        {
            alien.SetActive(true);
            yield return new WaitForSeconds(speed);
            alien.SetActive(false);
            yield return new WaitForSeconds(speed);
            _curShowTime -= speed * 2;
        }
    }

    private void OnMouseDown()
    {
        GameManagerG2.Ins.CheckAnswer(_id, () =>
        {
            alien.SetActive(true);
            StopAllCoroutines();
        });
    }
}
