using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// SubjectEntry chua thong tin ve mon hoc
[System.Serializable]
public class SubjectEntry
{
    [Tooltip("Ten mon hien thi")]
    public string subjectName; // Ten mon hoc
    [Tooltip("Key Notes: Resources/NoteItems/<key>/BuoiN.txt. Bo trong se dung subjectName.")]
    public string subjectKeyForNotes = ""; // Key de truy xuat ghi chu
    [Tooltip("Tong so buoi toi da cua mon trong hoc ky")]
    public int maxSessions; // So buoi toi da
    [HideInInspector] public int currentSessionIndex = 0; // Buoi hoc hien tai
}

// TeacherAction xu ly tuong tac voi giao vien, bao gom diem danh va hoc
public class TeacherAction : InteractableAction
{
    [Header("Config")]
    public SemesterConfig semesterConfig;

    [Header("Cac mon giang day")]
    public List<SubjectEntry> subjects = new List<SubjectEntry>();

    [Header("UI Title")]
    public string titleText;

    [Header("UI Texts")]
    public string openText = DataKeyText.openText; 
    public string confirmText = DataKeyText.text1; 
    public string wrongTimeText = DataKeyText.text2; 
    public string learningText = DataKeyText.text3; 
    [Tooltip("Da hoc xong tat ca buoi cua mon")]
    public string finishedAllSessionsText = "Em đã học xong tất cả các buổi của môn này!"; 

    [Header("Attendance/Absences")]
    [Tooltip("Hien khi nghi qua so buoi quy dinh")]
    public string exceededAbsenceText = "Em đã nghỉ quá số buổi quy định cho phép"; 

    [Header("Flow")]
    [Min(0.1f)] public float classSeconds = 3f; 

    [Header("Notes")]
    public bool addNoteWhenFinished = true; 
    [Tooltip("0 = tu danh so tiep theo. >0 = dung so buoi chi dinh")]
    public int overrideSessionIndex = 0; 

    [Header("Events")]
    public UnityEvent onClassStarted; 
    public UnityEvent onClassFinished;

    enum State { Idle, AwaitConfirm, InClass } // Trang thai cua giao vien
    State _state = State.Idle; // Trang thai hien tai
    Coroutine _classRoutine;
    InteractableNPC _callerCache; 

    GameUIManager UI => GameUIManager.Ins; 
    GameClock Clock => GameClock.Ins;

