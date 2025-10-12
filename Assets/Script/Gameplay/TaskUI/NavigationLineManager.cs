using DG.Tweening;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class NavigationLineManager : MonoBehaviour
{
    [Header("Line Settings")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = Color.yellow;
    [SerializeField] private float lineWidth = 0.1f;

    [Header("Shape")]
    [SerializeField] private bool straightLine = true;           // true=thẳng, false=cong
    [SerializeField, Range(4, 64)] private int curveSegments = 16;
    [SerializeField] private float curveLift = 0.35f;            // độ nhô phần giữa khi cong

    [Header("Ground Snap")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float raycastHeight = 6f;
    [SerializeField] private float groundOffset = 0.2f;          // nhô khỏi mặt đất
    [SerializeField] private float verticalNudge = -0.02f;       // tinh chỉnh cao/thấp tổng thể

    [Header("Start/End Offsets (tránh chọc chân)")]
    [SerializeField] private float playerForwardOffset = 0.6f;   // đẩy ra trước player
    [SerializeField] private float targetBackwardOffset = 0.6f;  // kéo trước mặt NPC

    [Header("Animation (optional)")]
    [SerializeField] private bool animateLine = true;            // nhấp nháy nhẹ
    [SerializeField] private float animationSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;

    [Header("Auto Clear (time)")]
    [SerializeField] private float autoClearTime = 30f;

    [Header("Proximity Auto Hide")]
    [SerializeField] private float hideWhenCloserThan = 1.8f;    // mét (tính theo XZ)
    [SerializeField] private bool fadeOutOnHide = true;
    [SerializeField] private float fadeDuration = 0.25f;

    // Runtime
    private Transform playerTransform;
    private Transform targetTransform;
    private Coroutine animationCoroutine;
    private Coroutine autoClearCoroutine;
    private bool _hasHiddenByProximity = false;

    // Singleton
    private static NavigationLineManager _instance;
    public static NavigationLineManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<NavigationLineManager>();
                if (_instance == null)
                {
                    var go = new GameObject("NavigationLineManager");
                    _instance = go.AddComponent<NavigationLineManager>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLineRenderer();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeLineRenderer()
    {
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        // --- FIX MATERIAL COLOR ISSUE: luôn tạo runtime instance ---
        Material runtimeMat;
        if (lineMaterial == null)
        {
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
#else
            var shader = Shader.Find("Unlit/Color");
#endif
            runtimeMat = new Material(shader);
        }
        else
        {
            runtimeMat = new Material(lineMaterial); // clone từ material được set trong Inspector
        }
        runtimeMat.color = lineColor;       // đặt màu mong muốn
        lineMaterial = runtimeMat;          // lưu lại để tham chiếu
        lineRenderer.material = lineMaterial;

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;

        // luôn nằm “sau” nhân vật
        lineRenderer.sortingLayerName = "Default";
        lineRenderer.sortingOrder = -10;

        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 6;
    }

    // ===== Public API =====
    public void CreateNavigationLine(Transform target, string targetName = "")
    {
        if (target == null) return;

        if (playerTransform == null)
            playerTransform = FindPlayer();
        if (playerTransform == null) return;

        targetTransform = target;
        _hasHiddenByProximity = false;

        StopAllAnimations();
        RedrawLine();

        if (animateLine)
            animationCoroutine = StartCoroutine(AnimateLineCoroutine());

        if (autoClearTime > 0)
            autoClearCoroutine = StartCoroutine(AutoClearCoroutine());

        lineRenderer.enabled = true;
    }

    public void ClearNavigationLine()
    {
        StopAllAnimations();

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 0;
        }

        targetTransform = null;
    }

    // ===== Update =====
    private void Update()
    {
        if (lineRenderer == null || !lineRenderer.enabled || playerTransform == null || targetTransform == null)
            return;

        // Ẩn khi đủ gần NPC (tính khoảng cách XZ)
        if (!_hasHiddenByProximity && DistanceXZ(playerTransform.position, targetTransform.position) <= hideWhenCloserThan)
        {
            FadeOutAndClear();
            return;
        }

        RedrawLine();
    }

    // ===== Core drawing =====
    private void RedrawLine()
    {
        if (playerTransform == null || targetTransform == null) return;

        // Hướng phẳng từ player -> target
        Vector3 dir = targetTransform.position - playerTransform.position;
        dir.y = 0f;
        Vector3 n = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

        // 1) Offset để không dính chân
        Vector3 start = playerTransform.position + n * playerForwardOffset;
        Vector3 end = targetTransform.position - n * targetBackwardOffset;

        // 2) Snap xuống mặt đất sau offset
        if (snapToGround)
        {
            start = SnapToGround(start) + Vector3.up * groundOffset;
            end = SnapToGround(end) + Vector3.up * groundOffset;
        }

        // 3) Làm phẳng theo cùng cao độ (không hạ thêm)
        float lineY = Mathf.Min(start.y, end.y);
        start.y = lineY + verticalNudge;
        end.y = lineY + verticalNudge;

        // 4) Vẽ
        if (straightLine)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
        else
        {
            DrawCurved(start, end, lineY + verticalNudge);
        }
    }

    private void DrawCurved(Vector3 start, Vector3 end, float flatY)
    {
        // Control point nhô nhẹ ở giữa (Bezier bậc 2)
        Vector3 mid = (start + end) * 0.5f;
        Vector3 dir = (end - start); dir.y = 0f;
        Vector3 left = new Vector3(-dir.z, 0f, dir.x).normalized;

        Vector3 control = mid + Vector3.up * curveLift + left * (curveLift * 0.25f);
        control.y = flatY + curveLift;

        int seg = Mathf.Max(4, curveSegments);
        lineRenderer.positionCount = seg + 1;
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            Vector3 p = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
            p.y = flatY + Mathf.Sin(t * Mathf.PI) * curveLift; // phẳng hai đầu, nhô giữa
            lineRenderer.SetPosition(i, p);
        }
    }

    // ===== Proximity fade =====
    private void FadeOutAndClear()
    {
        if (!lineRenderer || !lineRenderer.enabled) return;

        StopAllAnimations(); // dừng pulse trước

        if (fadeOutOnHide)
        {
            // tween alpha của start/endColor (không đụng shared material)
            Color c0 = lineRenderer.startColor;
            Color c1 = lineRenderer.endColor;

            DOTween.Kill(lineRenderer);
            DOTween.To(() => 1f, a =>
            {
                var sc = new Color(c0.r, c0.g, c0.b, a);
                var ec = new Color(c1.r, c1.g, c1.b, a);
                lineRenderer.startColor = sc;
                lineRenderer.endColor = ec;
            }, 0f, fadeDuration)
            .OnComplete(() => ClearNavigationLine());
        }
        else
        {
            ClearNavigationLine();
        }

        _hasHiddenByProximity = true;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ===== Utils =====
    private Vector3 SnapToGround(Vector3 src)
    {
        Vector3 origin = src + Vector3.up * raycastHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundLayers))
            return hit.point;
        return src;
    }

    private Transform FindPlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go) return go.transform;
        go = GameObject.Find("Player");
        if (go) return go.transform;

        var any = FindFirstObjectByType<MonoBehaviour>();
        if (any != null && (any.GetType().Name.Contains("Player") || any.GetType().Name.Contains("Controller")))
            return any.transform;

        return null;
    }

    private IEnumerator AnimateLineCoroutine()
    {
        float time = 0f;
        var baseColor = lineColor;

        while (lineRenderer != null && lineRenderer.enabled)
        {
            time += Time.deltaTime * animationSpeed;
            float pulse = Mathf.Sin(time) * pulseIntensity;

            Color animated = new Color(
                Mathf.Clamp01(baseColor.r + pulse),
                Mathf.Clamp01(baseColor.g + pulse),
                Mathf.Clamp01(baseColor.b + pulse),
                baseColor.a
            );

            lineRenderer.startColor = animated;
            lineRenderer.endColor = animated;
            yield return null;
        }
    }

    private IEnumerator AutoClearCoroutine()
    {
        yield return new WaitForSeconds(autoClearTime);
        ClearNavigationLine();
    }

    private void StopAllAnimations()
    {
        if (animationCoroutine != null) { StopCoroutine(animationCoroutine); animationCoroutine = null; }
        if (autoClearCoroutine != null) { StopCoroutine(autoClearCoroutine); autoClearCoroutine = null; }
    }

    private void OnDestroy() => StopAllAnimations();
}
