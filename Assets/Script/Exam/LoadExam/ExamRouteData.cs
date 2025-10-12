using UnityEngine;

public static class ExamRouteData
{
    public static string subjectName;
    public static string subjectKey;
    public static int? subjectIndexOverride;

    public static void Set(string displayName)
    {
        subjectName = displayName ?? "";
        subjectKey = KeyUtil.MakeKey(displayName ?? "");
        subjectIndexOverride = null;
        PlayerPrefs.SetString("LastExam_SubjectName", subjectName);
        PlayerPrefs.SetString("LastExam_SubjectKey", subjectKey);
    }

    public static void Set(string displayName, string customKey)
    {
        subjectName = displayName ?? "";
        subjectKey = KeyUtil.MakeKey(string.IsNullOrWhiteSpace(customKey) ? displayName : customKey);
        subjectIndexOverride = null;
        PlayerPrefs.SetString("LastExam_SubjectName", subjectName);
        PlayerPrefs.SetString("LastExam_SubjectKey", subjectKey);
    }

    // >>> Thêm mới:
    public static void SetIndex(int index)
    {
        subjectIndexOverride = index;
        PlayerPrefs.SetInt("LastExam_SubjectIndex", index);
    }
}

