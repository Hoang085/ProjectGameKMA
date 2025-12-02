using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameEndingManager : MonoBehaviour
{
    public static GameEndingManager Ins { get; private set; }

    [Header("FRIENDSHIP ENDING – Điều kiện kích hoạt")]
    [SerializeField] private bool enableFriendshipEnding = true;
    [Tooltip("Ngưỡng điểm thân thiện để kích hoạt ending")]
    [SerializeField] private int friendshipThreshold = 100;

    [Header("Video Ending")]
    [SerializeField] private bool playVideo = true;
    [Tooltip("VideoProfile dùng để phát ending bạn bè")]
    [SerializeField] private VideoProfile friendshipEndingVideo;
    [Tooltip("Popup video dùng chung trong project (giống PedestalInteraction)")]
    [SerializeField] private VideoPopupUI videoPopup;

    [Header("Notification Ending")]
    [Tooltip("NoticationEndingGame object để hiển thị thông điệp sau khi video kết thúc")]
    [SerializeField] private NoticationEndingGame noticationEndingGame;

    [Header("Kết thúc game sau khi video chạy xong")]
    [Tooltip("Delay nhỏ sau khi video kết thúc trước khi load scene / thoát game")]
    [SerializeField] private float delayAfterVideo = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private float checkInterval = 1f; 

    private bool _endingTriggered;
    private bool _waitingForVideo;
    private float _lastCheckTime;

    private const string ENDING_TRIGGERED_KEY = "PLAYER_ENDING_TRIGGERED";

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }
        Ins = this;

        _endingTriggered = PlayerPrefs.GetInt(ENDING_TRIGGERED_KEY, 0) == 1;
    }

    private void OnEnable()
    {
        _waitingForVideo = false;
        _lastCheckTime = 0f;
    }

    private void OnDisable()
    {

    }

    private void Update()
    {
        if (!enableFriendshipEnding) return;
        if (_endingTriggered) return;
        if (_waitingForVideo) return;

        if (Time.time - _lastCheckTime < checkInterval) return;
        _lastCheckTime = Time.time;

        if (GameManager.Ins == null)
        {
            return;
        }

        int currentFriendly = GameManager.Ins.GetFriendlyPoint();
        
        if (showDebugLogs && Time.frameCount % 600 == 0) 
        {
            DebugLog($"[GameEndingManager] Điểm thân thiện hiện tại: {currentFriendly}/{friendshipThreshold}");
        }

        if (currentFriendly >= friendshipThreshold)
        {
            DebugLog($"[GameEndingManager] Đạt ngưỡng điểm thân thiện! ({currentFriendly}/{friendshipThreshold})");
            TriggerFriendshipEnding();
        }
    }

    /// <summary>
    /// Gọi hàm này khi muốn ép kích hoạt ending bạn bè từ chỗ khác
    /// </summary>
    public void TriggerFriendshipEnding()
    {
        if (_endingTriggered)
        {
            return;
        }
        _endingTriggered = true;
        
        PlayerPrefs.SetInt(ENDING_TRIGGERED_KEY, 1);
        PlayerPrefs.Save();
        
        StartCoroutine(CoPlayFriendshipEnding());
    }

    /// <summary>
    /// Logic chính: khóa player -> phát video (nếu có) -> hiển thị notification -> end game
    /// </summary>
    private IEnumerator CoPlayFriendshipEnding()
    {
        Time.timeScale = 0f;

        if (playVideo && friendshipEndingVideo != null)
        {
            if (videoPopup)
            {
                _waitingForVideo = true;

                bool wasInactive = !videoPopup.gameObject.activeInHierarchy;
                if (wasInactive)
                {
                    videoPopup.gameObject.SetActive(true);
                    yield return null; 
                }

                videoPopup.PlayProfile_Inspector(friendshipEndingVideo);
                yield return videoPopup.WaitUntilFinished();

                DebugLog("[GameEndingManager] ✓ Video ending đã kết thúc!");
                _waitingForVideo = false;
            }
            else
            {
                DebugLog("[GameEndingManager] ✗ Không tìm thấy VideoPopupUI để phát video ending!", isWarning: true);
            }
        }
        else
        {
            DebugLog("[GameEndingManager] Không cấu hình video ending hoặc tắt playVideo, skip phần video.");
        }
        
        Time.timeScale = 1f;
        
        // **MỚI: Hiển thị NoticationEndingGame với message 1**
        if (noticationEndingGame != null)
        {
            DebugLog("[GameEndingManager] Hiển thị NoticationEndingGame với thông điệp 1...");
            noticationEndingGame.gameObject.SetActive(true);
            noticationEndingGame.GetMes1();
            
            // Không kết thúc game ngay - chờ người chơi click nút trong notification
            DebugLog("[GameEndingManager] ✓ Đã hiển thị notification ending - chờ người chơi tương tác");
        }
        else
        {
            DebugLog("[GameEndingManager] ✗ Không tìm thấy NoticationEndingGame!", isWarning: true);
            
            // Nếu không có notification, thực hiện flow cũ
            if (delayAfterVideo > 0f)
            {
                DebugLog($"[GameEndingManager] Chờ {delayAfterVideo}s trước khi kết thúc game...");
                yield return new WaitForSeconds(delayAfterVideo);
            }

            EndGame();
        }
    }

    private void EndGame()
    {
        Time.timeScale = 1f;
        
        SceneLoader.Load("MainMenu");
    }

    /// <summary>
    /// **MỚI: Helper method để debug có điều kiện**
    /// </summary>
    private void DebugLog(string message, bool isWarning = false)
    {
        if (!showDebugLogs) return;

        if (isWarning)
        {
            Debug.LogWarning(message);
        }
        else
        {
            Debug.Log(message);
        }
    }
}
