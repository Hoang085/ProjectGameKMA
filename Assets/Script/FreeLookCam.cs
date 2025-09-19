using UnityEngine;
using Cinemachine;
using System.Collections;

// Quản lý camera FreeLook, theo dõi người chơi và lưu game
public class FreeLookCam : MonoBehaviour
{
    public CinemachineFreeLook freeLookCam;        
    public PlayerSaveManager playerSaveManager;  

    [Header("Behavior")]
    public bool lockWhenDialogueOpen = true;    

    public bool saveOnPause = true;                   // Lưu khi tạm dừng game
    public bool saveOnQuit = true;                    // Lưu khi thoát game

    // cache thông số để khôi phục
    float _xSpeed0, _ySpeed0;
    string _xAxisName0, _yAxisName0;
    bool _isLocked;

    void Awake()
    {
        if (!playerSaveManager) playerSaveManager = SafeFindFirst<PlayerSaveManager>();
        if (!freeLookCam) freeLookCam = SafeFindFirst<CinemachineFreeLook>();

        if (freeLookCam)
        {
            // Lưu lại cấu hình gốc để còn khôi phục
            _xSpeed0 = freeLookCam.m_XAxis.m_MaxSpeed;
            _ySpeed0 = freeLookCam.m_YAxis.m_MaxSpeed;
            _xAxisName0 = freeLookCam.m_XAxis.m_InputAxisName;
            _yAxisName0 = freeLookCam.m_YAxis.m_InputAxisName;
        }
    }

    void Start()
    {
        StartCoroutine(BindWhenReady());
    }

    void Update()
    {
        if (!lockWhenDialogueOpen || !freeLookCam) return;

        bool wantLock = GameUIManager.Ins && GameUIManager.Ins.IsDialogueOpen;
        if (wantLock != _isLocked)
        {
            ApplyLock(wantLock);
            _isLocked = wantLock;
        }
    }

    void ApplyLock(bool locked)
    {
        if (!freeLookCam) return;

        if (locked)
        {
            // Khóa xoay: set speed = 0 + tắt input axis
            freeLookCam.m_XAxis.m_MaxSpeed = 0f;
            freeLookCam.m_YAxis.m_MaxSpeed = 0f;

            freeLookCam.m_XAxis.m_InputAxisName = string.Empty;
            freeLookCam.m_YAxis.m_InputAxisName = string.Empty;

            // cũng reset giá trị input tức thời về 0 để dừng ngay lập tức
            freeLookCam.m_XAxis.m_InputAxisValue = 0f;
            freeLookCam.m_YAxis.m_InputAxisValue = 0f;
        }
        else
        {
            // Mở khóa: khôi phục cấu hình gốc
            freeLookCam.m_XAxis.m_MaxSpeed = _xSpeed0;
            freeLookCam.m_YAxis.m_MaxSpeed = _ySpeed0;

            freeLookCam.m_XAxis.m_InputAxisName = _xAxisName0;
            freeLookCam.m_YAxis.m_InputAxisName = _yAxisName0;
        }
    }

    IEnumerator BindWhenReady()
    {
        while (!playerSaveManager || !playerSaveManager.PlayerInstance)
            yield return null;

        BindTo(playerSaveManager.PlayerInstance); // Gán camera với người chơi
    }

    public void BindTo(GameObject playerInstance)
    {
        if (!freeLookCam || !playerInstance) return;

        var t = playerInstance.transform;
        var lookAt = t.Find("LookAt") ?? t;

        freeLookCam.Follow = t;
        freeLookCam.LookAt = lookAt;
    }

    void OnApplicationPause(bool paused)
    {
        if (saveOnPause && paused)
            playerSaveManager?.SaveNow();
    }

    void OnApplicationQuit()
    {
        if (saveOnQuit)
            playerSaveManager?.SaveNow();
    }

    // Tìm instance đầu tiên của T, hỗ trợ Unity cũ và mới
    static T SafeFindFirst<T>(bool includeInactive = false) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return includeInactive
            ? FindFirstObjectByType<T>(FindObjectsInactive.Include)
            : FindFirstObjectByType<T>();
#else
        return includeInactive
            ? Object.FindObjectOfType<T>(true)
            : Object.FindObjectOfType<T>();
#endif
    }
}
