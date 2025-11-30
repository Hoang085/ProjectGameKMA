using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý việc lưu trữ và khôi phục trạng thái game khi chuyển scene thi
/// Thiết kế mới: Sử dụng các hệ thống có sẵn để lưu/khôi phục trạng thái
/// </summary>
public static class GameStateManager
{
    private const string SAVE_KEY = "GameState_PreExam";
    
    [Serializable]
    public class GameState
    {
        // Thông tin thi
        public string examSubject = "";
        public string sceneName = "GameScene";
        
        // Metadata
        public long saveTime;
        
        public GameState()
        {
            saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
    
    /// <summary>
    /// Lưu trạng thái trước khi vào thi
    /// </summary>
    public static void SavePreExamState(string examSubject)
    {
        try
        {
            var state = new GameState();
            state.examSubject = examSubject;
            state.sceneName = SceneManager.GetActiveScene().name;
            
            Debug.Log($"[GameStateManager] Bắt đầu lưu trạng thái trước thi '{examSubject}'");
            
            // 1. Lưu thời gian qua TimeSaveManager
            SaveTimeState();
            
            // 2. Lưu vị trí player qua PlayerSaveManager  
            SavePlayerState();
            
            // 3. Các hệ thống khác tự động lưu qua PlayerPrefs:
            // - AttendanceManager: Tự lưu điểm danh qua PlayerPrefs
            // - TaskManager: Không cần lưu vì sẽ refresh theo thời gian
            // - NotesService: Tự lưu notes qua PlayerPrefs
            // - IconNotificationManager: Sync với GameManager
            
            // 4. Lưu metadata exam
            string json = JsonUtility.ToJson(state);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
            
            Debug.Log($"[GameStateManager] Đã lưu trạng thái trước thi '{examSubject}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameStateManager] Lỗi lưu trạng thái: {ex.Message}");
        }
    }

    /// <summary>
    /// Khôi phục trạng thái sau khi thi xong
    /// </summary>
    // Trong GameStateManager.cs

    // 1. Đổi từ 'public static void' thành 'public static IEnumerator'
    public static System.Collections.IEnumerator RestorePostExamState()
    {
        if (!HasSavedState())
        {
            Debug.LogWarning("[GameStateManager] Không có trạng thái để khôi phục");
            yield break;
        }

        GameState state = null;
        try
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            state = JsonUtility.FromJson<GameState>(json);

            if (state == null)
            {
                Debug.LogError("[GameStateManager] Không thể parse dữ liệu");
                yield break;
            }

            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (currentTime - state.saveTime > 7200)
            {
                Debug.LogWarning("[GameStateManager] Dữ liệu quá cũ");
                ClearSavedState();
                yield break;
            }

            Debug.Log($"[GameStateManager] Bắt đầu khôi phục trạng thái từ thi '{state.examSubject}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameStateManager] Lỗi chuẩn bị khôi phục: {ex.Message}");
            ClearSavedState();
            yield break;
        }

        if (state != null)
        {
            yield return RestoreStateCoroutine(state);
        }
    }

    /// <summary>
    /// Coroutine khôi phục trạng thái
    /// </summary>
    private static System.Collections.IEnumerator RestoreStateCoroutine(GameState state)
    {
        Debug.Log("[GameStateManager] Bắt đầu khôi phục...");
        
        // Đợi các frame để đảm bảo scene đã load xong
        yield return null;
        yield return null;
        
        // 1. Khôi phục thời gian qua TimeSaveManager
        yield return RestoreTimeState();
        
        // 2. Khôi phục vị trí player (PlayerSaveManager tự động khôi phục trong Start)
        // Chỉ cần đợi nó hoàn thành
        yield return new UnityEngine.WaitForSeconds(0.5f);
        
        // 3. Refresh các hệ thống cần thiết
        yield return RefreshSystems();
        
        // 4. Hoàn thành
        yield return new UnityEngine.WaitForSeconds(0.2f);
        
        Debug.Log("[GameStateManager] Hoàn thành khôi phục trạng thái");
        
        // Xóa dữ liệu đã lưu
        ClearSavedState();
    }
    
    /// <summary>
    /// Lưu thời gian qua TimeSaveManager
    /// </summary>
    private static void SaveTimeState()
    {
        var timeSaveManager = TimeSaveManager.Ins;
        if (timeSaveManager != null)
        {
            timeSaveManager.Save();
            Debug.Log("[GameStateManager] Đã lưu thời gian qua TimeSaveManager");
        }
        else
        {
            Debug.LogWarning("[GameStateManager] TimeSaveManager không tìm thấy");
        }
    }
    
