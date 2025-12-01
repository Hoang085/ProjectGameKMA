using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

public class GameManagerG3 : SingletonG3<GameManagerG3>
{
    public int timeLimit;
    public MatchItem[] matchItems;
    public MatchItemUI itemUIPb;
    public Transform gridRoot;
    public GameStateG3 state; 

    private List<MatchItem> _matchItemsCopy;
    private List<MatchItemUI> _matchItemUIs;
    private List<MatchItemUI> _answers;
    private float _timeCounting;
    private int _totalMatchItems;
    private int _totalMoving; 
    private int _rightMoving;
    private bool _isAnswerChecking;

    private AIOpponentScore _aiOpponent;

    public int TotalMoving { get => _totalMoving; }
    public int RightMoving { get => _rightMoving; }

    public override void Awake()
    {
        MakeSingleton(false);
        _matchItemsCopy = new List<MatchItem>();
        _matchItemUIs = new List<MatchItemUI>();
        _answers = new List<MatchItemUI>();
        _timeCounting = timeLimit;
        state = GameStateG3.Starting;
    }

    public override void Start()
    {
        base.Start();
        _aiOpponent = FindAnyObjectByType<AIOpponentScore>(FindObjectsInactive.Include);

        if (AudioControllerG3.Ins)
        {
            AudioControllerG3.Ins.PlayBackgroundMusic();
        }
    }

    private void Update()
    {
        if (state != GameStateG3.Playing)
            return; 

        _timeCounting -= Time.deltaTime; 

        if (_timeCounting <= 0 && state != GameStateG3.Timeout)
        {
            state = GameStateG3.Timeout;
            _timeCounting = 0;

            if (_aiOpponent != null)       
                _aiOpponent.StopAutoScore();
            if (GUIManagerG3.Ins)
            {
                GUIManagerG3.Ins.timeoutDialog.Show(true); 
            }
            if (AudioControllerG3.Ins)
            {
                AudioControllerG3.Ins.PlaySound(AudioControllerG3.Ins.timeOut);
            }
            Debug.Log("Time's out! Game Over!");
        }

        if (GUIManagerG3.Ins)
        {
            GUIManagerG3.Ins.UpdateTimeBar((float)_timeCounting, timeLimit);
        }
    }

    public void PlayGame()
    {
        state = GameStateG3.Playing;

        _totalMoving = 0;
        _rightMoving = 0;
        _timeCounting = timeLimit;

        if (GUIManagerG3.Ins)
        {
            GUIManagerG3.Ins.UpdateScore(_totalMoving);
            GUIManagerG3.Ins.ShowGameplay(true);
        }

        GenerateMatchItems();

        if (_aiOpponent != null)
        {
            _aiOpponent.StartAutoScore();
        }
        else
        {
            Debug.LogWarning("[GameManagerG3] Không tìm thấy AIOpponentScore trong scene!");
        }
    }

    private void GenerateMatchItems() 
    {
        if (matchItems == null || matchItems.Length <= 0 || itemUIPb == null || gridRoot == null)
            return;

        int totalItems = matchItems.Length;
        int divItem = totalItems % 2; 
        _totalMatchItems = totalItems - divItem;

        for (int i = 0; i < _totalMatchItems; i++)
        {
            var matchItem = matchItems[i];
            if (matchItem != null)
            {
                matchItem.Id = i; 
            }
        }
        _matchItemsCopy.AddRange(matchItems); 
        _matchItemsCopy.AddRange(matchItems); 
        ShuffleMatchItems();
        ClearGrid();

        for (int i = 0; i < _matchItemsCopy.Count; i++)
        {
            var matchItem = _matchItemsCopy[i];

            var matchItemUIClone = Instantiate(itemUIPb, Vector3.zero, Quaternion.identity);
            matchItemUIClone.transform.SetParent(gridRoot);
            matchItemUIClone.transform.localPosition = Vector3.zero;
            matchItemUIClone.transform.localScale = Vector3.one;
            matchItemUIClone.UpdateFirstState(matchItem.icon);
            matchItemUIClone.Id = matchItem.Id; 
            _matchItemUIs.Add(matchItemUIClone); 

            if (matchItemUIClone.btnComp)
            {
                matchItemUIClone.btnComp.onClick.RemoveAllListeners();
                matchItemUIClone.btnComp.onClick.AddListener(() =>
                {
                    if (_isAnswerChecking)
                        return; 

                    _answers.Add(matchItemUIClone);
                    matchItemUIClone.OpenAnimTrigger();
                    if (_answers.Count == 2)
                    {
                        _totalMoving++;
                        if (GUIManagerG3.Ins)
                            GUIManagerG3.Ins.UpdateScore(_totalMoving);
                        _isAnswerChecking = true; 
                        StartCoroutine(CheckAnswerCo());
                    }

                    matchItemUIClone.btnComp.enabled = false; 
                });
            }
        }
    }

    private IEnumerator CheckAnswerCo()
    {
        bool isRight = _answers[0] != null && _answers[1] != null
            && _answers[0].Id == _answers[1].Id; 

        yield return new WaitForSeconds(1);

        if (_answers != null && _answers.Count == 2)
        {
            if (isRight)
            {
                _rightMoving++;
                for (int i = 0; i < _answers.Count; i++)
                {
                    var answer = _answers[i];
                    if (answer)
                        answer.ExplodeAnimTrigger();
                    if (AudioControllerG3.Ins)
                    {
                        AudioControllerG3.Ins.PlaySound(AudioControllerG3.Ins.right);
                    }
                }
            }
            else
            {
                for (int i = 0; i < _answers.Count; i++)
                {
                    var answer = _answers[i];
                    if (answer)
                        answer.OpenAnimTrigger();

                    if (AudioControllerG3.Ins)
                    {
                        AudioControllerG3.Ins.PlaySound(AudioControllerG3.Ins.wrong);
                    }
                }
            }
        }
        _answers.Clear(); 
        _isAnswerChecking = false;

        if (_rightMoving == _totalMatchItems)
        {
            state = GameStateG3.Gameover;  

            if (_aiOpponent != null)
                _aiOpponent.StopAutoScore();

            if (GUIManagerG3.Ins)
            {
                GUIManagerG3.Ins.gameoverDialog.Show(true);
            }
            if (AudioControllerG3.Ins)
            {
                AudioControllerG3.Ins.PlaySound(AudioControllerG3.Ins.gameover);
            }
            Debug.Log("Game Over! You win!");
        }
    }

    private void ShuffleMatchItems() 
    {
        if (_matchItemsCopy == null || _matchItemsCopy.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < _matchItemsCopy.Count; i++)
        {
            var temp = _matchItemsCopy[i];
            if (temp != null)
            {
                int randIdx = Random.Range(0, _matchItemsCopy.Count);
                _matchItemsCopy[i] = _matchItemsCopy[randIdx];
                _matchItemsCopy[randIdx] = temp;
            }
        }
    }

    private void ClearGrid() 
    {
        if (gridRoot == null)
            return;

        for (int i = 0; i < gridRoot.childCount; i++)
        {
            var child = gridRoot.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
