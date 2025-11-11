using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoPopupUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject root;        // Panel gốc (fullscreen, SetActive(false) ban đầu)
    [SerializeField] private RawImage screen;        // RawImage hiển thị
    [SerializeField] private CanvasGroup dim;        // CanvasGroup của nền đen mờ (tuỳ chọn)
    [SerializeField] private VideoPlayer player;     // VideoPlayer trên Panel
    [SerializeField] private RenderTexture targetTexture; // RenderTexture để hiển thị

    private VideoProfile _profile;

    private void Reset()
    {
        root = gameObject;
        screen = GetComponentInChildren<RawImage>(true);
        player = GetComponent<VideoPlayer>();
        dim = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (root) root.SetActive(false);

        // Cấu hình một lần
        if (player)
        {
            player.playOnAwake = false;
            player.waitForFirstFrame = true;

            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = targetTexture;

            if (screen && targetTexture) screen.texture = targetTexture;

            // Không dùng âm thanh
            player.audioOutputMode = VideoAudioOutputMode.None;
        }
    }

    private void OnEnable() { if (player) player.loopPointReached += OnFinished; }
    private void OnDisable() { if (player) player.loopPointReached -= OnFinished; }

    // API cũ: phát trực tiếp một clip
    public void Play(VideoClip clip)
    {
        var p = ScriptableObject.CreateInstance<VideoProfile>();
        p.clip = clip;
        PlayProfile(p);
    }

    // API chuẩn: phát theo profile
    public void PlayProfile(VideoProfile profile)
    {
        if (!player || profile == null || !profile.clip) return;

        _profile = profile;

        if (dim) dim.alpha = profile.dimBackground;
        player.clip = profile.clip;

        root.SetActive(true);

        // chuẩn bị rồi phát
        player.prepareCompleted -= StartPlayback; // tránh double-subscribe
        player.prepareCompleted += StartPlayback;
        player.Prepare();
    }

    private void StartPlayback(VideoPlayer vp)
    {
        vp.prepareCompleted -= StartPlayback;
        vp.Play();
    }

    private void OnFinished(VideoPlayer vp)
    {
        // tự đóng khi hết video
        Close();
    }

    public void Close()
    {
        if (player) player.Stop();
        if (root) root.SetActive(false);
        _profile = null;
    }
}