    /// <summary>
    /// Lưu vị trí player qua PlayerSaveManager
    /// </summary>
    private static void SavePlayerState()
    {
        var playerSaveManager = UnityEngine.Object.FindFirstObjectByType<PlayerSaveManager>();
        if (playerSaveManager != null)
        {
            playerSaveManager.SaveNow();
            Debug.Log("[GameStateManager] Đã lưu vị trí Player qua PlayerSaveManager");
        }
        else
        {
            Debug.LogWarning("[GameStateManager] PlayerSaveManager không tìm thấy");
        }
    }
    
    /// <summary>
    /// Khôi phục thời gian qua TimeSaveManager
    /// </summary>
    private static System.Collections.IEnumerator RestoreTimeState()
    {
        Debug.Log("[GameStateManager] Khôi phục thời gian qua TimeSaveManager...");
        
        var timeSaveManager = TimeSaveManager.Ins;
        if (timeSaveManager != null)
        {
            timeSaveManager.TryLoad();
            Debug.Log("[GameStateManager] Đã khôi phục thời gian qua TimeSaveManager");
        }
        else
        {
            Debug.LogWarning("[GameStateManager] TimeSaveManager không tìm thấy");
        }
        
        yield return new UnityEngine.WaitForSeconds(0.1f);
    }
    
    /// <summary>
    /// Refresh các hệ thống sau khi khôi phục
    /// </summary>
    private static System.Collections.IEnumerator RefreshSystems()
    {
        Debug.Log("[GameStateManager] Refresh các hệ thống...");
        
        // 1. Refresh TaskManager để cập nhật tasks theo thời gian mới
        var taskManager = TaskManager.Instance;
        if (taskManager != null)
        {
            taskManager.ForceRefreshTasks();
            taskManager.ResetTaskNotificationState();
            Debug.Log("[GameStateManager] Đã refresh TaskManager");
        }
        
        yield return new UnityEngine.WaitForSeconds(0.2f);
        
        // 2. **SỬA: Sử dụng GameManager method để sync notification system**
        if (GameManager.Ins != null)
        {
            GameManager.Ins.SyncNotificationSystemAfterRestore();
            Debug.Log("[GameStateManager] Đã sync notification system qua GameManager");
        }
        else
        {
            // Fallback: Sync trực tiếp với IconNotificationManager như cũ
            var iconNotificationManager = UnityEngine.Object.FindFirstObjectByType<IconNotificationManager>();
            if (iconNotificationManager != null)
            {
                // Force sync tất cả icon notifications
                foreach (IconType iconType in System.Enum.GetValues(typeof(IconType)))
                {
                    bool state = false; // Default state since GameManager is not available
                    iconNotificationManager.SetNotificationVisible(iconType, state);
                }
                Debug.Log("[GameStateManager] Đã sync IconNotificationManager (fallback)");
            }
        }
        
        yield return new UnityEngine.WaitForSeconds(0.1f);
        
        // 3. AttendanceManager và NotesService tự động hoạt động qua PlayerPrefs
        // Không cần thao tác gì thêm
        
        Debug.Log("[GameStateManager] Hoàn thành refresh hệ thống");
    }
    
    /// <summary>
    /// **THÊM: Method để GameManager có thể gọi khi cần refresh notifications**
    /// </summary>
    public static void TriggerNotificationRefresh()
    {
        if (GameManager.Ins != null)
        {
            GameManager.Ins.RefreshAllNotificationStatesAfterRestore();
            Debug.Log("[GameStateManager] Đã trigger notification refresh qua GameManager");
        }
    }
    
    /// <summary>
    /// Kiểm tra có dữ liệu đã lưu không
    /// </summary>
    public static bool HasSavedState()
    {
        return PlayerPrefs.HasKey(SAVE_KEY);
    }
    
    /// <summary>
    /// Lấy tên môn thi đã lưu
    /// </summary>
    public static string GetSavedExamSubject()
    {
        if (!HasSavedState()) return null;
        
        try
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            var state = JsonUtility.FromJson<GameState>(json);
            return state?.examSubject;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Xóa dữ liệu đã lưu
    /// </summary>
    public static void ClearSavedState()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        Debug.Log("[GameStateManager] Đã xóa dữ liệu trạng thái");
    }

    public static void SavePreMiniGameState(string gameName)
    {
        // Tận dụng lại logic lưu trữ đã có vì logic Restore dùng chung key SAVE_KEY
        SavePreExamState(gameName);
        Debug.Log($"[GameStateManager] Đã lưu trạng thái trước khi chơi MiniGame: {gameName}");
    }
}