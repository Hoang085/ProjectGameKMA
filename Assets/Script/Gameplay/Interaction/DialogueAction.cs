using UnityEngine;

public class DialogueAction : InteractableAction //dung de xu ly hanh dong tuong tac voi NPC lien quan den viec hien thi hop thoai
{
    [Header("Dialogue")]
    public string npcName;
    public string message = DataKeyText.openText;
    GameUIManager UI => GameUIManager.Ins;

    public override void DoInteract(InteractableNPC caller)
    {
        if (!UI) return;
        UI.OpenDialogue(npcName, message);
    }
}