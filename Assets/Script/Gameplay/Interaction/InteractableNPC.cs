using UnityEngine;

// Lop InteractableNPC xu ly tuong tac giua nguoi choi va NPC
[RequireComponent(typeof(Collider))]
public class InteractableNPC : MonoBehaviour
{
    [Header("Interact")]
    public KeyCode interactKey = KeyCode.F;
    public bool autoListenKey = true;
    public float interactCooldown = 0.2f;

    bool _playerNearby;
    float _lastInteractTime;
    InteractableAction _action;
    Rigidbody _rb;

    GameUIManager UI => GameUIManager.Ins;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        _action = GetComponent<InteractableAction>();
        _rb = GetComponent<Rigidbody>();
    }

    // An goi y tuong tac khi tat NPC
    void OnDisable()
    {
        if (UI) UI.HideInteractPrompt();
        _playerNearby = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerNearby = true;
        _action?.OnPlayerEnter();
        if (UI) UI.ShowInteractPrompt(interactKey); // Hien thi goi y tuong tac
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerNearby = false;
        _action?.OnPlayerExit();
        if (UI) UI.HideInteractPrompt(); // An goi y tuong tac
    }

    void Update()
    {
        if (!autoListenKey || !_playerNearby) return;
        if (Input.GetKeyDown(interactKey) && Time.time - _lastInteractTime >= interactCooldown)
        {
            _lastInteractTime = Time.time;
            DoInteract(); // Thuc hien tuong tac
        }
    }

    // Kich hoat hanh dong tuong tac
    public void DoInteract()
    {
        if (_action != null)
            _action.DoInteract(this);
    }
}