using System;
using System.Collections.Generic;

[Serializable]
public class QuizFile
{
    public string subjectKey;
    public List<QuizQuestion> questions = new(); // 30–45 câu
}

[Serializable]
public class QuizQuestion
{
    public string id;
    public string question;
    public string[] answers = new string[4]; // A,B,C,D
    public int correctIndex;                  // 0..3
}
