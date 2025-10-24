using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EndOfSemesterNotice : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject noticeRoot;   // panel con để bật/tắt
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text contentText;

    [Header("Pause Options")]
    [SerializeField] private bool pauseWhenShown = true;      // dùng timeScale + audio
    [SerializeField] private bool disableClockUI = true;      // tắt ClockUI (đang dùng unscaled)
    [SerializeField] private bool disablePlayerController = true;
    [SerializeField] private bool disableFreeLookCam = true;
    [SerializeField] private List<MonoBehaviour> extraToDisable; // thêm script khác nếu cần (DayNightLighting, TickerMono, v.v.)

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

    private void Awake()
    {
        if (noticeRoot == null) noticeRoot = gameObject;
        if (confirmButton == null) confirmButton = GetComponentInChildren<Button>(true);
        if (contentText == null) contentText = GetComponentInChildren<TMP_Text>(true);

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(HideNotice);
            confirmButton.onClick.AddListener(HideNotice);
        }

        if (noticeRoot != null)
            noticeRoot.SetActive(false);
    }

    // === API được gọi từ GameUIManager ===
    public void TryShowForTerm(int term)
    {
        ShowIfNotShown(term);
    }

    private void ShowIfNotShown(int term)
    {
        int lastShown = PlayerPrefs.GetInt(PP_LAST_SHOWN_TERM, -1);
        if (lastShown == term) return;

        if (contentText != null)
        {
            int prev = Mathf.Max(1, term - 1);
            contentText.text =
                $"Bạn đã hoàn thành các môn học của học kỳ {prev}.\n" +
                $"Chuẩn bị tinh thần để bước sang học kỳ {term} nào!";
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (noticeRoot != null) noticeRoot.SetActive(true);

        ApplyPause(); // ⛔ dừng mọi hoạt động

        PlayerPrefs.SetInt(PP_LAST_SHOWN_TERM, term);
        PlayerPrefs.Save();
        Debug.Log($"[EOS] Shown notice for term {term}");
    }

    public void HideNotice()
    {
        if (noticeRoot != null) noticeRoot.SetActive(false);
        ReleasePause(); // ▶️ tiếp tục
        Debug.Log("[EOS] Notice closed");
    }

    // =============== PAUSE PACK ===============
    private void ApplyPause()
    {
        if (_paused) return;
        _paused = true;

        // cache refs 1 lần nếu chưa có
        if (disableClockUI && _clockUI == null)
            _clockUI = FindIncludingInactive<ClockUI>();

        if (disablePlayerController && _playerController == null)
            _playerController = FindIncludingInactive<PlayerController>();

        if (disableFreeLookCam && _freeLookCam == null)
            _freeLookCam = FindIncludingInactive<FreeLookCam>();

        // Lưu timeScale & audio
        _prevTimeScale = Time.timeScale;
        _prevAudioPause = AudioListener.pause;

        // Dừng “thế giới”
        if (pauseWhenShown)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        // Tắt các component unscaled / input
        _enabledBefore.Clear();
        DisableIfNotNull(_clockUI);
        DisableIfNotNull(_playerController);
        DisableIfNotNull(_freeLookCam);
        if (extraToDisable != null)
            foreach (var mb in extraToDisable)
                DisableIfNotNull(mb);

        // mở chuột nếu đang lock
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ReleasePause()
    {
        if (!_paused) return;
        _paused = false;

        // Khôi phục timeScale & audio
        if (pauseWhenShown)
        {
            Time.timeScale = _prevTimeScale;
            AudioListener.pause = _prevAudioPause;
        }

        // Khôi phục trạng thái enabled ban đầu
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

    // === Helper để tìm object kể cả khi inactive (hoạt động trên Unity 6 và cũ hơn) ===
    private static T FindIncludingInactive<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<T>(true);
#endif
    }
}
