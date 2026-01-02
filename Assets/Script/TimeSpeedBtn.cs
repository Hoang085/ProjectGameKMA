using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TimeSpeedBtn : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Time Speed Settings")]
    [Tooltip("Hệ số nhân tốc độ khi giữ nút (ví dụ: 0.1 = tua nhanh 10 lần)")]
    [Min(0.01f)]
    public float fastForwardMultiplier = 0.1f;

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private Button buttonImage;

    private float _originalSecondsPerMinute;
    private bool _isHolding;

    void Start()
    {
        if (GameClock.Ins)
        {
            _originalSecondsPerMinute = GameClock.Ins.secondsPerGameMinute;
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

        // Lưu tốc độ gốc
        _originalSecondsPerMinute = GameClock.Ins.secondsPerGameMinute;

        // Reset bộ đếm tích lũy để tránh nhảy thời gian
        GameClock.Ins.ResetTimeAccumulator();

        // Áp dụng hệ số nhân để tua nhanh (secondsPerGameMinute nhỏ hơn = thời gian chạy nhanh hơn)
        GameClock.Ins.secondsPerGameMinute = _originalSecondsPerMinute * fastForwardMultiplier;

        _isHolding = true;

        Debug.Log($"[TimeSpeedBtn] Tua nhanh: {_originalSecondsPerMinute}s → {GameClock.Ins.secondsPerGameMinute}s per game minute");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        RestoreNormalSpeed();
    }

    private void RestoreNormalSpeed()
    {
        if (!GameClock.Ins || !_isHolding) return;

        // Reset bộ đếm tích lũy trước khi khôi phục tốc độ
        GameClock.Ins.ResetTimeAccumulator();

        // Khôi phục tốc độ ban đầu
        GameClock.Ins.secondsPerGameMinute = _originalSecondsPerMinute;
        _isHolding = false;

        Debug.Log($"[TimeSpeedBtn] Trở lại tốc độ bình thường: {_originalSecondsPerMinute}s per game minute");
    }
}
