using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio Settings")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    
    [Header("Game Settings")]
    public Toggle vibrationToggle;
    public Toggle soundToggle;
    public Toggle musicToggle;
    
    [Header("Graphics Settings")]
    public Dropdown qualityDropdown;
    public Toggle fullscreenToggle;
    
    [Header("Default Values")]
    public float defaultMasterVolume = 1f;
    public float defaultMusicVolume = 0.8f;
    public float defaultSfxVolume = 1f;
    public bool defaultVibration = true;
    public bool defaultSound = true;
    public bool defaultMusic = true;
    public int defaultQuality = 2; // Medium quality
    public bool defaultFullscreen = true;
    
    // Lưu trữ giá trị hiện tại
    private float currentMasterVolume;
    private float currentMusicVolume;
    private float currentSfxVolume;
    private bool currentVibration;
    private bool currentSound;
    private bool currentMusic;
    private int currentQuality;
    private bool currentFullscreen;
    
    void Start()
    {
        LoadSettings();
        SetupUI();
    }
    
    void SetupUI()
    {
        // Thiết lập sliders
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = currentMasterVolume;
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }
        
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = currentMusicVolume;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
        
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = currentSfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
        
        // Thiết lập toggles
        if (vibrationToggle != null)
        {
            vibrationToggle.isOn = currentVibration;
            vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);
        }
        
        if (soundToggle != null)
        {
            soundToggle.isOn = currentSound;
            soundToggle.onValueChanged.AddListener(OnSoundChanged);
        }
        
        if (musicToggle != null)
        {
            musicToggle.isOn = currentMusic;
            musicToggle.onValueChanged.AddListener(OnMusicChanged);
        }
        
        // Thiết lập dropdown và toggle khác
        if (qualityDropdown != null)
        {
            qualityDropdown.value = currentQuality;
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }
        
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = currentFullscreen;
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }
    }
    
    // Audio Settings
    public void OnMasterVolumeChanged(float value)
    {
        currentMasterVolume = value;
        AudioListener.volume = value;
        SaveSettings();
    }
    
    public void OnMusicVolumeChanged(float value)
    {
        currentMusicVolume = value;
        // Áp dụng cho music audio source
        // AudioManager.Instance.SetMusicVolume(value);
        SaveSettings();
    }
    
    public void OnSfxVolumeChanged(float value)
    {
        currentSfxVolume = value;
        // Áp dụng cho SFX audio source
        // AudioManager.Instance.SetSfxVolume(value);
        SaveSettings();
    }
    
    // Game Settings
    public void OnVibrationChanged(bool value)
    {
        currentVibration = value;
        SaveSettings();
    }
    
    public void OnSoundChanged(bool value)
    {
        currentSound = value;
        AudioListener.volume = value ? currentMasterVolume : 0f;
        SaveSettings();
    }
    
    public void OnMusicChanged(bool value)
    {
        currentMusic = value;
        // Tắt/bật music
        // AudioManager.Instance.SetMusicEnabled(value);
        SaveSettings();
    }
    
    // Graphics Settings
    public void OnQualityChanged(int value)
    {
        currentQuality = value;
        QualitySettings.SetQualityLevel(value);
        SaveSettings();
    }
    
    public void OnFullscreenChanged(bool value)
    {
        currentFullscreen = value;
        Screen.fullScreen = value;
        SaveSettings();
    }
    
    // Lưu cài đặt
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", currentMasterVolume);
        PlayerPrefs.SetFloat("MusicVolume", currentMusicVolume);
        PlayerPrefs.SetFloat("SfxVolume", currentSfxVolume);
        PlayerPrefs.SetInt("Vibration", currentVibration ? 1 : 0);
        PlayerPrefs.SetInt("Sound", currentSound ? 1 : 0);
        PlayerPrefs.SetInt("Music", currentMusic ? 1 : 0);
        PlayerPrefs.SetInt("Quality", currentQuality);
        PlayerPrefs.SetInt("Fullscreen", currentFullscreen ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log("Settings saved");
    }
    
    // Tải cài đặt
    public void LoadSettings()
    {
        currentMasterVolume = PlayerPrefs.GetFloat("MasterVolume", defaultMasterVolume);
        currentMusicVolume = PlayerPrefs.GetFloat("MusicVolume", defaultMusicVolume);
        currentSfxVolume = PlayerPrefs.GetFloat("SfxVolume", defaultSfxVolume);
        currentVibration = PlayerPrefs.GetInt("Vibration", defaultVibration ? 1 : 0) == 1;
        currentSound = PlayerPrefs.GetInt("Sound", defaultSound ? 1 : 0) == 1;
        currentMusic = PlayerPrefs.GetInt("Music", defaultMusic ? 1 : 0) == 1;
        currentQuality = PlayerPrefs.GetInt("Quality", defaultQuality);
        currentFullscreen = PlayerPrefs.GetInt("Fullscreen", defaultFullscreen ? 1 : 0) == 1;
        
        // Áp dụng cài đặt
        AudioListener.volume = currentSound ? currentMasterVolume : 0f;
        QualitySettings.SetQualityLevel(currentQuality);
        Screen.fullScreen = currentFullscreen;
    }
    
    // Reset về mặc định
    public void ResetToDefault()
    {
        currentMasterVolume = defaultMasterVolume;
        currentMusicVolume = defaultMusicVolume;
        currentSfxVolume = defaultSfxVolume;
        currentVibration = defaultVibration;
        currentSound = defaultSound;
        currentMusic = defaultMusic;
        currentQuality = defaultQuality;
        currentFullscreen = defaultFullscreen;
        
        SetupUI();
        SaveSettings();
        
        Debug.Log("Settings reset to default");
    }
    
    // Đóng cài đặt và quay lại game
    public void CloseSettings()
    {
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.CloseSettings();
        }
    }
}

