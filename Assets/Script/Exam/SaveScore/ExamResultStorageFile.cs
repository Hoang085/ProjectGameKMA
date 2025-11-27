using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ExamResultStorageFile
{
    const string FileName = "ExamResults.json";
    const string LatestScorePrefix = "ExamScoreLatest_";     // float (he10)
    const string LatestScore4Prefix = "ExamScore4Latest_";   // float
    const string LatestLetterPrefix = "ExamLetterLatest_";   // string

    // Số bản ghi tối đa mỗi môn để giữ gọn file (đổi nếu cần)
    public static int MaxAttemptsPerSubject = 50;

    static string FilePath
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            return path;
        }
    }

    public static ExamResultsDB Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new ExamResultsDB();
            var json = File.ReadAllText(FilePath);
            var db = JsonUtility.FromJson<ExamResultsDB>(json);
            return db ?? new ExamResultsDB();
        }
        catch (Exception)
        {
            return new ExamResultsDB();
        }
    }

    public static void Save(ExamResultsDB db)
    {
        try
        {
            var json = JsonUtility.ToJson(db, true);
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Debug.Log("[ExamResultStorageFile] Saving to: " + FilePath);

            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);

            // Nếu file đích CHƯA tồn tại -> Move (lần đầu)
            if (!File.Exists(FilePath))
            {
                File.Move(tmp, FilePath);
            }
            else
            {
#if UNITY_2021_2_OR_NEWER
                // Đích đã tồn tại -> Replace an toàn
                File.Replace(tmp, FilePath, null);
#else
                File.Delete(FilePath);
                File.Move(tmp, FilePath);
#endif
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"[ExamResultStorageFile] Save error: {e}");
        }
    }

    public static void DebugPrintAll()
    {
        var db = Load();
        if (db == null || db.entries.Count == 0)
        {
            Debug.Log("[ExamResultStorageFile] Chưa có dữ liệu điểm nào.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[ExamResultStorageFile] ===== Lịch sử thi đã lưu =====");
        for (int i = 0; i < db.entries.Count; i++)
        {
            var a = db.entries[i];
            string retakeMarker = a.isRetake ? " [THI LẠI]" : "";
            sb.AppendLine(
                $"#{i + 1} | {a.subjectName} ({a.subjectKey}) | {a.examTitle} | " +
                $"{a.score10:0.0}/10, {a.score4:0.0}/4, {a.letter} | " +
                $"{a.correct}/{a.total} câu đúng | {a.takenAtIso}{retakeMarker}"
            );
        }
        Debug.Log(sb.ToString());
    }

    public static void AddAttempt(ExamAttempt a)
    {
        // --- ONLY CHANGE: đảm bảo semesterIndex là 1-based theo kỳ hiện tại ---
        // Nếu chỗ tạo ExamAttempt chưa set hoặc set = 0 thì mình gán = GameClock.Term (1-based).
        if (a.semesterIndex <= 0)
        {
            a.semesterIndex = GetCurrentTermOrDefault1();
        }
        // ----------------------------------------------------------------------

        var db = Load();
        db.entries.Add(a);

        // Giới hạn số bản ghi mỗi môn
        TrimPerSubject(db, a.subjectKey, MaxAttemptsPerSubject);

        Save(db);

        // Cập nhật cache "điểm gần nhất"
        CacheLatest(a);

        // ===== NOTIFICATION INTEGRATION =====
        // Trigger notification for new score
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnScoreAdded(a.subjectKey, a.semesterIndex, a.takenAtUnix);
        }
    }

    /// <summary>
    /// **MỚI: Ghi nhận môn bị cấm thi - tự động điểm 0**
    /// Gọi hàm này khi học sinh bị cấm thi (vắng quá nhiều, vi phạm...)
    /// </summary>
    public static void RecordBannedExam(string subjectKey, string subjectName, int semesterIndex = 0)
    {
        // Đảm bảo semesterIndex hợp lệ
        if (semesterIndex <= 0)
        {
            semesterIndex = GetCurrentTermOrDefault1();
        }

        // Kiểm tra xem đã có bản ghi bị cấm chưa (tránh trùng)
        var existing = GetAllForSubject(subjectKey);
        foreach (var e in existing)
        {
            if (e.isBanned && e.semesterIndex == semesterIndex)
            {
                Debug.LogWarning($"[ExamResultStorageFile] Môn {subjectName} kì {semesterIndex} đã có bản ghi cấm thi rồi!");
                return;
            }
        }

        // Tạo bản ghi điểm 0 với flag isBanned
        var bannedAttempt = new ExamAttempt
        {
            semesterIndex = semesterIndex,
            subjectKey = subjectKey,
            subjectName = subjectName,
            examTitle = "Bị Cấm Thi",
            
            // Điểm 0
            score10 = 0f,
            score4 = 0f,
            letter = "F",
            
            correct = 0,
            total = 0,
            durationSeconds = 0,
            
            // Timestamp hiện tại
            takenAtIso = DateTime.UtcNow.ToString("o"),
            takenAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            
            isRetake = false,
            isBanned = true  // ← Đánh dấu bị cấm thi
        };

        // Lưu vào database
        AddAttempt(bannedAttempt);

        Debug.Log($"[ExamResultStorageFile] ✘ Đã ghi nhận môn '{subjectName}' BỊ CẤM THI - Điểm 0.0 (Kì {semesterIndex})");
    }

    static int GetCurrentTermOrDefault1()
    {
        try
        {
            if (GameClock.Ins != null) return Mathf.Max(1, GameClock.Ins.Term); // Term đã 1-based
        }
        catch { }
        return 1;
    }

    static void TrimPerSubject(ExamResultsDB db, string subjectKey, int maxPerSubject)
    {
        if (maxPerSubject <= 0) return;

        // lấy tất cả của môn -> sort theo thời gian giảm dần -> giữ top N
        var list = new List<ExamAttempt>();
        foreach (var e in db.entries)
            if (e.subjectKey == subjectKey) list.Add(e);

        list.Sort((b, a) => a.takenAtUnix.CompareTo(b.takenAtUnix)); // desc

        if (list.Count <= maxPerSubject) return;

        var toRemove = new HashSet<ExamAttempt>();
        for (int i = maxPerSubject; i < list.Count; i++)
            toRemove.Add(list[i]);

        db.entries.RemoveAll(e => toRemove.Contains(e));
    }

    static void CacheLatest(ExamAttempt a)
    {
        var key = a.subjectKey;
        PlayerPrefs.SetFloat(LatestScorePrefix + key, a.score10);
        PlayerPrefs.SetFloat(LatestScore4Prefix + key, a.score4);
        PlayerPrefs.SetString(LatestLetterPrefix + key, a.letter);
        PlayerPrefs.Save();
    }

    // API tiện dụng:

    public static List<ExamAttempt> GetAllForSubject(string subjectKey)
    {
        var db = Load();
        var list = new List<ExamAttempt>();
        foreach (var e in db.entries)
            if (e.subjectKey == subjectKey) list.Add(e);

        list.Sort((b, a) => a.takenAtUnix.CompareTo(b.takenAtUnix)); // desc
        return list;
    }

    public static ExamAttempt GetLatestForSubject(string subjectKey)
    {
        var list = GetAllForSubject(subjectKey);
        return list.Count > 0 ? list[0] : null;
    }

    public static float GetLatestScore10Cached(string subjectKey, float defaultValue = -1f)
        => PlayerPrefs.GetFloat(LatestScorePrefix + subjectKey, defaultValue);

    /// <summary>
    /// **MỚI: Kiểm tra xem có lần thi đầu (không phải retake) trượt (< 4.0) không**
    /// </summary>
    public static bool HasFailedOriginalExam(string subjectKey)
    {
        var allAttempts = GetAllForSubject(subjectKey);
        
        // Tìm lần thi đầu (isRetake = false)
        foreach (var attempt in allAttempts)
        {
            if (!attempt.isRetake)
            {
                return attempt.score10 < 4.0f;
            }
        }
        
        return false;
    }

    /// <summary>
    /// **MỚI: Kiểm tra xem đã thi lại chưa**
    /// </summary>
    public static bool HasRetakeAttempt(string subjectKey)
    {
        var allAttempts = GetAllForSubject(subjectKey);
        foreach (var attempt in allAttempts)
        {
            if (attempt.isRetake) return true;
        }
        return false;
    }

    /// <summary>
    /// **MỚI: Lấy kết quả thi lần đầu (không phải retake)**
    /// </summary>
    public static ExamAttempt GetOriginalAttempt(string subjectKey)
    {
        var allAttempts = GetAllForSubject(subjectKey);

        // Tìm attempt không phải retake gần nhất
        foreach (var attempt in allAttempts)
        {
            if (!attempt.isRetake) return attempt;
        }

        return null;
    }

    /// <summary>
    /// **MỚI: Lấy kết quả thi lại (nếu có)**
    /// </summary>
    public static ExamAttempt GetRetakeAttempt(string subjectKey)
    {
        var allAttempts = GetAllForSubject(subjectKey);

        // Tìm attempt là retake
        foreach (var attempt in allAttempts)
        {
            if (attempt.isRetake) return attempt;
        }

        return null;
    }

    /// <summary>
    /// **MỚI: Kiểm tra xem môn này đã bị cấm thi chưa trong kì hiện tại**
    /// </summary>
    public static bool IsSubjectBanned(string subjectKey, int semesterIndex = 0)
    {
        if (semesterIndex <= 0)
        {
            semesterIndex = GetCurrentTermOrDefault1();
        }

        var attempts = GetAllForSubject(subjectKey);
        foreach (var a in attempts)
        {
            if (a.isBanned && a.semesterIndex == semesterIndex)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// **MỚI: Xóa cache PlayerPrefs của tất cả điểm đã lưu**
    /// </summary>
    public static void ClearCache()
    {
        var db = Load();
        if (db?.entries != null)
        {
            // Xóa cache của từng môn
            foreach (var entry in db.entries)
            {
                PlayerPrefs.DeleteKey(LatestScorePrefix + entry.subjectKey);
                PlayerPrefs.DeleteKey(LatestScore4Prefix + entry.subjectKey);
                PlayerPrefs.DeleteKey(LatestLetterPrefix + entry.subjectKey);
            }
        }
        PlayerPrefs.Save();
    }

    public static void ClearAll()   // cẩn thận khi gọi
    {
        // Xoá file lịch sử
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }

        // **SỬA: Xóa luôn PlayerPrefs cache để đồng bộ**
        ClearCache();
    }
}
