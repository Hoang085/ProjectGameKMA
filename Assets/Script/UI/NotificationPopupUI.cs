using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class NotificationPopupUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text boxText;      // kéo BoxText
    [SerializeField] private Image iconNoti;        // kéo IconNoti (optional)
    [SerializeField] private Animator animator;     // Animator của prefab

    [Header("Animator")]
    [SerializeField] private string animTrigger = "Play";

    [Header("Lifetime Settings")]
    [SerializeField] private bool useCustomLifetime = false; // Bật để dùng thời gian tùy chỉnh
    [SerializeField] private float customLifetime = 10f;    // Thời gian tồn tại tùy chỉnh

    public void Setup(string message, Sprite icon = null)
    {
        if (boxText) boxText.text = message ?? string.Empty;
        if (iconNoti && icon) iconNoti.sprite = icon;

        if (animator && !string.IsNullOrEmpty(animTrigger))
            animator.SetTrigger(animTrigger);

        // Chỉ sử dụng custom lifetime nếu được bật
        if (useCustomLifetime)
        {
            CancelInvoke(nameof(DestroySelf));
            Invoke(nameof(DestroySelf), customLifetime);
        }
        // Nếu không, chỉ dựa vào Animation Event để destroy
    }

    /// <summary>
    /// Setup với thời gian tồn tại cụ thể
    /// </summary>
    public void Setup(string message, Sprite icon, float customLifetimeOverride)
    {
        if (boxText) boxText.text = message ?? string.Empty;
        if (iconNoti && icon) iconNoti.sprite = icon;

        if (animator && !string.IsNullOrEmpty(animTrigger))
            animator.SetTrigger(animTrigger);

        // Sử dụng thời gian được chỉ định
        CancelInvoke(nameof(DestroySelf));
        Invoke(nameof(DestroySelf), customLifetimeOverride);
    }

    // GỌI từ Animation Event ở cuối clip
    public void DestroySelf()
    {
        Destroy(gameObject);
    }
}