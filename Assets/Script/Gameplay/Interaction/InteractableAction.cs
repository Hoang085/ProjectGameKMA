using UnityEngine;

public abstract class InteractableAction : MonoBehaviour
{
    // Goi khi nhan phim
    public abstract void DoInteract(InteractableNPC caller);

    // Gọi khi player vao/ra trigger
    public virtual void OnPlayerEnter() { }
    public virtual void OnPlayerExit() { }
}
