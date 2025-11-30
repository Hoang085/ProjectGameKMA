using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManagerG2 : SingletonG2<GameManagerG2>
{
    public GameState state;
    public int numberOfCircle;
    public float rotateSpeed;
    public int maxAlienNumber; // S? l??ng alien t?i ?a
    public float suggestSpeed; // T?c ?? hi?n th?
    public int levelUpStep;

    private List<CircleItem> _allCircles;
    private List<int> _cItemIdxs;
    private List<int> _cItemSelecteds;

    private int _score;
    private int _curLevel = 1;
    private int _curAlien;

    private GameState _prevState;

    public int Score { get => _score; }
    public GameState PrevState { get => _prevState; }

    protected override void Awake()
    {
        MakeSingleton(false);
        _cItemSelecteds = new List<int>();
    }

    protected override void Start()
    {
        ChangeState(GameState.Starting);

        AudioControllerG2.Ins.PlayBackgroundMusic();
    }

    public void PlayGame()
    {
        CircleFormation.Ins.Draw(numberOfCircle);
        StartCoroutine(StartGameCo());

        GUIManagerG2.Ins.ShowGameplay(true);
        GUIManagerG2.Ins.UpdateScore(0);

        var ai = FindAnyObjectByType<AIOpponentScore>();
        if (ai != null)
        {
            ai.StartAutoScore();
        }
    }

    private IEnumerator StartGameCo()
    {
        GUIManagerG2.Ins.ShowWaitingText(true);
        Init();
        yield return new WaitForSeconds(2f);
        CircleHolder.Ins.Rotate(rotateSpeed);
        ChangeState(GameState.Playing);
        GUIManagerG2.Ins.ShowWaitingText(false);
    }

    public void ChangeState(GameState newState)
    {
        _prevState = state;
        state = newState;
    }

    private void Init()
    {
        _cItemIdxs = new List<int>();
        _allCircles = CircleFormation.Ins.Elements;
        _curAlien = Mathf.Clamp(_curAlien, 1, maxAlienNumber);

        // Thêm t?t c? ch? s? trong List _allCircles vào List _cItemIdx
        for (int i = 0; i < _allCircles.Count; ++i)
        {
            _cItemIdxs.Add(i);
        }

        _cItemIdxs.RemoveAll(IsMatchToRemove);

        _cItemSelecteds.Clear();

        if (_allCircles == null || _allCircles.Count <= 0)
            return;

        int alienNeaded = _curAlien > _allCircles.Count ? _allCircles.Count : _curAlien;

        for (int i = 0; i < _allCircles.Count; ++i)
        {
            var circleItem = _allCircles[i];
            if (!circleItem)
                continue;
            circleItem.alien.SetActive(false);
        }

        // L?y ra ng?u nhiên các circleItem ?? ng??i ch?i ?oán
        for (int i = 0; i < alienNeaded; i++)
        {
            int randIdx = Random.Range(0, _cItemIdxs.Count);
            int cItemIdxVal = _cItemIdxs[randIdx];
            var cItem = _allCircles[cItemIdxVal];
            _cItemIdxs.Remove(cItemIdxVal);
            if (cItem == null)
                continue;
            _cItemSelecteds.Add(cItemIdxVal);
            cItem.ShowAlien(suggestSpeed);
        }
    }

    private bool IsMatchToRemove(int id)
    {
        if (_allCircles == null || _allCircles.Count <= 0)
            return false;

        if (_cItemSelecteds.Contains(id))
            return true;

        return false;
    }

    public void CheckAnswer(int id, UnityAction OnRightClick = null)
    {
        if (_cItemSelecteds == null || _cItemSelecteds.Count <= 0 || state != GameState.Playing)
            return;

        if (_cItemSelecteds.Contains(id))
        {
            if (OnRightClick != null)
            {
                OnRightClick.Invoke();
            }
            _score++;
            _cItemSelecteds.Remove(id);
            CreateNextLevel(id);

            GUIManagerG2.Ins.UpdateScore(_score);

            AudioControllerG2.Ins.PlaySound(AudioControllerG2.Ins.right);
        }
        else
        {
            Gameover();
        }
    }

    private void Gameover()
    {
        ChangeState(GameState.Gameover);

        CircleHolder.Ins.StopRotate();

        for (int i = 0; i < _cItemSelecteds.Count; i++)
        {
            int cItemIdx = _cItemSelecteds[i];
            var cItem = _allCircles[cItemIdx];
            if (!cItem)
                continue;
            cItem.ShowAlien(suggestSpeed);
        }
        StartCoroutine(ShowGameoverDialogCo());

        AudioControllerG2.Ins.StopPlayMusic();
        AudioControllerG2.Ins.PlaySound(AudioControllerG2.Ins.wrong);
    }

    private IEnumerator ShowGameoverDialogCo()
    {
        yield return new WaitForSeconds(2f);

        if (GUIManagerG2.Ins.gameoverDialog)
        {
            GUIManagerG2.Ins.gameoverDialog.Show(true);
        }
    }

    private void CreateNextLevel(int id)
    {
        if (_cItemSelecteds.Count > 0)
        {
            return;
        }

        if (_curLevel % levelUpStep == 0)
        {
            _curAlien++;
        }

        _curLevel++;
        Init();
    }
}
