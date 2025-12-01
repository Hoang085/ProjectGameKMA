using UnityEngine;

public class ScalePermanent : MonoBehaviour
{
    [SerializeField]
    private float fixedScale = 0.44f;

    private void Awake()
    {
        // Tắt CanvasScaler của Canvas chứa GameObject này (nếu có)
        var canvasScaler = GetComponentInParent<UnityEngine.UI.CanvasScaler>();
        if (canvasScaler != null)
        {
            canvasScaler.enabled = false; // Vô hiệu hóa CanvasScaler
        }

        // Đặt scale cố định
        transform.localScale = Vector3.one * fixedScale;
    }

    private void LateUpdate()
    {
        // Đảm bảo scale không bị thay đổi bởi hệ thống khác
        if (!Mathf.Approximately(transform.localScale.x, fixedScale))
        {
            transform.localScale = Vector3.one * fixedScale;
        }
    }

    // Phương thức để kiểm tra và đặt lại scale nếu cần
    public void ForceSetScale()
    {
        transform.localScale = Vector3.one * fixedScale;
    }
}