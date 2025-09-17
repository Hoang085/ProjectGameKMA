using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TeacherAction : InteractableAction
{
    [Header("Config")]
    public SemesterConfig semesterConfig;
    [Tooltip("Tên môn hiển thị")]
    public string subjectName;
    [Tooltip("Key để load Note ở Resources/NoteItems/<key>/BuoiN.txt.")]
    public string subjectKeyForNotes = "";

    [Header("UI Title")]
    public string titleText = "Giảng viên";

    [Header("UI Texts")]
    public string openText = DataKeyText.openText;
    public string confirmText = DataKeyText.text1; 
    public string wrongTimeText = DataKeyText.text2; 
    public string learningText = DataKeyText.text3; 

    [Header("Flow")]
    [Min(0.1f)] public float classSeconds = 3f;

    [Header("Notes")]
    public bool addNoteWhenFinished = true;
    [Tooltip("0 = tự đánh số tiếp theo. >0 = dùng số buổi chỉ định.")]
    public int overrideSessionIndex = 0;

    [Header("Events")]
    public UnityEvent onClassStarted;
    public UnityEvent onClassFinished;

    enum State { Idle, AwaitConfirm, InClass }
    State _state = State.Idle;
    Coroutine _classRoutine;
    InteractableNPC _callerCache;

    GameUIManager UI => GameUIManager.Ins;
    GameClock Clock => GameClock.I;

    // ---------------- Helpers ----------------
    string TitleText()
    {
        return string.IsNullOrWhiteSpace(titleText) ? "No Title" : titleText;
    }

    bool IsRightNowThisSubject()
    {
        if (!Clock || !semesterConfig || string.IsNullOrWhiteSpace(subjectName)) return false;
        var today = Clock.Weekday;
        var slot1Based = Clock.GetSlotIndex1Based();
        return ScheduleResolver.IsSessionMatch(semesterConfig, subjectName, today, slot1Based);
    }

    int GetNextSessionIndexFor(string subjectKey)
    {
        var list = NotesService.Instance.noteRefs;
        int max = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i].subjectKey == subjectKey && list[i].sessionIndex > max)
                max = list[i].sessionIndex;
        return max + 1;
    }
    // -----------------------------------------

    // ============== InteractableAction ==============
    public override string GetPromptText()
    {
        // Luôn mời nói chuyện (không hiện "chưa đến giờ...")
        return !string.IsNullOrEmpty(overridePrompt) ? overridePrompt : TitleText();
    }

    public override void OnPlayerExit()
    {
        if (_state == State.AwaitConfirm)
        {
            _state = State.Idle;
            UI?.CloseDialogue();
            UI?.UnbindTeacher(this);
        }
    }

    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI || _state == State.InClass) return;

        // Ấn F: luôn mở openText
        _state = State.AwaitConfirm;
        _callerCache = caller;

        UI.BindTeacher(this);
        UI.OpenDialogue(TitleText(), openText);
    }
    // ===============================================

    // Gắn vào nút "Điểm danh & Học"
    public void UI_StartClass()
    {
        if (!IsRightNowThisSubject())
        {
            UI?.OpenDialogue(TitleText(), wrongTimeText); // sai ca → text2
            return;
        }
        StartClass();
    }

    public void UI_Close()
    {
        UI?.CloseDialogue();
        UI?.UnbindTeacher(this);
        _state = State.Idle;
    }

    void StartClass()
    {
        var runner = UI != null ? (MonoBehaviour)UI : this;
        if (!runner || !runner.isActiveAndEnabled) return;

        if (_classRoutine != null) runner.StopCoroutine(_classRoutine);
        _classRoutine = runner.StartCoroutine(ClassRoutine());
    }

    IEnumerator ClassRoutine()
    {
        _state = State.InClass;
        onClassStarted?.Invoke();

        // Đúng ca: hiện text1 trước…
        UI?.OpenDialogue(TitleText(), confirmText);
        yield return new WaitForSeconds(1.0f);

        // …rồi chuyển sang text4 trong lúc học
        UI?.OpenDialogue(TitleText(), learningText);
        yield return new WaitForSeconds(classSeconds);

        UI?.CloseDialogue();
        onClassFinished?.Invoke();

        // Tạo note sau khi học xong (nếu bật)
        if (addNoteWhenFinished && NotesService.Instance != null)
        {
            string key = !string.IsNullOrWhiteSpace(subjectKeyForNotes) ? subjectKeyForNotes : subjectName;
            int sessionIndex = overrideSessionIndex > 0 ? overrideSessionIndex : GetNextSessionIndexFor(key);

            NotesService.Instance.AddNoteRef(key, sessionIndex, subjectName);

            var b = FindObjectOfType<BackpackUIManager>();
            if (b && b.gameObject.activeInHierarchy) b.RefreshNoteButtons();
        }

        if (Clock) Clock.NextSlot();

        _state = State.Idle;

        // refresh prompt
        UI?.HideInteractPrompt();
        UI?.ShowInteractPrompt(KeyCode.F);
    }
}
