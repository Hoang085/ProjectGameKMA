using UnityEngine;

[DisallowMultipleComponent]
public class AudioController : MonoBehaviour
{
    public static AudioController Ins { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    
    public AudioSource MusicSource => musicSource;
    public AudioSource SfxSource => sfxSource;

    [Header("Audio Clips (Kéo file âm thanh vào đây)")]
    [SerializeField] private AudioClip backgroundMusic; 
    [SerializeField] private AudioClip onClickSound; 

    [Header("Settings")]
    [SerializeField] private bool loopMusic = true;
    
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }
        Ins = this;

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = loopMusic;
            musicSource.playOnAwake = false;
            musicSource.volume = 1f;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0.5f; 
        }
        
        LoadVolume();
    }

    void Start()
    {
        PlayMusic(backgroundMusic);
    }

    void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }
    
    private void LoadVolume()
    {
        if (musicSource != null)
        {
            musicSource.volume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
        }
        
        if (sfxSource != null)
        {
            sfxSource.volume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource.isPlaying) musicSource.Stop();
    }


    /// <summary>
    /// Gọi hàm này khi người chơi bấm nút (Button OnClick)
    /// </summary>
    public void PlayClickSound()
    {
        PlaySFX(onClickSound);
    }

    /// <summary>
    /// Hàm gốc để phát SFX (dùng nội bộ hoặc cho các âm khác nếu cần sau này)
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }
}