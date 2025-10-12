using UnityEngine;
using System;

public class ExamLoader : MonoBehaviour
{
    [Serializable]
    public struct ExamEntry
    {
        [Tooltip("Tên hiển thị trên UI / dropdown")]
        public string displayName;

        [Tooltip("Đường dẫn trong Resources, ví dụ: Exams/ToanCaoCap")]
        public string resourcePath;
    }

    [Header("Danh sách môn (chọn sẵn ở Inspector)")]
    public ExamEntry[] exams;

    [Tooltip("Lựa chọn môn để thi")] 
    public int subjectIndex;

    // giữ lại API cũ để tương thích (không bắt buộc dùng nữa)
    [HideInInspector] public string examFileName = "";

    // trạng thái hiện tại
    private int _currentIndex = -1;

    void Awake()
    {
        if (exams != null && exams.Length > 0)
            SelectByIndex(Mathf.Clamp(subjectIndex, 0, exams.Length - 1));
    }

    [ContextMenu("Chọn môn mặc định (theo defaultIndex)")]
    public void ApplyDefault()
    {
        if (exams != null && exams.Length > 0)
            SelectByIndex(Mathf.Clamp(subjectIndex, 0, exams.Length - 1));
    }

    public void SelectByIndex(int index)
    {
        if (exams == null || exams.Length == 0) { Debug.LogError("ExamLoader: Chưa cấu hình danh sách môn."); return; }
        if (index < 0 || index >= exams.Length) { Debug.LogError("ExamLoader: Index không hợp lệ."); return; }

        _currentIndex = index;
        examFileName = exams[_currentIndex].resourcePath;
    }

    public ExamData LoadExam()
    {
        // ưu tiên đường dẫn từ danh sách; fallback về examFileName nếu dev tự gán
        var path = "";
        if (_currentIndex >= 0 && _currentIndex < (exams?.Length ?? 0))
            path = exams[_currentIndex].resourcePath;
        if (string.IsNullOrEmpty(path))
            path = examFileName;
        Debug.Log($"[ExamLoader] Using path: {path} (index={_currentIndex})");

        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogError("Không tìm thấy file đề thi: " + path);
            return null;
        }
        ExamData data = JsonUtility.FromJson<ExamData>(textAsset.text);

        // nếu JSON chưa có examTitle thì gán theo displayName cho đẹp
        if (data != null && string.IsNullOrEmpty(data.examTitle) && _currentIndex >= 0)
            data.examTitle = exams[_currentIndex].displayName;

        return data;
    }

    // tiện lợi để gán từ nút UI mà không cần viết code mới
    public void SelectByKey(string resourceKey)
    {
        if (exams == null) return;
        for (int i = 0; i < exams.Length; i++)
            if (exams[i].resourcePath == resourceKey) { SelectByIndex(i); return; }
        Debug.LogWarning("ExamLoader: Không tìm thấy resourceKey = " + resourceKey);
    }
}
