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
            sb.AppendLine(
                $"#{i + 1} | {a.subjectName} ({a.subjectKey}) | {a.examTitle} | " +
                $"{a.score10:0.0}/10, {a.score4:0.0}/4, {a.letter} | " +
                $"{a.correct}/{a.total} câu đúng | {a.takenAtIso}"
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

    public static void ClearAll()   // cẩn thận khi gọi
    {
        // Xoá file lịch sử
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }

        // Không xoá PlayerPrefs ở đây để an toàn
        // (tuỳ bạn có thể thêm hàm ClearCache nếu muốn)
    }
}
