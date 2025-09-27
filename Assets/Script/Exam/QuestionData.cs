[System.Serializable]
public class QuestionData
{
    public string text;
    public string[] options;
    public int correctIndex;
}

[System.Serializable]
public class ExamData
{
    public string examTitle;
    public string subjectName;
    public int durationSeconds;
    public QuestionData[] questions;
}
