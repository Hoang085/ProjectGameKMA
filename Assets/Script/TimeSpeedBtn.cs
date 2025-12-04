using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TimeSpeedBtn : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Time Speed Settings")]
    [Min(0.01f)]
    public float fastForwardSpeed = 1f;

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private Button buttonImage;

    private float _originalSpeed;
    private bool _isHolding;

    void Start()
    {
        if (GameClock.Ins)
        {
            _originalSpeed = GameClock.Ins.secondsPerGameMinute;
        }
    }

    void OnDisable()
    {
        if (_isHolding)
        {
            RestoreNormalSpeed();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!GameClock.Ins) return;
        _originalSpeed = GameClock.Ins.secondsPerGameMinute;
        GameClock.Ins.secondsPerGameMinute = fastForwardSpeed;
        _isHolding = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        RestoreNormalSpeed();
    }

    private void RestoreNormalSpeed()
    {
        if (!GameClock.Ins) return;
        GameClock.Ins.secondsPerGameMinute = _originalSpeed;
        _isHolding = false;
    }
}
