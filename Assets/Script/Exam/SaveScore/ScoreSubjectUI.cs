using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreSubjectUI : MonoBehaviour
{
    [Header("Roots")]
    [Tooltip("Kéo thả node BlockScore vào đây")]
    public Transform blockScoreRoot;

    // row parents: TextScoreSubject1/2/3
    Transform[] _row = new Transform[3];

    // cached labels
    TextMeshProUGUI[] _subj = new TextMeshProUGUI[3];
    TextMeshProUGUI[] _he10 = new TextMeshProUGUI[3];
    TextMeshProUGUI[] _he4 = new TextMeshProUGUI[3];
    TextMeshProUGUI[] _stat = new TextMeshProUGUI[3];

    int _currentSemester = 1;

    public bool treatZeroAsOne = true; // Nếu dữ liệu cũ còn semesterIndex = 0 thì bật true để xem như Kì 1
    public bool verboseLog = true;  // In log để debug

    void Awake()
    {
        if (!blockScoreRoot)
        {
            blockScoreRoot = FindDeep(transform, "BlockScore");
        }

        // Lấy 3 dòng: TextScoreSubject1/2/3
        for (int i = 0; i < 3; i++)
        {
            string rowName = $"TextScoreSubject{i + 1}";
            _row[i] = FindDeep(blockScoreRoot ? blockScoreRoot : transform, rowName);
            if (_row[i] == null)
                Debug.LogWarning($"[ScoreBoardFromJson] Không thấy row '{rowName}'");

            // Bên trong mỗi row, tìm các label.
            // Chấp nhận 2 kiểu tên: có số (TextSubject1) hoặc không số (TextSubject).
            _subj[i] = FindTMPInRow(i, "TextSubject");
            _he10[i] = FindTMPInRow(i, "TextScore10");
            _he4[i] = FindTMPInRow(i, "TextScore4");
            _stat[i] = FindTMPInRow(i, "TextStatus");
        }

        // Bắt nút BtnSemes1..BtnSemes10 (nằm ngoài BlockScore)
        for (int s = 1; s <= 10; s++)
        {
            var btn = FindButtonDeep(transform, $"BtnSemes{s}");
            if (btn != null)
            {
                int cap = s;
                btn.onClick.AddListener(() => SetSemester(cap));
            }
        }
    }

    void OnEnable() => Refresh();

    public void SetSemester_1() => SetSemester(1);
    public void SetSemester_2() => SetSemester(2);
    public void SetSemester_3() => SetSemester(3);
    // … nếu thích có thêm SetSemester_4() …

    public void SetSemester(int semesterIndex1Based)
    {
        _currentSemester = Mathf.Max(1, semesterIndex1Based);
        Refresh();
    }

    void Refresh()
    {
        var db = ExamResultStorageFile.Load();
        if (verboseLog) Debug.Log($"[ScoreBoard] Loaded entries = {db?.entries?.Count ?? 0}");

        if (db == null || db.entries.Count == 0) { FillEmpty(); return; }

        // map 0 -> 1 nếu bật tuỳ chọn
        int want = _currentSemester;
        var list = db.entries.FindAll(e =>
        {
            int sem = e.semesterIndex;
            if (treatZeroAsOne && sem == 0) sem = 1;
            return sem == want;
        });

        if (verboseLog) Debug.Log($"[ScoreBoard] Filter semester={want} -> {list.Count} entries");

        if (list.Count == 0) { FillEmpty(); return; }

        list.Sort((b, a) => a.takenAtUnix.CompareTo(b.takenAtUnix)); // desc
        Dictionary<string, ExamAttempt> latest = new();
        foreach (var at in list)
            if (!latest.ContainsKey(at.subjectKey)) latest[at.subjectKey] = at;

        var subjects = new List<ExamAttempt>(latest.Values);
        subjects.Sort((a, b) => string.Compare(a.subjectName, b.subjectName, System.StringComparison.CurrentCulture));

        for (int row = 0; row < 3; row++)
        {
            if (row < subjects.Count) FillRow(row, subjects[row]);
            else ClearRow(row);
        }

        if (verboseLog) Debug.Log($"[ScoreBoard] Painted {Mathf.Min(3, subjects.Count)} row(s).");
    }

    void FillRow(int row, ExamAttempt at)
    {
        if (_subj[row]) _subj[row].text = at.subjectName;
        if (_he10[row]) _he10[row].text = at.score10.ToString("0.0");
        if (_he4[row]) _he4[row].text = at.score4.ToString("0.0");
        if (_stat[row]) _stat[row].text = GetStatusText(at);
    }

    void ClearRow(int row)
    {
        if (_subj[row]) _subj[row].text = "—";
        if (_he10[row]) _he10[row].text = "—";
        if (_he4[row]) _he4[row].text = "—";
        if (_stat[row]) _stat[row].text = "—";
    }

    void FillEmpty() { for (int i = 0; i < 3; i++) ClearRow(i); }

    string GetStatusText(ExamAttempt at)
    {
        if (!string.IsNullOrEmpty(at.letter))
            return at.letter.Trim().ToUpperInvariant() == "F" ? "Trượt" : "Đạt";
        return (at.score10 >= 4.0f) ? "Đạt" : "Trượt";
    }

    // ---- helpers ----
    static Transform FindDeep(Transform root, string name)
    {
        if (!root) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r) return r;
        }
        return null;
    }

    static Button FindButtonDeep(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t ? t.GetComponent<Button>() : null;
    }

    TextMeshProUGUI FindTMPInRow(int rowIndex, string baseName)
    {
        // ưu tiên có số: TextSubject1/TextScore10/TextScore4/TextStatus
        var p = _row[rowIndex] ? _row[rowIndex] : (blockScoreRoot ? blockScoreRoot : transform);
        var withNum = FindDeep(p, $"{baseName}{rowIndex + 1}");
        if (withNum) return withNum.GetComponent<TextMeshProUGUI>();

        var noNum = FindDeep(p, baseName);
        return noNum ? noNum.GetComponent<TextMeshProUGUI>() : null;
    }
}
