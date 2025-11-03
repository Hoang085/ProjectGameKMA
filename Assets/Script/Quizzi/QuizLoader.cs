using UnityEngine;

public static class QuizLoader
{
    /// <summary>
    /// Đọc file Resources/Quiz/{subjectKey}.json
    /// </summary>
    public static QuizFile LoadFromResources(string subjectKey)
    {
        if (string.IsNullOrWhiteSpace(subjectKey))
        {
            Debug.LogError("[QuizLoader] subjectKey rỗng.");
            return null;
        }

        TextAsset ta = Resources.Load<TextAsset>($"Quiz/{subjectKey}");
        if (ta == null)
        {
            Debug.LogError($"[QuizLoader] Không tìm thấy file: Resources/Quiz/{subjectKey}.json");
            return null;
        }

        QuizFile file = null;
        try
        {
            file = JsonUtility.FromJson<QuizFile>(ta.text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QuizLoader] Lỗi parse JSON {subjectKey}: {e}");
        }

        if (file == null || file.questions == null || file.questions.Count == 0)
        {
            Debug.LogError($"[QuizLoader] JSON rỗng hoặc sai cấu trúc: {subjectKey}");
            return null;
        }

        return file;
    }
}
