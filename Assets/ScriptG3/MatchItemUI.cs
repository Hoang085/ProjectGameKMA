using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class MatchItemUI : MonoBehaviour
{
    // Cập nhật lại các thẻ
    private int _id;

    public Sprite bg;
    public Sprite backBG;
    public Image itemBG;
    public Image itemIcon;
    public Button btnComp;

    private bool _isOpened;
    private Animator _anim;

    public int Id { get => _id; set => _id = value; }

    private void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    public void UpdateFirstState(Sprite icon)
    {
        if (itemBG)
        {
            itemBG.sprite = backBG;
        }

        if (itemIcon)
        {
            itemIcon.sprite = icon;
            itemIcon.gameObject.SetActive(false);
        }
    }

    public void ChangeState()
    {
        _isOpened = !_isOpened;

        if (itemBG)
        {
            itemBG.sprite = _isOpened ? bg : backBG;
        }

        if (itemIcon)
        {
            itemIcon.gameObject.SetActive(_isOpened);
        }
    }

    public void OpenAnimTrigger()
    {
        if (_anim)
        {
            _anim.SetBool(AnimState.Flip.ToString(), true);
        }
    }

    public void ExplodeAnimTrigger()
    {
        if (_anim)
        {
            _anim.SetBool(AnimState.Explde.ToString(), true);
        }
    }

    public void BackToIdle()
    {
        if (_anim)
        {
            _anim.SetBool(AnimState.Flip.ToString(), false);
        }
        if (btnComp)
        {
            btnComp.enabled = !_isOpened;
        }
    }
}
