using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;
    
    [Header("UI References")]
    public GameObject settingsPanel; // Panel cài đặt
    public Button settingsButton; // Nút mở cài đặt
    public Button closeSettingsButton; // Nút đóng cài đặt
    
    [Header("Game State")]
    public bool isPaused = false;
    public bool isInSettings = false;
    
    // Lưu trạng thái game trước khi pause
    private float savedTimeScale;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Thiết lập các nút
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);
            
        if (closeSettingsButton != null)
            closeSettingsButton.onClick.AddListener(CloseSettings);
            
        // Ẩn panel cài đặt ban đầu
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
    
    void Update()
    {
        // Kiểm tra phím ESC để mở/đóng cài đặt
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isInSettings)
                CloseSettings();
            else
                OpenSettings();
        }
    }
    
    public void OpenSettings()
    {
        if (isInSettings) return;
        
        isInSettings = true;
        
        // Lưu time scale hiện tại
        savedTimeScale = Time.timeScale;
        
        // Tạm dừng game
        PauseGame();
        
        // Hiện panel cài đặt
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
            
        Debug.Log("Settings opened - Game paused");
    }
    
    public void CloseSettings()
    {
        if (!isInSettings) return;
        
        isInSettings = false;
        
        // Ẩn panel cài đặt
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
            
        // Tiếp tục game
        ResumeGame();
        
        Debug.Log("Settings closed - Game resumed");
    }
    
    public void PauseGame()
    {
        if (isPaused) return;
        
        isPaused = true;
        Time.timeScale = 0f; // Dừng thời gian game
        
        // Tạm dừng tất cả audio
        AudioListener.pause = true;
        
        Debug.Log("Game paused");
    }
    
    public void ResumeGame()
    {
        if (!isPaused) return;
        
        isPaused = false;
        Time.timeScale = savedTimeScale; // Khôi phục thời gian game
        
        // Tiếp tục audio
        AudioListener.pause = false;
        
        Debug.Log("Game resumed");
    }
    
    public void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }
    
    // Phương thức để các script khác kiểm tra trạng thái pause
    public bool IsGamePaused()
    {
        return isPaused;
    }
    
    public bool IsInSettings()
    {
        return isInSettings;
    }
    
    // Phương thức để reset về trạng thái bình thường
    public void ResetToNormal()
    {
        isPaused = false;
        isInSettings = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
}

