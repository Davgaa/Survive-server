using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    [Header("Camera")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float mouseSensitivity = 100f;

    private CharacterController _cc;
    private Camera _cam;
    private Vector3 _velocity;
    private bool _isGrounded;
    private float _xRotation;
    private Animator _animator;

    public override void OnStartClient()
    {
        base.OnStartClient();

        Debug.Log($"[PlayerController] OnStartClient | IsOwner={IsOwner}");

        _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            Debug.LogWarning("[PlayerController] Animator not found in children.");
        else
            Debug.Log($"[PlayerController] Animator found on {_animator.gameObject.name}");

        if (!IsOwner)
            return;

        _cc = GetComponent<CharacterController>();
        if (_cc == null)
        {
            Debug.LogError("[PlayerController] CharacterController missing!");
            enabled = false;
            return;
        }

        if (groundCheck == null)
        {
            Debug.LogError("[PlayerController] groundCheck missing!");
            enabled = false;
            return;
        }

        if (cameraHolder == null)
        {
            Debug.LogError("[PlayerController] cameraHolder missing!");
            enabled = false;
            return;
        }

        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("[PlayerController] Main Camera not found!");
            enabled = false;
            return;
        }

        _cam.transform.SetParent(cameraHolder);
        _cam.transform.localPosition = Vector3.zero;
        _cam.transform.localRotation = Quaternion.identity;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsOwner || _cc == null)
            return;

        HandleGround();
        HandleMovement();
        HandleJump();
        HandleGravity();
        HandleMouseLook();
        UpdateAnimator();
    }

    private void HandleGround()
    {
        _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    private void HandleMovement()
    {
        float h = 0f;
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) h = -1f;
            if (Keyboard.current.dKey.isPressed) h = 1f;
            if (Keyboard.current.wKey.isPressed) v = 1f;
            if (Keyboard.current.sKey.isPressed) v = -1f;
        }

        Vector3 move = (transform.right * h + transform.forward * v).normalized;
        _cc.Move(move * speed * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (_animator != null)
                _animator.SetTrigger("Jump");
        }
    }

    private void HandleGravity()
    {
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null || _cam == null)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        float mouseX = delta.x * mouseSensitivity * Time.deltaTime;
        float mouseY = delta.y * mouseSensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);

        cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void UpdateAnimator()
    {
        if (_animator == null)
            return;

        Vector3 horizontalVelocity = _cc.velocity;
        horizontalVelocity.y = 0f;

        float moveSpeed = horizontalVelocity.magnitude;

        _animator.SetFloat("Speed", moveSpeed, 0.1f, Time.deltaTime);
        _animator.SetBool("IsGrounded", _isGrounded);
        _animator.SetFloat("VerticalVelocity", _velocity.y);
    }
}