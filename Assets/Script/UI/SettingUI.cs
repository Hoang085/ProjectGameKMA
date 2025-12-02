using UnityEngine;
using UnityEngine.UI;
using HHH.Common;
using TMPro;

public class SettingUI : BasePopUp
{
    [Header("UI References - Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("UI References - Texts")]
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    [Header("UI References - Icons (Kéo Image cái loa vào đây)")]
    [SerializeField] private Image musicIconImage; 
    [SerializeField] private Image sfxIconImage;  

    [Header("Icon Sprites (Kéo hình ảnh vào đây)")]
    [SerializeField] private Sprite soundOnSprite;  
    [SerializeField] private Sprite soundOffSprite;

    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    public override void OnInitScreen()
    {
        base.OnInitScreen();

        if (AudioController.Ins == null) return;

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveAllListeners();

            float currentMusicVol = AudioController.Ins.MusicSource.volume;
            musicSlider.value = currentMusicVol;

            UpdateMusicTextDisplay(currentMusicVol);
            UpdateIcon(musicIconImage, currentMusicVol); 

            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();

            float currentSfxVol = AudioController.Ins.SfxSource.volume;
            sfxSlider.value = currentSfxVol;

            UpdateSfxTextDisplay(currentSfxVol);
            UpdateIcon(sfxIconImage, currentSfxVol);

            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioController.Ins != null)
        {
            AudioController.Ins.MusicSource.volume = value;
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);

            UpdateMusicTextDisplay(value);
            UpdateIcon(musicIconImage, value);
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioController.Ins != null)
        {
            AudioController.Ins.SfxSource.volume = value;
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, value);

            UpdateSfxTextDisplay(value);
            UpdateIcon(sfxIconImage, value); 
        }
    }

    private void UpdateIcon(Image targetImage, float value)
    {
        if (targetImage == null) return;

        if (value <= 0.01f)
        {
            if (soundOffSprite != null) targetImage.sprite = soundOffSprite;
        }
        else
        {
            if (soundOnSprite != null) targetImage.sprite = soundOnSprite;
        }
    }

    private void UpdateMusicTextDisplay(float value)
    {
        if (musicValueText != null)
            musicValueText.text = Mathf.RoundToInt(value * 10).ToString();
    }

    private void UpdateSfxTextDisplay(float value)
    {
        if (sfxValueText != null)
            sfxValueText.text = Mathf.RoundToInt(value * 10).ToString();
    }

    public override void OnCloseScreen()
    {
        base.OnCloseScreen();
        PlayerPrefs.Save();
    }
}