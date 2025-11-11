using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoPopupUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;   // Panel hiển thị (có thể là chính GameObject)
    [SerializeField] private RawImage screen;        // RawImage hiển thị RenderTexture
    [SerializeField] private RenderTexture targetTexture;
    [SerializeField] private VideoPlayer player;

    [Header("Optional dim overlay (để áp dụng dimBackground)")]
    [SerializeField] private Image dimOverlay;       // 1 Image full screen (màu đen) để dim nền

    [Header("Behavior")]
    [SerializeField] private float prepareTimeout = 5f; // thời gian chờ Prepare()

    private bool _isPlaying;
    private VideoProfile _currentProfile;

    /// <summary> Video đang phát? </summary>
    public bool IsPlaying => _isPlaying;

    /// <summary> Bắn khi popup đóng. </summary>
    public event Action Closed;

    private void Reset()
    {
        if (!panelRoot) panelRoot = gameObject;
        if (!player) player = GetComponentInChildren<VideoPlayer>(true);
        if (!screen) screen = GetComponentInChildren<RawImage>(true);
    }

    private void Awake()
    {
        // Ẩn ngay từ đầu nếu muốn
        if (panelRoot) panelRoot.SetActive(false);

        if (player)
        {
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.isLooping = false;                           // auto close khi hết
            player.renderMode = VideoRenderMode.RenderTexture;
            if (targetTexture) player.targetTexture = targetTexture;
            if (screen && targetTexture) screen.texture = targetTexture;

            // Không cần âm thanh
            player.audioOutputMode = VideoAudioOutputMode.None;

            // Hết video thì tự Close()
            player.loopPointReached += OnVideoEnded;
        }
    }

    private void OnDestroy()
    {
        if (player) player.loopPointReached -= OnVideoEnded;
    }

    /// <summary>
    /// Adapter DÀNH CHO UNITYEVENT (void + 1 arg).
    /// Dùng cái này để bind trong Inspector: VideoPopupUI.PlayProfile_Inspector(VideoProfile).
    /// </summary>
    public void PlayProfile_Inspector(VideoProfile profile)
    {
        if (panelRoot && !panelRoot.activeSelf) panelRoot.SetActive(true);
        StartCoroutine(CoPlay(profile));
    }

    /// <summary>
    /// Nếu gọi bằng code trong script khác, bạn có thể dùng cái này.
    /// </summary>
    public Coroutine PlayProfile(VideoProfile profile)
    {
        if (panelRoot && !panelRoot.activeSelf) panelRoot.SetActive(true);
        return StartCoroutine(CoPlay(profile));
    }

    private IEnumerator CoPlay(VideoProfile profile)
    {
        _currentProfile = profile;

        if (!player)
        {
            Debug.LogError("[VideoPopupUI] Missing VideoPlayer");
            yield break;
        }
        if (!profile || !profile.clip)
        {
            Debug.LogError("[VideoPopupUI] VideoProfile thiếu clip!");
            yield break;
        }

        // Áp dim nền (nếu có Image dimOverlay)
        ApplyDim(profile.dimBackground);

        // Mở panel hiển thị
        if (panelRoot && !panelRoot.activeSelf) panelRoot.SetActive(true);

        // Chuẩn bị video
        player.source = VideoSource.VideoClip;
        player.clip = profile.clip;
        player.Prepare();

        float t = 0f;
        while (!player.isPrepared && t < prepareTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!player.isPrepared)
        {
            Debug.LogError("[VideoPopupUI] Prepare() timeout");
            Close();
            yield break;
        }

        // Phát
        _isPlaying = true;
        player.Play();
    }

    private void ApplyDim(float dimAmount01)
    {
        if (!dimOverlay) return;

        // dimAmount01 = 0..1 (0 = trong suốt, 1 = đen hoàn toàn)
        var c = dimOverlay.color;
        dimOverlay.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(dimAmount01));
    }

    private void OnVideoEnded(VideoPlayer _)
    {
        _isPlaying = false;
        Close();
    }

    /// <summary> Đóng popup: dừng phát, ẩn panel, bắn Closed. </summary>
    public void Close()
    {
        if (player && player.isPlaying) player.Stop();
        _isPlaying = false;

        if (panelRoot && panelRoot.activeSelf) panelRoot.SetActive(false);

        Closed?.Invoke();
    }

    /// <summary> Chờ tới khi thật sự kết thúc/đóng (không dựa vào activeSelf). </summary>
    public IEnumerator WaitUntilFinished()
    {
        // Đợi bắt đầu phát (phòng gọi quá sớm)
        while (this != null && player != null && !_isPlaying)
            yield return null;

        // Đợi tới khi _isPlaying = false
        while (this != null && player != null && _isPlaying)
            yield return null;
    }
}
