using UnityEngine;

public class DialogueAction : InteractableAction
{
    [Header("Dialogue")]
    public string npcName = "NPC";
    [TextArea] public string message = "Chào em, hôm nay chúng ta bắt đầu buổi học nhé";

    GameUIManager UI => GameUIManager.Ins;

    public override string GetPromptText()
    {
        // Nếu không overridePrompt thì dùng tên NPC
        return string.IsNullOrEmpty(overridePrompt) ? npcName : overridePrompt;
    }

    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI) return;
        UI.OpenDialogue(npcName, message);
    }
}
