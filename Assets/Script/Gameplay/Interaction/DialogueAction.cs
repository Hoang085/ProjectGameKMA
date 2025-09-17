using UnityEngine;

public class DialogueAction : InteractableAction
{
    [Header("Dialogue")]
    public string npcName;
    public string message = DataKeyText.openText;

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
