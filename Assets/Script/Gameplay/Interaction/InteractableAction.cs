using UnityEngine;

public abstract class InteractableAction : MonoBehaviour
{
    [Header("Prompt")]
    public string overridePrompt;

    public virtual string GetPromptText()
        => string.IsNullOrEmpty(overridePrompt) ? "Tương tác" : overridePrompt;

    // gọi khi nhấn phím
    public abstract void DoInteract(InteractableNPC caller);

    // gọi khi player vào/ra trigger (để action có thể reset state)
    public virtual void OnPlayerEnter() { }
    public virtual void OnPlayerExit() { }
}
