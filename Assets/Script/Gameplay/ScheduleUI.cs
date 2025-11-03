using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Reflection;
using HHH.Common;

/// <summary>
/// JSON deserialization classes for subject names
/// </summary>
[System.Serializable]
public class SubjectNameItem
{
    public string key;
    public string display;
}

[System.Serializable]
public class SubjectNameWrapper
{
    public SubjectNameItem[] items;
}

public class ScheduleUI : BasePopUp
{
    [Header("Text Table Attendance")]
    [SerializeField] private TextMeshProUGUI _textSubjectAttendance1; //mon1
    [SerializeField] private TextMeshProUGUI _textSubjectAttendance2; //mon2
    [SerializeField] private TextMeshProUGUI _textSubjectAttendance3; //mon3
    [SerializeField] private TextMeshProUGUI _textAttended1; //(7/10)
    [SerializeField] private TextMeshProUGUI _textAttended2; //(7/10)
    [SerializeField] private TextMeshProUGUI _textAttended3; //(7/10)
    [SerializeField] private TextMeshProUGUI _textAbsent1;   //(3/10)
    [SerializeField] private TextMeshProUGUI _textAbsent2;   //(3/10)
    [SerializeField] private TextMeshProUGUI _textAbsent3;   //(3/10)

    [Header("Text Table Schedule Exam")]
    [SerializeField] private TextMeshProUGUI _textSubjectExam1; //mon1
    [SerializeField] private TextMeshProUGUI _textSubjectExam2; //mon2
    [SerializeField] private TextMeshProUGUI _textSubjectExam3; //mon3
    [SerializeField] private TextMeshProUGUI _textDateExam1;    //ngay thi
    [SerializeField] private TextMeshProUGUI _textDateExam2;    //ngay thi
    [SerializeField] private TextMeshProUGUI _textDateExam3;    //ngay thi

    [Header("Semester Config để lấy tất cả môn học")]
    [SerializeField] private SemesterConfig _semesterConfig;
    [Tooltip("Nếu bỏ trống, sẽ tự động tìm TeacherAction đầu tiên có SemesterConfig")]
    
    // Cached references
    private GameClock Clock => GameClock.Ins;
    private AttendanceManager AttendanceManager => AttendanceManager.Instance;

    // Subject name mapping cache
    private Dictionary<string, string> _subjectDisplayNames;

    public override void OnInitScreen()
    {
        LoadSubjectDisplayNames();
    }

    public override void OnShowScreen()
    {
        base.OnShowScreen();
        RefreshAllScheduleData();
    }

    void OnEnable()
    {
        LoadSubjectDisplayNames();
        RefreshAllScheduleData();
    }

    /// <summary>
    /// **CẢI THIỆN: Load subject display names and refresh all schedule data - bao gồm cả môn không học**
    /// </summary>
    public void RefreshAllScheduleData()
    {
        // Load subject display names if not already loaded
        if (_subjectDisplayNames == null)
        {
            LoadSubjectDisplayNames();
        }

        // **MỚI: Lấy SemesterConfig**
        var semesterConfig = GetSemesterConfig();
        if (semesterConfig == null)
        {
            Debug.LogWarning("[ScheduleUI] Không tìm thấy SemesterConfig để lấy danh sách tất cả môn học");
            // Fallback về phương thức cũ chỉ lấy môn đang học
            RefreshAllScheduleDataLegacy();
            return;
        }

        // **MỚI: Lấy tất cả môn từ SemesterConfig**
        var allSubjects = new List<SubjectDataInfo>();
        int currentTerm = Clock != null ? Clock.Term : 1;

        if (semesterConfig.Subjects != null)
        {
            foreach (var semSubject in semesterConfig.Subjects)
            {
                if (semSubject == null || string.IsNullOrWhiteSpace(semSubject.Name)) continue;

                // Tìm TeacherAction tương ứng (nếu có)
                var teacherAction = FindTeacherActionForSubject(semSubject.Name);
                SubjectEntry subjectEntry = null;
                if (teacherAction != null)
                {
                    subjectEntry = FindSubjectEntryInTeacher(teacherAction, semSubject.Name);
                }

                var subjectData = new SubjectDataInfo
                {
                    semesterSubject = semSubject,
                    subjectEntry = subjectEntry,
                    teacher = teacherAction,
                    displayName = GetSubjectDisplayName(semSubject.Name),
                    attended = GetAttendedFromPlayerPrefs(semSubject.Name, subjectEntry, currentTerm),
                    absences = GetAbsencesFromPlayerPrefs(semSubject.Name, currentTerm),
                    maxSessions = GetMaxSessionsForSubject(semSubject, subjectEntry),
                    examInfo = GetExamInfoForSubject(semSubject.Name, teacherAction, subjectEntry, currentTerm)
                };

                allSubjects.Add(subjectData);
            }
        }

        // Fill the UI with data (max 3 subjects)
        FillAttendanceTable(allSubjects);
        FillExamTable(allSubjects);

        Debug.Log($"[ScheduleUI] Refreshed schedule data for {allSubjects.Count} subjects (bao gồm cả môn không học)");
    }

