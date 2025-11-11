using UnityEngine;

/// <summary>
/// Quản lý phát nhạc nền (Background Music)
/// </summary>
[DisallowMultipleComponent]
public class AudioController : MonoBehaviour
{
    public static AudioController Ins { get; private set; }

    [Header("Background Music")]
    [SerializeField] private AudioSource bgmSource;   // Kéo AudioSource từ Inspector
    [SerializeField] private AudioClip gameSceneMusic; // Kéo nhạc nền cho GameScene
    [SerializeField] private bool loop = true;

    void Awake()
    {
        // Đảm bảo singleton và không bị hủy khi đổi scene
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }
        Ins = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();

        bgmSource.loop = loop;
        bgmSource.playOnAwake = false;
        bgmSource.volume = 0.5f;
    }

    void Start()
    {
        PlayGameSceneMusic();
    }

    /// <summary>
    /// Phát nhạc nền cho GameScene
    /// </summary>
    public void PlayGameSceneMusic()
    {
        if (gameSceneMusic == null)
        {
            Debug.LogWarning("[AudioController] Chưa gán AudioClip cho gameSceneMusic!");
            return;
        }

        bgmSource.clip = gameSceneMusic;
        bgmSource.Play();
    }

    /// <summary>
    /// Tạm dừng hoặc phát lại nhạc nền
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (paused) bgmSource.Pause();
        else bgmSource.UnPause();
    }

    /// <summary>
    /// Thay nhạc nền khác (nếu cần chuyển scene)
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopMusic()
    {
        if(bgmSource.isPlaying)
            bgmSource.Stop();
    }
}
