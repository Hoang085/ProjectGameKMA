using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using HHH.Common; // dùng BasePopUp & PopupManager

public class EndOfSemesterNotice : BasePopUp
{
    [Header("Refs")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text contentText;

    [Header("Pause Options")]
    [SerializeField] private bool pauseWhenShown = true;
    [SerializeField] private bool disableClockUI = true;
    [SerializeField] private bool disablePlayerController = true;
    [SerializeField] private bool disableFreeLookCam = true;
    [SerializeField] private List<MonoBehaviour> extraToDisable;

    private const string PP_LAST_SHOWN_TERM = "EOS_LastShownTerm";

    // cache
    private ClockUI _clockUI;
    private PlayerController _playerController;
    private FreeLookCam _freeLookCam;
    private bool _paused;
    private float _prevTimeScale;
    private bool _prevAudioPause;

    // lưu trạng thái enabled để khôi phục
    private readonly Dictionary<Behaviour, bool> _enabledBefore = new();

    public override void OnInitScreen()
    {
        base.OnInitScreen();
        if (!confirmButton) confirmButton = GetComponentInChildren<Button>(true);
        if (!contentText) contentText = GetComponentInChildren<TMP_Text>(true);

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                // Thả pause trước để game tiếp tục trong lúc popup fade-out
                ReleasePause();
                OnCloseScreen(); // dùng lifecycle của BasePopUp (đóng có tween)
            });
        }
    }

    /// <summary>
    /// Gọi ở bất kỳ đâu để hiển thị EOS nếu chưa hiển thị cho kỳ này.
    /// </summary>
    public static void TryShowForTerm(int term)
    {
        int lastShown = PlayerPrefs.GetInt(PP_LAST_SHOWN_TERM, -1);
        if (lastShown == term) return;

        // Hiển thị qua PopupManager, truyền term làm arg
        PopupManager.Ins.OnShowScreen(PopupName.EndOfSemesterNotice, term);
    }

    public override void OnShowScreen(object arg)
    {
        // 1) Soạn nội dung theo term (lấy từ arg hoặc GameClock)
        int term = (arg is int t) ? t : (GameClock.Ins ? GameClock.Ins.Term : 1);
        if (contentText != null)
        {
            int prev = Mathf.Max(1, term - 1);
            contentText.text =
                $"Bạn đã hoàn thành các môn học của học kỳ {prev}.\n" +
                $"Chuẩn bị tinh thần để bước sang học kỳ {term} nào!";
        }

        // 2) Áp dụng pause/input lock trước khi chạy hiệu ứng mở popup (tween chạy unscaled)
        ApplyPause();

        // 3) Ghi nhớ đã show
        PlayerPrefs.SetInt(PP_LAST_SHOWN_TERM, term);
        PlayerPrefs.Save();

        // 4) Gọi animation mở của BasePopUp
        base.OnShowScreen(arg);
    }

    public override void OnShowScreen()
    {
        // fallback nếu ai đó gọi overload không arg
        OnShowScreen(GameClock.Ins ? (object)GameClock.Ins.Term : 1);
    }

    // =============== PAUSE PACK ===============
    private void ApplyPause()
    {
        if (_paused) return;
        _paused = true;

        if (disableClockUI && _clockUI == null)
            _clockUI = FindIncludingInactive<ClockUI>();
        if (disablePlayerController && _playerController == null)
            _playerController = FindIncludingInactive<PlayerController>();
        if (disableFreeLookCam && _freeLookCam == null)
            _freeLookCam = FindIncludingInactive<FreeLookCam>();

        _prevTimeScale = Time.timeScale;
        _prevAudioPause = AudioListener.pause;

        if (pauseWhenShown)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        _enabledBefore.Clear();
        DisableIfNotNull(_clockUI);
        DisableIfNotNull(_playerController);
        DisableIfNotNull(_freeLookCam);
        if (extraToDisable != null)
            foreach (var mb in extraToDisable)
                DisableIfNotNull(mb);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ReleasePause()
    {
        if (!_paused) return;
        _paused = false;

        if (pauseWhenShown)
        {
            Time.timeScale = _prevTimeScale;
            AudioListener.pause = _prevAudioPause;
        }

        foreach (var kv in _enabledBefore)
        {
            if (kv.Key != null)
                kv.Key.enabled = kv.Value;
        }
        _enabledBefore.Clear();
    }

    private void DisableIfNotNull(Behaviour b)
    {
        if (b == null) return;
        if (!_enabledBefore.ContainsKey(b))
            _enabledBefore[b] = b.enabled;
        b.enabled = false;
    }

    private static T FindIncludingInactive<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<T>(true);
#endif
    }
}