    /// <summary>
    /// **MỚI: Lấy SemesterConfig từ inspector hoặc tự động tìm**
    /// </summary>
    private SemesterConfig GetSemesterConfig()
    {
        // Ưu tiên SemesterConfig từ inspector
        if (_semesterConfig != null)
        {
            return _semesterConfig;
        }

        // Tự động tìm từ TeacherAction đầu tiên
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        foreach (var teacher in teachers)
        {
            if (teacher.semesterConfig != null)
            {
                _semesterConfig = teacher.semesterConfig; // Cache lại
                return teacher.semesterConfig;
            }
        }

        return null;
    }

    /// <summary>
    /// **MỚI: Tìm TeacherAction tương ứng với tên môn**
    /// </summary>
    private TeacherAction FindTeacherActionForSubject(string subjectName)
    {
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        foreach (var teacher in teachers)
        {
            if (teacher.subjects == null) continue;

            foreach (var subject in teacher.subjects)
            {
                if (ScheduleResolver.NameEquals(subject.subjectName, subjectName))
                {
                    return teacher;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// **MỚI: Tìm SubjectEntry trong TeacherAction tương ứng với tên môn**
    /// </summary>
    private SubjectEntry FindSubjectEntryInTeacher(TeacherAction teacher, string subjectName)
    {
        if (teacher?.subjects == null) return null;

        foreach (var subject in teacher.subjects)
        {
            if (ScheduleResolver.NameEquals(subject.subjectName, subjectName))
            {
                return subject;
            }
        }
        return null;
    }

    /// <summary>
    /// **MỚI: Lấy maxSessions từ SubjectData hoặc SubjectEntry**
    /// </summary>
    private int GetMaxSessionsForSubject(SubjectData semSubject, SubjectEntry subjectEntry)
    {
        // Ưu tiên từ SubjectEntry nếu có
        if (subjectEntry != null && subjectEntry.maxSessions > 0)
        {
            return subjectEntry.maxSessions;
        }

        // Fallback: đếm số sessions trong SubjectData
        if (semSubject?.Sessions != null)
        {
            return semSubject.Sessions.Length;
        }

        // Default fallback
        return 10;
    }

    /// <summary>
    /// **CẢI THIỆN: Lấy thông tin thi từ PlayerPrefs cho tất cả môn**
    /// </summary>
    private ExamScheduleInfo GetExamInfoForSubject(string subjectName, TeacherAction teacher, SubjectEntry subjectEntry, int currentTerm)
    {
        // Thử lấy từ TeacherAction trước (nếu có)
        if (teacher != null && subjectEntry != null)
        {
            var teacherExamInfo = GetExamInfoFromTeacherAction(teacher, subjectEntry);
            if (teacherExamInfo != null)
            {
                return teacherExamInfo;
            }
        }

        // **MỚI: Lấy trực tiếp từ PlayerPrefs**
        return GetExamInfoFromPlayerPrefs(subjectName, currentTerm);
    }

    /// <summary>
    /// **MỚI: Lấy thông tin thi trực tiếp từ PlayerPrefs**
    /// </summary>
    private ExamScheduleInfo GetExamInfoFromPlayerPrefs(string subjectName, int currentTerm)
    {
        try
        {
            // Sử dụng cùng pattern key như TeacherAction
            string normalizedSubjectName = NormalizeKey(subjectName);
            string keyPrefix = $"T{currentTerm}_EXAM_{normalizedSubjectName}";

            // Kiểm tra xem có lịch thi không
            if (!PlayerPrefs.HasKey(keyPrefix + "_day") || !PlayerPrefs.HasKey(keyPrefix + "_slot1Based"))
            {
                return null; // Không có lịch thi
            }

            int term = PlayerPrefs.GetInt(keyPrefix + "_term", currentTerm);
            int week = PlayerPrefs.GetInt(keyPrefix + "_week", 0);
            Weekday day = (Weekday)PlayerPrefs.GetInt(keyPrefix + "_day");
            int slot = PlayerPrefs.GetInt(keyPrefix + "_slot1Based");
            bool missed = PlayerPrefs.GetInt(keyPrefix + "_missed", 0) == 1;

            return new ExamScheduleInfo
            {
                subjectName = subjectName,
                examDay = day,
                examSlot = slot,
                examTerm = term,
                examWeek = week,
                missed = missed
            };
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ScheduleUI] Could not get exam info from PlayerPrefs for {subjectName}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// **CẢI THIỆN: Lấy attended sessions hỗ trợ cả môn có và không có TeacherAction**
    /// </summary>
    private int GetAttendedFromPlayerPrefs(string subjectName, SubjectEntry subjectEntry, int currentTerm)
    {
        // Nếu có SubjectEntry, sử dụng logic cũ
        if (subjectEntry != null)
        {
            return GetAttendedFromPlayerPrefs(subjectEntry, currentTerm);
        }

        // **MỚI: Cho môn không có TeacherAction, thử tìm trong PlayerPrefs**
        string normalizedSubjectName = NormalizeKey(subjectName);
        
        // Thử các pattern key khác nhau
        string[] possibleKeys = {
            $"T{currentTerm}_SUBJ_{normalizedSubjectName}_session",
            $"SUBJ_{normalizedSubjectName}_session"
        };

        foreach (string key in possibleKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                return PlayerPrefs.GetInt(key, 0);
            }
        }

        return 0; // Không tìm thấy dữ liệu attended
    }

    /// <summary>
    /// **Phương thức cũ được giữ lại cho fallback**
    /// </summary>
    private void RefreshAllScheduleDataLegacy()
    {
        // Get all teachers and their subjects (phương thức cũ)
        var teachers = FindObjectsByType<TeacherAction>(FindObjectsSortMode.None);
        var allSubjects = new List<SubjectDataInfo>();

        int currentTerm = Clock != null ? Clock.Term : 1;

        // Collect all subject data
        foreach (var teacher in teachers)
        {
            if (teacher.subjects == null) continue;

            foreach (var subject in teacher.subjects)
            {
                if (string.IsNullOrWhiteSpace(subject.subjectName)) continue;

                var subjectData = new SubjectDataInfo
                {
                    subjectEntry = subject,
                    teacher = teacher,
                    displayName = GetSubjectDisplayName(subject.subjectName),
                    attended = GetAttendedFromPlayerPrefs(subject, currentTerm),
                    absences = GetAbsencesFromPlayerPrefs(subject, currentTerm),
                    maxSessions = subject.maxSessions,
                    examInfo = GetExamInfoFromTeacherAction(teacher, subject)
                };

                allSubjects.Add(subjectData);
            }
        }

        // Fill the UI with data (max 3 subjects)
        FillAttendanceTable(allSubjects);
        FillExamTable(allSubjects);

        Debug.Log($"[ScheduleUI] Refreshed schedule data for {allSubjects.Count} subjects (legacy mode)");
    }

    /// <summary>
    /// Fill attendance table with subject data
    /// </summary>
    private void FillAttendanceTable(List<SubjectDataInfo> subjects)
    {
        // Arrays for easier iteration
        var subjectTexts = new[] { _textSubjectAttendance1, _textSubjectAttendance2, _textSubjectAttendance3 };
        var attendedTexts = new[] { _textAttended1, _textAttended2, _textAttended3 };
        var absentTexts = new[] { _textAbsent1, _textAbsent2, _textAbsent3 };

        // Fill up to 3 subjects
        for (int i = 0; i < 3; i++)
        {
            if (i < subjects.Count)
            {
                var subject = subjects[i];

                if (subjectTexts[i] != null)
                    subjectTexts[i].text = subject.displayName;

                if (attendedTexts[i] != null)
                    attendedTexts[i].text = $"{subject.attended}/{subject.maxSessions}";

                if (absentTexts[i] != null)
                    absentTexts[i].text = $"{subject.absences}/{subject.maxSessions}";
            }
            else
            {
                // Clear empty slots
                if (subjectTexts[i] != null)
                    subjectTexts[i].text = "—";

                if (attendedTexts[i] != null)
                    attendedTexts[i].text = "—";

                if (absentTexts[i] != null)
                    absentTexts[i].text = "—";
            }
        }
    }

    /// <summary>
    /// Fill exam table with subject data
    /// </summary>
    private void FillExamTable(List<SubjectDataInfo> subjects)
    {
        // Arrays for easier iteration
        var subjectTexts = new[] { _textSubjectExam1, _textSubjectExam2, _textSubjectExam3 };
        var dateTexts = new[] { _textDateExam1, _textDateExam2, _textDateExam3 };

        // Fill up to 3 subjects
        for (int i = 0; i < 3; i++)
        {
            if (i < subjects.Count)
            {
                var subject = subjects[i];

                if (subjectTexts[i] != null)
                    subjectTexts[i].text = subject.displayName;

                if (dateTexts[i] != null)
                {
                    if (subject.examInfo != null)
                    {
                        if (subject.examInfo.missed)
                        {
                            dateTexts[i].text = "Đã bỏ lỡ";
                        }
                        else
                        {
                            // Format exam time using TeacherAction data
                            string dayName = DataKeyText.VN_Weekday(subject.examInfo.examDay);
                            int startMin = DataKeyText.GetSlotStartMinute(DataKeyText.SlotFromIndex1Based(subject.examInfo.examSlot));
                            string timeStr = DataKeyText.FormatHM(startMin);
                            dateTexts[i].text = $"Thời gian thi - {dayName} - Ca {subject.examInfo.examSlot}";
                        }
                    }
                    else
                    {
                        dateTexts[i].text = "Chưa có lịch thi";
                    }
                }
            }
            else
            {
                // Clear empty slots
                if (subjectTexts[i] != null)
                    subjectTexts[i].text = "—";

                if (dateTexts[i] != null)
                    dateTexts[i].text = "—";
            }
        }
    }

    /// <summary>
    /// Load subject display names from Resources/TextSubjectDisplay/subjectname
    /// </summary>
    private void LoadSubjectDisplayNames()
    {
        _subjectDisplayNames = new Dictionary<string, string>();

        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("TextSubjectDisplay/subjectname");
            if (jsonFile != null)
            {
                var wrapper = JsonUtility.FromJson<SubjectNameWrapper>(jsonFile.text);
                if (wrapper?.items != null)
                {
                    foreach (var item in wrapper.items)
                    {
                        if (!string.IsNullOrEmpty(item.key) && !string.IsNullOrEmpty(item.display))
                        {
                            _subjectDisplayNames[item.key.ToLowerInvariant()] = item.display;
                        }
                    }
                }
                Debug.Log($"[ScheduleUI] Loaded {_subjectDisplayNames.Count} subject display names");
            }
            else
            {
                Debug.LogWarning("[ScheduleUI] Could not load subject display names from Resources/TextSubjectDisplay/subjectname");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScheduleUI] Error loading subject display names: {e.Message}");
        }
    }

    /// <summary>
    /// Get display name for subject key from Resources
    /// </summary>
    private string GetSubjectDisplayName(string subjectKey)
    {
        if (string.IsNullOrEmpty(subjectKey)) return "Không có tên môn";

        // Sử dụng KeyUtil.MakeKey() để normalize giống như hệ thống khác
        string normalizedKey = KeyUtil.MakeKey(subjectKey);

        if (_subjectDisplayNames != null && _subjectDisplayNames.ContainsKey(normalizedKey))
        {
            return _subjectDisplayNames[normalizedKey];
        }

        // Fallback to original key if not found in mapping
        return subjectKey;
    }

    /// <summary>
    /// Get attended sessions from PlayerPrefs using TeacherAction key pattern
    /// </summary>
    private int GetAttendedFromPlayerPrefs(SubjectEntry subject, int currentTerm)
    {
        // Use the same key pattern as TeacherAction
        string subjectKey = GetStableSubjectKey(subject);
        string key = $"T{currentTerm}_SUBJ_{subjectKey}_session";

        int attended = PlayerPrefs.GetInt(key, 0);

        // Try legacy keys if new key doesn't exist
        if (attended == 0)
        {
            string legacyKey1 = $"T{currentTerm}_SUBJ_{NormalizeKey(subject.subjectName)}_session";
            attended = PlayerPrefs.GetInt(legacyKey1, 0);

            if (attended == 0)
            {
                string legacyKey2 = $"SUBJ_{NormalizeKey(subject.subjectName)}_session";
                attended = PlayerPrefs.GetInt(legacyKey2, 0);
            }
        }

        return attended;
    }

    /// <summary>
    /// **CẢI THIỆN: Get absences hỗ trợ cả môn có và không có SubjectEntry**
    /// </summary>
    private int GetAbsencesFromPlayerPrefs(SubjectEntry subject, int currentTerm)
    {
        if (AttendanceManager != null)
        {
            return AttendanceManager.GetAbsences(subject.subjectName, currentTerm);
        }

        // Fallback: try to get directly from PlayerPrefs if AttendanceManager not available
        string key = $"T{currentTerm}_ABS_{NormalizeKey(subject.subjectName)}";
        return PlayerPrefs.GetInt(key, 0);
    }

    /// <summary>
    /// **MỚI: Get absences cho môn chỉ có tên (không có SubjectEntry)**
    /// </summary>
    private int GetAbsencesFromPlayerPrefs(string subjectName, int currentTerm)
    {
        if (AttendanceManager != null)
        {
            return AttendanceManager.GetAbsences(subjectName, currentTerm);
        }

        // Fallback: try to get directly from PlayerPrefs if AttendanceManager not available
        string key = $"T{currentTerm}_ABS_{NormalizeKey(subjectName)}";
        return PlayerPrefs.GetInt(key, 0);
    }

    /// <summary>
    /// Get exam information from TeacherAction using reflection
    /// </summary>
    private ExamScheduleInfo GetExamInfoFromTeacherAction(TeacherAction teacher, SubjectEntry subject)
    {
        try
        {
            // Use reflection to access the private TryLoadExamAssignment method
            var method = typeof(TeacherAction).GetMethod("TryLoadExamAssignment",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                object[] parameters = new object[] { subject, 0, 0, Weekday.Mon, 0, false };
                bool hasExam = (bool)method.Invoke(teacher, parameters);

                if (hasExam)
                {
                    int term = (int)parameters[1];
                    int week = (int)parameters[2];
                    Weekday day = (Weekday)parameters[3];
                    int slot = (int)parameters[4];
                    bool missed = (bool)parameters[5];

                    return new ExamScheduleInfo
                    {
                        subjectName = subject.subjectName,
                        examDay = day,
                        examSlot = slot,
                        examTerm = term,
                        examWeek = week,
                        missed = missed
                    };
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ScheduleUI] Could not get exam info for {subject.subjectName}: {e.Message}");
        }

        return null;
    }

    // Helper methods from TeacherAction pattern
    private static string NormalizeKey(string s) => (s ?? "").Trim().ToLowerInvariant();

    private string GetStableSubjectKey(SubjectEntry s)
    {
        var baseKey = !string.IsNullOrWhiteSpace(s.subjectKeyForNotes) ? s.subjectKeyForNotes : s.subjectName;
        return NormalizeKey(baseKey);
    }

    /// <summary>
    /// **CẢI THIỆN: Internal data structure for organizing subject information - hỗ trợ cả SubjectData từ SemesterConfig**
    /// </summary>
    private class SubjectDataInfo
    {
        // **MỚI: Hỗ trợ SubjectData từ SemesterConfig**
        public SubjectData semesterSubject;
        
        // Existing fields
        public SubjectEntry subjectEntry;
        public TeacherAction teacher;
        public string displayName;
        public int attended;
        public int absences;
        public int maxSessions;
        public ExamScheduleInfo examInfo;
    }

    /// <summary>
    /// Data structure for exam schedule information
    /// </summary>
    [System.Serializable]
    public class ExamScheduleInfo
    {
        public string subjectName;
        public Weekday examDay;
        public int examSlot;
        public int examTerm;
        public int examWeek;
        public bool missed;
    }
}