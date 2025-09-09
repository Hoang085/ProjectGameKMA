using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TeacherAction : InteractableAction
{
    [Header("Config")]
    public SemesterConfig semesterConfig;

    [Tooltip("Tên môn mà NPC này phụ trách (ví dụ: \"Tư Tưởng HCM\"). Có thể để không dấu.")]
    public string subjectName = "Tu Tuong HCM";

    [Tooltip("Để trống để tự dùng subjectName làm tiêu đề.")]
    public string titleText = "";

    [Header("UI Texts")]
    [TextArea]
    public string confirmText =
        "Đúng ca môn này. Bấm 'Điểm danh & Học' để bắt đầu, hoặc 'Đóng' để huỷ.";
    [TextArea]
    public string wrongTimeText =
        "Không phải giờ môn này, quay lại đúng ca nhé.";
    [TextArea]
    public string greetText =
        "Chào em, vào lớp điểm danh nào!";
    [TextArea]
    public string learningText =
        "Đang học...";

    [Header("Flow")]
    [Min(0.1f)] public float classSeconds = 3f;

    [Header("Events")]
    public UnityEvent onClassStarted;
    public UnityEvent onClassFinished;

    enum State { Idle, AwaitConfirm, InClass }
    State _state = State.Idle;
    Coroutine _classRoutine;
    InteractableNPC _callerCache;

    GameUIManager UI => GameUIManager.Ins;
    GameClock Clock => GameClock.I;

    string Title => string.IsNullOrEmpty(titleText) ? subjectName : titleText;

    // --------- InteractableAction ----------
    public override string GetPromptText()
    {
        if (!string.IsNullOrEmpty(overridePrompt)) return overridePrompt;
        return IsRightNowThisSubject() ? Title : "Chưa đến giờ môn này";
    }

    public override void OnPlayerExit()
    {
        if (_state == State.AwaitConfirm)
        {
            _state = State.Idle;
            UI?.CloseDialogue();
            UI?.UnbindTeacher(this);             // <<< THÊM
        }
    }

    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI) return;
        if (_state == State.InClass) return;

        if (!IsRightNowThisSubject())
        {
            UI.OpenDialogue(Title, wrongTimeText);
            return;
        }

        _state = State.AwaitConfirm;
        _callerCache = caller;

        UI.BindTeacher(this);                    // <<< THÊM: đăng ký giáo viên đang mở hộp
        UI.OpenDialogue(Title, confirmText);
    }
    // ---------------------------------------

    /// <summary>Gắn vào nút UI "Điểm danh & Học".</summary>
    public void UI_StartClass()
    {
        if (!IsRightNowThisSubject())
        {
            UI?.OpenDialogue(Title, wrongTimeText);
            return;
        }
        StartClass(_callerCache);
    }

    /// <summary>Gắn vào nút UI "Đóng".</summary>
    public void UI_Close()
    {
        UI?.CloseDialogue();
        UI?.UnbindTeacher(this);                 // <<< THÊM
        _state = State.Idle;
    }

    // ================= Core =================

    bool IsRightNowThisSubject()
    {
        if (!Clock || !semesterConfig || string.IsNullOrWhiteSpace(subjectName))
            return false;

        var today = Clock.Weekday;
        var slot1Based = Clock.GetSlotIndex1Based();

        return ScheduleResolver.IsSessionMatch(
            semesterConfig, subjectName, today, slot1Based);
    }

    void StartClass(InteractableNPC caller)
    {
        MonoBehaviour runner = UI != null ? (MonoBehaviour)UI : this;
        if (!runner || !runner.isActiveAndEnabled)
        {
            Debug.LogWarning("[TeacherAction] No active runner to start coroutine.");
            return;
        }

        if (_classRoutine != null) runner.StopCoroutine(_classRoutine);
        _classRoutine = runner.StartCoroutine(ClassRoutine());
    }

    IEnumerator ClassRoutine()
    {
        _state = State.InClass;

        onClassStarted?.Invoke();

        UI?.OpenDialogue(Title, greetText);
        yield return new WaitForSeconds(1.2f);

        UI?.OpenDialogue(Title, learningText);
        yield return new WaitForSeconds(classSeconds);

        UI?.CloseDialogue();

        onClassFinished?.Invoke();

        if (Clock) Clock.NextSlot();

        _state = State.Idle;

        // refresh prompt
        if (UI)
        {
            UI.HideInteractPrompt();
            UI.ShowInteractPrompt(GetPromptText(), KeyCode.F);
        }
    }
}
