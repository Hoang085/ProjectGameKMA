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

    [Header("BAD ENDING – Điều kiện kích hoạt")]
    [SerializeField] private bool enableBadEnding = true;
    [Tooltip("Số môn không đạt để kích hoạt bad ending")]
    [SerializeField] private int failedSubjectsThreshold = 15;

    [Header("GRADUATION ENDING – Điều kiện kích hoạt")]
    [SerializeField] private bool enableGraduationEnding = true;
    [Tooltip("Tự động kích hoạt vào Kỳ 10, Tuần 6, Chủ Nhật, Ca 1")]
    [SerializeField] private int graduationTerm = 10;
    [SerializeField] private int graduationWeek = 6;
    [SerializeField] private Weekday graduationDay = Weekday.Sun;
    [SerializeField] private DaySlot graduationSlot = DaySlot.MorningA;

    [Header("Video Ending")]
    [SerializeField] private bool playVideo = true;
    [Tooltip("VideoProfile dùng để phát ending bạn bè")]
    [SerializeField] private VideoProfile friendshipEndingVideo;
    [Tooltip("VideoProfile dùng để phát bad ending")]
    [SerializeField] private VideoProfile badEndingVideo;
    [Tooltip("VideoProfile dùng để phát graduation ending")]
    [SerializeField] private VideoProfile graduationEndingVideo;
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
        // **MỚI: Kiểm tra graduation ending trước (ưu tiên cao nhất)**
        if (enableGraduationEnding && !_endingTriggered && !_waitingForVideo)
        {
            if (IsGraduationTime())
            {
                DebugLog($"[GameEndingManager] Đạt thời điểm tốt nghiệp! (Kỳ {graduationTerm}, Tuần {graduationWeek}, {graduationDay}, Ca {(int)graduationSlot + 1})");
                TriggerGraduationEnding();
                return;
            }
        }

        // Kiểm tra bad ending trước (ưu tiên cao hơn friendship)
        if (enableBadEnding && !_endingTriggered && !_waitingForVideo)
        {
            if (Time.time - _lastCheckTime < checkInterval) return;
            _lastCheckTime = Time.time;

            int failedCount = GetFailedSubjectsCount();
            
            if (showDebugLogs && Time.frameCount % 600 == 0)
            {
                DebugLog($"[GameEndingManager] Số môn trượt: {failedCount}/{failedSubjectsThreshold}");
            }

            if (failedCount >= failedSubjectsThreshold)
            {
                DebugLog($"[GameEndingManager] Đạt ngưỡng môn trượt! ({failedCount}/{failedSubjectsThreshold})");
                TriggerBadEnding();
                return;
            }
        }

        // Kiểm tra friendship ending
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
    /// **MỚI: Kích hoạt bad ending khi có quá nhiều môn trượt**
    /// </summary>
    public void TriggerBadEnding()
    {
        if (_endingTriggered)
        {
            return;
        }
        _endingTriggered = true;
        
        PlayerPrefs.SetInt(ENDING_TRIGGERED_KEY, 1);
        PlayerPrefs.Save();
        
        StartCoroutine(CoPlayBadEnding());
    }

    /// <summary>
    /// **MỚI: Kích hoạt graduation ending (happy ending cuối cùng)**
    /// </summary>
    public void TriggerGraduationEnding()
    {
        if (_endingTriggered)
        {
            return;
        }
        _endingTriggered = true;
        
        PlayerPrefs.SetInt(ENDING_TRIGGERED_KEY, 1);
        PlayerPrefs.Save();
        
        StartCoroutine(CoPlayGraduationEnding());
    }

    /// <summary>
    /// Logic chính: khóa player -> phát video (nếu có) -> hiển thị notification -> end game
    /// </summary>
    private IEnumerator CoPlayFriendshipEnding()
    {
        // **MỚI: Xóa tất cả notification trước khi video chạy**
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.ClearAllNotifications();
            DebugLog("[GameEndingManager] Đã xóa tất cả notification trước khi phát friendship ending video");
        }

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

                // **MỚI: Chờ thêm vài frame để video popup hoàn toàn đóng**
                yield return null;
                yield return null;
                
                // **MỚI: Đảm bảo video popup đã tắt hẳn**
                if (videoPopup.gameObject.activeSelf)
                {
                    videoPopup.gameObject.SetActive(false);
                    DebugLog("[GameEndingManager] Tắt video popup thủ công");
                }
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
        
        // **MỚI: Chờ thêm 1 frame sau khi restore timeScale**
        yield return null;
        
        // **MỚI: Hiển thị NoticationEndingGame với message 1**
        if (noticationEndingGame != null)
        {
            DebugLog("[GameEndingManager] Hiển thị NoticationEndingGame với thông điệp 1...");
            
            // **MỚI: Đảm bảo GameObject chính của notification đang active**
            if (!noticationEndingGame.gameObject.activeInHierarchy)
            {
                noticationEndingGame.gameObject.SetActive(true);
                DebugLog("[GameEndingManager] Đã kích hoạt notification GameObject");
                yield return null; // Chờ 1 frame để Unity kích hoạt object
            }
            
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

    /// <summary>
    /// **MỚI: Coroutine phát bad ending**
    /// </summary>
    private IEnumerator CoPlayBadEnding()
    {
        // **MỚI: Xóa tất cả notification trước khi video chạy**
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.ClearAllNotifications();
            DebugLog("[GameEndingManager] Đã xóa tất cả notification trước khi phát bad ending video");
        }

        Time.timeScale = 0f;

        if (playVideo && badEndingVideo != null)
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

                videoPopup.PlayProfile_Inspector(badEndingVideo);
                yield return videoPopup.WaitUntilFinished();

                DebugLog("[GameEndingManager] ✓ Bad ending video đã kết thúc!");
                _waitingForVideo = false;

                yield return null;
                yield return null;
                
                if (videoPopup.gameObject.activeSelf)
                {
                    videoPopup.gameObject.SetActive(false);
                    DebugLog("[GameEndingManager] Tắt video popup thủ công");
                }
            }
            else
            {
                DebugLog("[GameEndingManager] ✗ Không tìm thấy VideoPopupUI để phát video bad ending!", isWarning: true);
            }
        }
        else
        {
            DebugLog("[GameEndingManager] Không cấu hình video bad ending hoặc tắt playVideo, skip phần video.");
        }
        
        Time.timeScale = 1f;
        
        yield return null;
        
        // Hiển thị NoticationEndingGame với message 2 (bad ending)
        if (noticationEndingGame != null)
        {
            DebugLog("[GameEndingManager] Hiển thị NoticationEndingGame với thông điệp bad ending...");
            
            if (!noticationEndingGame.gameObject.activeInHierarchy)
            {
                noticationEndingGame.gameObject.SetActive(true);
                DebugLog("[GameEndingManager] Đã kích hoạt notification GameObject");
                yield return null;
            }
            
            noticationEndingGame.GetMes2(); // Message 2: Bad ending
            
            DebugLog("[GameEndingManager] ✓ Đã hiển thị bad ending notification - chờ người chơi tương tác");
        }
        else
        {
            DebugLog("[GameEndingManager] ✗ Không tìm thấy NoticationEndingGame!", isWarning: true);
            
            if (delayAfterVideo > 0f)
            {
                DebugLog($"[GameEndingManager] Chờ {delayAfterVideo}s trước khi kết thúc game...");
                yield return new WaitForSeconds(delayAfterVideo);
            }

            EndGame();
        }
    }

    /// <summary>
    /// **MỚI: Coroutine phát graduation ending (happy ending cuối cùng)**
    /// </summary>
    private IEnumerator CoPlayGraduationEnding()
    {
        // Xóa tất cả notification trước khi video chạy
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.ClearAllNotifications();
            DebugLog("[GameEndingManager] Đã xóa tất cả notification trước khi phát graduation ending video");
        }

        Time.timeScale = 0f;

        if (playVideo && graduationEndingVideo != null)
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

                videoPopup.PlayProfile_Inspector(graduationEndingVideo);
                yield return videoPopup.WaitUntilFinished();

                DebugLog("[GameEndingManager] ✓ Graduation ending video đã kết thúc!");
                _waitingForVideo = false;

                yield return null;
                yield return null;
                
                if (videoPopup.gameObject.activeSelf)
                {
                    videoPopup.gameObject.SetActive(false);
                    DebugLog("[GameEndingManager] Tắt video popup thủ công");
                }
            }
            else
            {
                DebugLog("[GameEndingManager] ✗ Không tìm thấy VideoPopupUI để phát video graduation ending!", isWarning: true);
            }
        }
        else
        {
            DebugLog("[GameEndingManager] Không cấu hình video graduation ending hoặc tắt playVideo, skip phần video.");
        }
        
        Time.timeScale = 1f;
        
        yield return null;
        
        // Hiển thị NoticationEndingGame với message 3 (graduation ending) + GPA
        if (noticationEndingGame != null)
        {
            DebugLog("[GameEndingManager] Hiển thị NoticationEndingGame với thông điệp graduation ending...");
            
            if (!noticationEndingGame.gameObject.activeInHierarchy)
            {
                noticationEndingGame.gameObject.SetActive(true);
                DebugLog("[GameEndingManager] Đã kích hoạt notification GameObject");
                yield return null;
            }
            
            // Tính GPA và hiển thị
            float gpa = CalculateOverallGPA();
            noticationEndingGame.GetMes3WithGPA(gpa); // Message 3: Graduation ending + GPA
            
            DebugLog("[GameEndingManager] ✓ Đã hiển thị graduation ending notification với GPA - chờ người chơi tương tác");
        }
        else
        {
            DebugLog("[GameEndingManager] ✗ Không tìm thấy NoticationEndingGame!", isWarning: true);
            
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

    /// <summary>
    /// **MỚI: Đếm số môn không đạt từ ExamResultStorageFile**
    /// Bao gồm: Điểm < 4.0, Bị cấm thi, Bỏ thi
    /// </summary>
    private int GetFailedSubjectsCount()
    {
        var db = ExamResultStorageFile.Load();
        if (db == null || db.entries == null || db.entries.Count == 0)
        {
            if (showDebugLogs)
                Debug.Log("[GameEndingManager] Không có dữ liệu thi để kiểm tra");
            return 0;
        }

        // Lấy kỳ hiện tại
        int currentTerm = GetCurrentTerm();

        // Dictionary lưu kết quả mới nhất của từng môn
        var subjectResults = new System.Collections.Generic.Dictionary<string, ExamAttempt>();

        foreach (var attempt in db.entries)
        {
            // SỬA: Thay vì chỉ xét kỳ hiện tại (==), ta xét từ đầu đến kỳ hiện tại (<=)
            if (attempt.semesterIndex > currentTerm)
                continue;

            string key = string.IsNullOrEmpty(attempt.subjectKey) ? attempt.subjectName : attempt.subjectKey;

            // Logic lấy lần thi mới nhất (retake) là đúng, giữ nguyên
            if (!subjectResults.ContainsKey(key) ||
                attempt.takenAtUnix > subjectResults[key].takenAtUnix)
            {
                subjectResults[key] = attempt;
            }
        }

        int failedCount = 0;

        foreach (var kvp in subjectResults)
        {
            var attempt = kvp.Value;
            bool isFailed = false;
            string failReason = "";

            // Logic kiểm tra trượt giữ nguyên
            if (attempt.isBanned)
            {
                isFailed = true;
                failReason = "Bị cấm thi";
            }
            else if (!string.IsNullOrEmpty(attempt.examTitle) &&
                     attempt.examTitle.Contains("Bỏ Thi"))
            {
                isFailed = true;
                failReason = "Bỏ thi";
            }
            else if (attempt.score10 < 4.0f)
            {
                isFailed = true;
                failReason = $"Điểm thấp ({attempt.score10:F1})";
            }
            else if (!string.IsNullOrEmpty(attempt.letter) &&
                     attempt.letter.Trim().Equals("F", System.StringComparison.OrdinalIgnoreCase))
            {
                isFailed = true;
                failReason = "Letter = F";
            }

            if (isFailed)
            {
                failedCount++;
                // Log debug để bạn kiểm tra xem nó đang đếm những môn nào
                if (showDebugLogs)
                {
                    DebugLog($"[Counted Failed] Kỳ {attempt.semesterIndex} - {attempt.subjectName}: {failReason}");
                }
            }
        }

        if (showDebugLogs) DebugLog($"[Total Failed] Tổng số môn trượt tích lũy: {failedCount}");

        return failedCount;
    }

    /// <summary>
    /// **MỚI: Lấy kỳ hiện tại từ GameClock**
    /// </summary>
    private int GetCurrentTerm()
    {
        if (GameClock.Ins != null)
        {
            return GameClock.Ins.Term;
        }
        return 1; // Fallback
    }

    /// <summary>
    /// **MỚI: Tính GPA tổng suốt 10 kỳ học**
    /// Chỉ lấy điểm cao nhất của mỗi môn (kể cả retake) để tính trung bình
    /// </summary>
    private float CalculateOverallGPA()
    {
        var db = ExamResultStorageFile.Load();
        if (db == null || db.entries == null || db.entries.Count == 0)
        {
            DebugLog("[GameEndingManager] Không có dữ liệu thi để tính GPA");
            return 0f;
        }

        // Dictionary lưu điểm cao nhất của từng môn
        var bestScores = new System.Collections.Generic.Dictionary<string, float>();

        foreach (var attempt in db.entries)
        {
            // Bỏ qua các môn bị cấm thi hoặc bỏ thi
            if (attempt.isBanned) continue;
            if (!string.IsNullOrEmpty(attempt.examTitle) && attempt.examTitle.Contains("Bỏ Thi"))
                continue;

            string key = string.IsNullOrEmpty(attempt.subjectKey) ? attempt.subjectName : attempt.subjectKey;

            // Lấy điểm cao nhất (cho phép retake cải thiện điểm)
            if (!bestScores.ContainsKey(key) || attempt.score4 > bestScores[key])
            {
                bestScores[key] = attempt.score4;
            }
        }

        if (bestScores.Count == 0)
        {
            DebugLog("[GameEndingManager] Không có môn nào hợp lệ để tính GPA");
            return 0f;
        }

        // Tính GPA trung bình
        float totalScore = 0f;
        foreach (var score in bestScores.Values)
        {
            totalScore += score;
        }

        float gpa = totalScore / bestScores.Count;
        gpa = Mathf.Clamp(gpa, 0f, 4f);

        DebugLog($"[GameEndingManager] GPA tổng: {gpa:F2} (từ {bestScores.Count} môn)");

        return gpa;
    }

    /// <summary>
    /// **MỚI: Kiểm tra có phải thời điểm tốt nghiệp không**
    /// </summary>
    private bool IsGraduationTime()
    {
        if (GameClock.Ins == null) return false;

        bool isRightTerm = GameClock.Ins.Term == graduationTerm;
        bool isRightWeek = GameClock.Ins.Week == graduationWeek;
        bool isRightDay = GameClock.Ins.Weekday == graduationDay;
        bool isRightSlot = GameClock.Ins.Slot == graduationSlot;

        if (showDebugLogs && Time.frameCount % 600 == 0)
        {
            DebugLog($"[GameEndingManager] Graduation check: T{GameClock.Ins.Term}=={graduationTerm}? W{GameClock.Ins.Week}=={graduationWeek}? {GameClock.Ins.Weekday}=={graduationDay}? {GameClock.Ins.Slot}=={graduationSlot}.");
        }

        return isRightTerm && isRightWeek && isRightDay && isRightSlot;
    }
}
