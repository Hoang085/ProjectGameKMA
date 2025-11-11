using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class InteractableZone : MonoBehaviour
{
    [Header("Nhận diện Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Phím tương tác")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Sự kiện UI / logic")]
    public UnityEvent onShowPrompt;   // Ví dụ: hiện label "Nhấn F: Vào trong"
    public UnityEvent onHidePrompt;   // Ẩn label
    public UnityEvent onInteract;     // Mở UI căng tin, play SFX, v.v.

    [Header("Tích hợp hệ thống hành động sẵn có")]
    [SerializeField] private List<InteractableAction> actions = new List<InteractableAction>();

    [Header("Chống bấm lặp")]
    [SerializeField] private float interactCooldown = 0.2f;

    private bool _playerInside;
    private float _lastInteractTime;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;   // bắt buộc là Trigger
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _playerInside = true;

        // Báo cho actions biết player đã vào vùng
        if (actions != null)
            foreach (var a in actions) if (a) a.OnPlayerEnter();

        // Hiện prompt "Nhấn F: Vào trong" (nếu bạn gán)
        onShowPrompt?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        _playerInside = false;

        if (actions != null)
            foreach (var a in actions) if (a) a.OnPlayerExit();

        // Ẩn prompt
        onHidePrompt?.Invoke();
    }

    private void OnDisable()
    {
        // Nếu object bị tắt khi đang trong vùng => chắc chắn ẩn prompt
        if (_playerInside)
        {
            _playerInside = false;
            onHidePrompt?.Invoke();
        }
    }

    private void Update()
    {
        if (!_playerInside) return;

        if (Input.GetKeyDown(interactKey) && (Time.time - _lastInteractTime) >= interactCooldown)
        {
            _lastInteractTime = Time.time;

            // Ẩn prompt khi mở UI
            onHidePrompt?.Invoke();

            // Gọi custom event: mở UI căng tin
            onInteract?.Invoke();

            // Gọi các InteractableAction của bạn (truyền null vì không phải NPC)
            if (actions != null)
            {
                foreach (var a in actions)
                {
                    if (!a) continue;
                    a.DoInteract(null);
                }
            }
        }
    }
}
