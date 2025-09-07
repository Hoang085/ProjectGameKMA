using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Stats")]
    public float moveSpeed;      // tốc độ đi bộ
    public float runMultiplier;    // giữ chuột phải để chạy nhanh
    public float jumpForce;
    public float acceleration;    // gia tốc vào/ra tốc độ (mượt chân)

    [Header("Smooth")]
    public float turnSmoothTime = 0.1f;
    private float _turnSmoothVelocity;

    [Header("Refs")]
    public Transform cam;

    private Rigidbody _rb;
    private Animator _anim;
    private Collider _col;

    // --- cache input (để FixedUpdate dùng) ---
    private Vector2 _moveInput;
    private bool _runHeld;
    private bool _jumpPressed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _anim = GetComponent<Animator>();
        _col = GetComponent<Collider>();

        if (!cam && Camera.main) cam = Camera.main.transform;

        // Chống đổ & mượt giữa các bước vật lý -> giúp camera hết rung
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
        MovePlayer(); // bao gồm xoay + di chuyển mượt
    }


    // Set up phím di chuyển cho player
    private void HandleInput()
    {
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");
        _runHeld = Input.GetMouseButton(1);
        if (Input.GetKeyDown(KeyCode.Space)) _jumpPressed = true;
    }

    // Set up animator cho tốc độ và trạng thái grounded
    private void UpdateAnimator()
    {
        float targetSpeed = _moveInput.sqrMagnitude > 0.01f ? moveSpeed * (_runHeld ? runMultiplier : 1f) : 0f;
        // Khi player di chuyển nếu runHeld là true thì tốc độ sẽ là moveSpeed * runMultiplier, ngược lại là moveSpeed * 1f. Nếu không có input thì tốc độ là 0.
        if (_anim)
        {
            _anim.SetFloat(DataKey.SPEED, targetSpeed);
            _anim.SetBool(DataKey.ISGROUND, IsGrounded());
        }
    }

    // trả về tốc độ hiện tại dựa trên input chạy
    private float RunPlayer()
    {
        return _runHeld ? moveSpeed * runMultiplier : moveSpeed;
    }

    // xử lý xung lực nhảy và reset flag
    private void JumpPlayer(bool grounded)
    {
        if (_jumpPressed && grounded)
        {
            // xoá vận tốc rơi trước khi bật để nhảy ổn định
            _rb.velocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (_anim) _anim.SetTrigger(DataKey.JUMP);
        }
        _jumpPressed = false; // luôn reset sau khi xử lý
    }

    // Di chuyển + xoay + mượt vận tốc mặt phẳng XZ
    private void MovePlayer()
    {
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        float baseSpeed = RunPlayer();

        if (inputDir.sqrMagnitude > 0.01f)
        {
            float camY = cam ? cam.eulerAngles.y : transform.eulerAngles.y;
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + camY;

            // Xoay mượt theo hướng di chuyển/quay camera
            float yAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref _turnSmoothVelocity,
                turnSmoothTime
            );
            transform.rotation = Quaternion.Euler(0f, yAngle, 0f);

            // Hướng di chuyển theo camera
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // LERP vận tốc XZ để mượt ⇢ giảm rung camera khi đổi hướng
            Vector3 targetVel = new Vector3(moveDir.x * baseSpeed, _rb.velocity.y, moveDir.z * baseSpeed);
            _rb.velocity = SmoothVelocity(_rb.velocity, targetVel, acceleration);
        }
        else
        {
            // Không có input: mượt về 0 trên mặt phẳng, giữ Y
            Vector3 targetVel = new Vector3(0f, _rb.velocity.y, 0f);
            _rb.velocity = SmoothVelocity(_rb.velocity, targetVel, acceleration);
        }
    }

    // Hàm LERP mượt vận tốc với gia tốc cố định
    private Vector3 SmoothVelocity(Vector3 current, Vector3 target, float accel)
    {
        // Dùng dạng 1 - exp(-a*dt) để độc lập framerate
        float t = 1f - Mathf.Exp(-accel * Time.fixedDeltaTime);
        return Vector3.Lerp(current, target, t);
    }

    // Grounded bằng raycast theo collider cho player nhận đúng mặt đất
    private bool IsGrounded()
    {
        if (_col == null) return false;
        Vector3 origin = _col.bounds.center;
        float rayLen = _col.bounds.extents.y + 0.1f;
        return Physics.Raycast(origin, Vector3.down, rayLen);
    }
}
