using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ExamAttempt
{
    public int semesterIndex; 
    public string subjectKey;
    public string subjectName;
    public string examTitle;

    public float score10;
    public float score4;
    public string letter;

    public int correct;
    public int total;
    public int durationSeconds;

    public string takenAtIso;   // ISO 8601 UTC
    public long takenAtUnix;    // dự phòng sort nhanh
    
    // **MỚI: Hỗ trợ thi lại - đơn giản hơn**
    public bool isRetake = false;  // true nếu đây là lần thi lại
}

[Serializable]
public class ExamResultsDB
{
    public int schemaVersion = 1;
    public List<ExamAttempt> entries = new List<ExamAttempt>();
}