    private void Awake()
    {
        for (int i = 0; i < subjects.Count; i++)
            subjects[i].currentSessionIndex = LoadProgress(subjects[i]);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < subjects.Count; i++)
            SaveProgress(subjects[i]);
    }

    string TitleText() => string.IsNullOrWhiteSpace(titleText) ? "No Title" : titleText;

    // Tim mon hoc khop voi ca hien tai
    bool TryFindSubjectForNow(out SubjectEntry subj)
    {
        subj = null;
        if (!Clock || !semesterConfig || subjects == null || subjects.Count == 0) return false;

        var today = Clock.Weekday;
        var slot1Based = Clock.GetSlotIndex1Based();

        foreach (var s in subjects)
        {
            if (string.IsNullOrWhiteSpace(s.subjectName)) continue;
            if (ScheduleResolver.IsSessionMatch(semesterConfig, s.subjectName, today, slot1Based))
            {
                subj = s;
                return true;
            }
        }
        return false;
    }

    // Tao key luu tien do mon hoc
    private string ProgressKey(SubjectEntry s)
    {
        string key = string.IsNullOrWhiteSpace(s.subjectKeyForNotes) ? s.subjectName : s.subjectKeyForNotes;
        return $"SUBJ_{key}_session";
    }

    private int LoadProgress(SubjectEntry s) => PlayerPrefs.GetInt(ProgressKey(s), 0);

    private void SaveProgress(SubjectEntry s) => PlayerPrefs.SetInt(ProgressKey(s), s.currentSessionIndex);

    // Lay so buoi ghi chu tiep theo
    private int GetNextSessionIndexFromNotes(string subjectKey)
    {
        if (NotesService.Instance == null) return 1;
        var list = NotesService.Instance.noteRefs;
        int max = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i].subjectKey == subjectKey && list[i].sessionIndex > max)
                max = list[i].sessionIndex;
        return max + 1;
    }

    // Them ghi chu 
    private void AddNoteIfNeeded(SubjectEntry subj)
    {
        if (!addNoteWhenFinished || NotesService.Instance == null) return;

        string key = !string.IsNullOrWhiteSpace(subj.subjectKeyForNotes) ? subj.subjectKeyForNotes : subj.subjectName;

        // TÍNH SỐ BUỔI THỰC TẾ ĐÃ DIỄN RA CHO MÔN NÀY (đến hiện tại, sau khi học xong buổi vừa rồi)
        int attendedSoFar = Mathf.Max(0, subj.currentSessionIndex);     
        int term = Clock ? Clock.Term : 1;
        int absencesSoFar = 0;
        var att = AttendanceManager.Instance;
        if (att != null) absencesSoFar = Mathf.Max(0, att.GetAbsences(subj.subjectName, term));

        int realSessionIndex = attendedSoFar + absencesSoFar;   

        // Cho phép override thủ công nếu bạn set trong Inspector
        int sessionIndex = overrideSessionIndex > 0 ? overrideSessionIndex : realSessionIndex;

        // LƯU NOTE (NotesService tự chống trùng cùng (subjectKey, sessionIndex) rồi)
        NotesService.Instance.AddNoteRef(key, sessionIndex, subj.subjectName);

        // Nếu đang mở Balo thì refresh giao diện
        var b = FindFirstObjectByType<BackpackUIManager>();
        if (b && b.gameObject.activeInHierarchy) b.RefreshNoteButtons();
    }

    // Xu ly khi nguoi choi roi khoi NPC
    public override void OnPlayerExit()
    {
        if (_state == State.AwaitConfirm)
        {
            _state = State.Idle;
            UI?.CloseDialogue();
            UI?.UnbindTeacher(this);
        }
    }

    // Xu ly tuong tac voi giao vien
    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI || _state == State.InClass) return;

        _state = State.AwaitConfirm;
        _callerCache = caller;

        UI.BindTeacher(this);
        UI.OpenDialogue(TitleText(), openText); // Mo hoi thoai
    }

    // Bat dau lop hoc tu giao dien
    public void UI_StartClass()
    {
        if (_state == State.InClass) return;

        if (!TryFindSubjectForNow(out var subj))
        {
            UI?.OpenDialogue(TitleText(), wrongTimeText); // Sai ca hoc
            return;
        }

        if (subj.currentSessionIndex >= Mathf.Max(1, subj.maxSessions))
        {
            UI?.OpenDialogue(TitleText(), finishedAllSessionsText); // Da hoc het buoi
            return;
        }

        var att = AttendanceManager.Instance;
        if (att != null)
        {
            // Kiem tra nghi qua gioi han
            if (att.HasExceededAbsences(subj.subjectName, Clock ? Clock.Term : 1))
            {
                UI?.OpenDialogue(TitleText(), exceededAbsenceText);
                return;
            }

            // Kiem tra diem danh
            if (!att.TryCheckIn(subj.subjectName, out string err))
            {
                UI?.OpenDialogue(TitleText(), string.IsNullOrEmpty(err) ? wrongTimeText : err);
                return;
            }
        }

        StartClass(subj); // Bat dau lop hoc
    }

    // Dong giao dien
    public void UI_Close()
    {
        UI?.CloseDialogue();
        UI?.UnbindTeacher(this);
        _state = State.Idle;
    }

    // Chay lop hoc
    void StartClass(SubjectEntry subj)
    {
        var runner = UI != null ? (MonoBehaviour)UI : this;
        if (!runner || !runner.isActiveAndEnabled) return;

        if (_classRoutine != null) runner.StopCoroutine(_classRoutine);
        _classRoutine = runner.StartCoroutine(ClassRoutine(subj));
    }

    // Coroutine xu ly qua trinh hoc
    IEnumerator ClassRoutine(SubjectEntry subj)
    {
        _state = State.InClass;
        onClassStarted?.Invoke();

        UI?.OpenDialogue(TitleText(), confirmText); // Xac nhan diem danh
        yield return new WaitForSeconds(1.0f);

        UI?.OpenDialogue(TitleText(), learningText); // Dang hoc
        yield return new WaitForSeconds(classSeconds);

        UI?.CloseDialogue();
        onClassFinished?.Invoke(); // Ket thuc lop

        subj.currentSessionIndex = Mathf.Clamp(subj.currentSessionIndex + 1, 0, Mathf.Max(1, subj.maxSessions));
        SaveProgress(subj);

        AddNoteIfNeeded(subj); // Them ghi chu

        // ✅ Gọi ClockUI để chuyển ca + warp đồng hồ
        var clockUI = Object.FindFirstObjectByType<ClockUI>();
        if (clockUI != null)
        {
            clockUI.JumpToNextSessionNow();
        }
        else
        {
            // fallback nếu chưa có ClockUI trong scene
            if (Clock) Clock.NextSlot();
        }

        _state = State.Idle;
        UI?.HideInteractPrompt();
    }

    public void UI_TakeExam()
    {
        if (!TryFindSubjectForNow(out var subj))
        {
            UI?.OpenDialogue(TitleText(), "Không đúng ca thi hoặc môn học.");
            return;
        }

        // Kiểm tra vắng quá số buổi cho phép
        var att = AttendanceManager.Instance;
        if (att != null && att.HasExceededAbsences(subj.subjectName, Clock ? Clock.Term : 1))
        {
            UI?.OpenDialogue(TitleText(), "Em vắng quá số buổi cho phép nên bị cấm thi");
            return;
        }

        // Kiểm tra số buổi học đã tham gia
        if (subj.currentSessionIndex < subj.maxSessions)
        {
            UI?.OpenDialogue(TitleText(), "Em chưa học đủ số buổi môn này");
            return;
        }

        // Nếu qua được tất cả điều kiện → cho thi
        UI?.OpenDialogue(TitleText(), "Em đủ điều kiện để thi môn này! Chúc may mắn.");
        // TODO: ở đây bạn thêm logic mở scene thi, hoặc hiện UI thi
    }
}