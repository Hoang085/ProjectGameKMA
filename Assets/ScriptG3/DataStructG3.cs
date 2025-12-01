using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]

public class MatchItem
{
    public Sprite icon;

    private int _id;

    public int Id { get => _id; set => _id = value; }
}

public enum AnimState
{
    Flip,
    Explde
}

public enum GameStateG3
{
    Starting,
    Playing,
    Timeout,
    Gameover
}