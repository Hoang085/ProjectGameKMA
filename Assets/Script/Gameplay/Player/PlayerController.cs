using UnityEngine;

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


    // Set up phim di chuyen, nhay, chay
    private void HandleInput()
    {
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");
        _runHeld = Input.GetMouseButton(1);
        if (Input.GetKeyDown(KeyCode.Space)) _jumpPressed = true;
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
            _rb.velocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);
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
            Vector3 targetVel = new Vector3(moveDir.x * baseSpeed, _rb.velocity.y, moveDir.z * baseSpeed);
            _rb.velocity = SmoothVelocity(_rb.velocity, targetVel, acceleration);
        }
        else
        {
            // Khong co input, LERP van toc XZ ve 0 de dung muot
            Vector3 targetVel = new Vector3(0f, _rb.velocity.y, 0f);
            _rb.velocity = SmoothVelocity(_rb.velocity, targetVel, acceleration);
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