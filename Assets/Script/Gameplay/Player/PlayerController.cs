using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PlayerController : MonoBehaviour
{
    [Header("Stats")]
    public float moveSpeed;      // speed walk
    public float runMultiplier;    // he so nhan toc do khi chay
    public float jumpForce;
    public float acceleration;    // gia toc de lerp van toc

    [Header("Smooth")]
    public float turnSmoothTime = 0.1f; // thoi gian de xoay muot
    private float _turnSmoothVelocity; // tham so giup xoay muot

    [Header("Camera Zoom")]
    [Tooltip("Reference đến Cinemachine FreeLook camera")]
    public CinemachineFreeLook freeLookCamera;
    [Tooltip("Tốc độ zoom camera")]
    public float zoomSpeed = 2f;
    [Tooltip("Khoảng cách camera tối thiểu (gần nhất)")]
    public float minCameraDistance = 2f;
    [Tooltip("Khoảng cách camera tối đa (xa nhất)")]
    public float maxCameraDistance = 10f;
    [Tooltip("Khoảng cách camera mặc định")]
    public float defaultCameraDistance = 5f;

    [Header("Refs")]
    public Transform cam;

    private Rigidbody _rb;
    private Animator _anim;
    private Collider _col;
    private Transform _trans;

    // --- cache input cho FixUpdate ---
    private Vector2 _moveInput;
    private bool _runHeld;
    private bool _jumpPressed;
    
    // Cache cho camera zoom
    private float _currentCameraDistance;
    private float[] _originalOrbits;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _anim = GetComponent<Animator>();
        _col = GetComponent<Collider>();
        _trans = GetComponent<Transform>();

        if (!cam && Camera.main) cam = Camera.main.transform;

        // Chong do & muot giua cac buoc vat ly -> giup camera het rung
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Tự động tìm FreeLook camera nếu chưa assign
        if (!freeLookCamera)
        {
#if UNITY_2023_1_OR_NEWER
            freeLookCamera = FindFirstObjectByType<CinemachineFreeLook>();
#else
            freeLookCamera = FindObjectOfType<CinemachineFreeLook>();
#endif
        }

        // Lưu orbit radius gốc của camera
        if (freeLookCamera != null)
        {
            _originalOrbits = new float[3];
            _originalOrbits[0] = freeLookCamera.m_Orbits[0].m_Radius;
            _originalOrbits[1] = freeLookCamera.m_Orbits[1].m_Radius;
            _originalOrbits[2] = freeLookCamera.m_Orbits[2].m_Radius;
            _currentCameraDistance = defaultCameraDistance;
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        bool grounded = IsGrounded();
        JumpPlayer(grounded);
        MovePlayer();
    }

    private void HandleInput()
    {
        // THAY ĐỔI: Sử dụng IsAnyUIOpen thay vì chỉ IsDialogueOpen
        if (GameUIManager.Ins && GameUIManager.Ins.IsAnyUIOpen)
        {
            _moveInput = Vector2.zero;
            _runHeld = false;
            _jumpPressed = false;
            return;
        }

        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");
        _runHeld = Input.GetMouseButton(1);
        if (Input.GetKeyDown(KeyCode.Space)) _jumpPressed = true;

        // Xử lý zoom camera bằng chuột giữa
        HandleCameraZoom();
    }

    // Xử lý zoom camera khi lăn chuột giữa
    private void HandleCameraZoom()
    {
        if (freeLookCamera == null) return;

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            // Lăn lên (scrollInput > 0) -> zoom in (gần hơn)
            // Lăn xuống (scrollInput < 0) -> zoom out (xa hơn)
            _currentCameraDistance -= scrollInput * zoomSpeed;
            _currentCameraDistance = Mathf.Clamp(_currentCameraDistance, minCameraDistance, maxCameraDistance);

            // Tính tỷ lệ zoom dựa trên khoảng cách mặc định
            float zoomRatio = _currentCameraDistance / defaultCameraDistance;

            // Áp dụng zoom cho cả 3 orbit (top, middle, bottom)
            freeLookCamera.m_Orbits[0].m_Radius = _originalOrbits[0] * zoomRatio;
            freeLookCamera.m_Orbits[1].m_Radius = _originalOrbits[1] * zoomRatio;
            freeLookCamera.m_Orbits[2].m_Radius = _originalOrbits[2] * zoomRatio;
        }
    }

    // Set up animator cho speed và state Isgrounded
    private void UpdateAnimator()
    {
        float targetSpeed = _moveInput.sqrMagnitude > 0.01f ? moveSpeed * (_runHeld ? runMultiplier : 1f) : 0f;
        // Khi player di chuyen, neu _runHeld = true -> speed = moveSpeed * runMultiplier, nguoc lai = moveSpeed
        if (_anim)
        {
            _anim.SetFloat(DataKey.SPEED, targetSpeed);
            _anim.SetBool(DataKey.ISGROUND, IsGrounded());
        }
    }

    // Tra ve toc do chay hoac di bo
    private float RunPlayer()
    {
        return _runHeld ? moveSpeed * runMultiplier : moveSpeed;
    }

    // Xu ly force nhay
    private void JumpPlayer(bool grounded)
    {
        if (_jumpPressed && grounded)
        {
            // Xoa van toc Y hien tai de nhay chinh xac hon
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (_anim) _anim.SetTrigger(DataKey.JUMP);
        }
        _jumpPressed = false;
    }

    // Di chuyen + xoay + muot van toc mat phang XZ
    private void MovePlayer()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        float baseSpeed = RunPlayer();

        if (inputDir.sqrMagnitude > 0.01f)
        {
            float camY = cam ? cam.eulerAngles.y : transform.eulerAngles.y;
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + camY;

            // Xoay muot theo camera
            float yAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref _turnSmoothVelocity,
                turnSmoothTime
            );
            transform.rotation = Quaternion.Euler(0f, yAngle, 0f);

            // Huong di chuyen theo huong da xoay
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // LERP van toc XZ de muot va giu van toc Y de chong rung
            Vector3 targetVel = new Vector3(moveDir.x * baseSpeed, _rb.linearVelocity.y, moveDir.z * baseSpeed);
            _rb.linearVelocity = SmoothVelocity(_rb.linearVelocity, targetVel, acceleration);
        }
        else
        {
            // Khong co input, LERP van toc XZ ve 0 de dung muot
            Vector3 targetVel = new Vector3(0f, _rb.linearVelocity.y, 0f);
            _rb.linearVelocity = SmoothVelocity(_rb.linearVelocity, targetVel, acceleration);
        }
    }

    // Ham lerp van toc giua current va target voi gia toc accel
    private Vector3 SmoothVelocity(Vector3 current, Vector3 target, float accel)
    {
        // Dung dang 1 - exp(-a*dt) de doc lap framerate
        float t = 1f - Mathf.Exp(-accel * Time.fixedDeltaTime);
        return Vector3.Lerp(current, target, t);
    }

    // Ground bang raycast theo collider cho player nhan dung mat dat
    private bool IsGrounded()
    {
        if (_col == null) return false;
        Vector3 origin = _col.bounds.center;
        float rayLen = _col.bounds.extents.y + 0.1f;
        return Physics.Raycast(origin, Vector3.down, rayLen);
    }
}