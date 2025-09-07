using System.Collections;
using UnityEngine;

public class TeacherAction : InteractableAction
{
    [Header("Data")]
    public SemesterConfig semesterConfig;
    public string subjectName = "Toan";

    [Header("Texts")]
    public string titleText = "Toán Cao Cấp";
    [TextArea] public string confirmText = "Đúng ca môn này. Bấm 'Điểm danh & Học' để bắt đầu, hoặc 'Đóng' để huỷ.";
    [TextArea] public string wrongTimeText = "Không phải giờ môn này, quay lại đúng ca nhé.";
    [TextArea] public string greetText = "Chào em, vào lớp điểm danh nào!";
    [TextArea] public string learningText = "Đang học...";

    [Header("Flow")]
    public float classSeconds = 3f;

    enum State { Idle, AwaitConfirm, InClass }
    State _state = State.Idle;

    Coroutine _classRoutine;
    InteractableNPC _callerCache;

    GameUIManager UI => GameUIManager.Ins;
    GameClock Clock => GameClock.I;

    public override string GetPromptText()
    {
        if (!string.IsNullOrEmpty(overridePrompt)) return overridePrompt;
        // Khi đang chờ xác nhận vẫn hiện tên môn, KHÔNG hướng dẫn bấm F lần 2
        return IsRightNowThisSubject() ? subjectName : "Chưa đến giờ môn này";
    }

    public override void OnPlayerExit()
    {
        // Rời vùng tương tác thì đóng hộp nếu đang chờ
        if (_state == State.AwaitConfirm)
        {
            _state = State.Idle;
            if (UI) UI.CloseDialogue();
        }
    }

    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI) return;

        // Nếu đang học thì bỏ qua
        if (_state == State.InClass) return;

        // Nếu sai ca -> chỉ báo, không cho xác nhận
        if (!IsRightNowThisSubject())
        {
            UI.OpenDialogue(titleText, wrongTimeText);
            return;
        }

        // ĐÚNG CA: chỉ mở hộp xác nhận, KHÔNG start lớp ở đây
        _state = State.AwaitConfirm;
        _callerCache = caller;
        UI.OpenDialogue(titleText, confirmText);

        // Lưu ý: các nút trong UI sẽ gọi UI_StartClass() / UI_Close()
        // -> Không nghe phím F ở đây để tránh vô tình start.
    }

    // ==== Các hàm dành cho nút UI ====
    public void UI_StartClass()
    {
        // phòng trường hợp slot đã đổi giữa lúc mở hộp
        if (!IsRightNowThisSubject()) { if (UI) UI.OpenDialogue(titleText, wrongTimeText); return; }
        StartClass(_callerCache); // _callerCache có cũng được, không có cũng không sao
    }


    public void UI_Close()
    {
        if (UI) UI.CloseDialogue();
        _state = State.Idle;
    }

    // =================================

    void StartClass(InteractableNPC caller)
    {
        // Dùng runner luôn active để chạy coroutine (UI thường luôn bật)
        MonoBehaviour runner = GameUIManager.Ins != null ? (MonoBehaviour)GameUIManager.Ins : this;

        if (!runner.isActiveAndEnabled)
        {
            Debug.LogWarning("[TeacherAction] No active runner to start coroutine.");
            return;
        }

        if (_classRoutine != null)
            runner.StopCoroutine(_classRoutine);

        _classRoutine = runner.StartCoroutine(ClassRoutine());
    }


    bool IsRightNowThisSubject()
    {
        if (!Clock || !semesterConfig || string.IsNullOrEmpty(subjectName)) return false;
        var today = Clock.Weekday;
        var slot1Based = Clock.GetSlotIndex1Based();
        return ScheduleResolver.IsSessionMatch(semesterConfig, subjectName, today, slot1Based);
    }

    IEnumerator ClassRoutine()
    {
        _state = State.InClass;

        if (UI) UI.OpenDialogue(titleText, greetText);
        yield return new WaitForSeconds(1.2f);

        if (UI) UI.OpenDialogue(titleText, learningText);
        yield return new WaitForSeconds(Mathf.Max(0.1f, classSeconds));

        if (UI) UI.CloseDialogue();
        if (Clock) Clock.NextSlot();

        _state = State.Idle;

        // Làm mới prompt
        if (GameUIManager.Ins)
        {
            GameUIManager.Ins.HideInteractPrompt();
            GameUIManager.Ins.ShowInteractPrompt(GetPromptText(), KeyCode.F);
        }
    }
}
