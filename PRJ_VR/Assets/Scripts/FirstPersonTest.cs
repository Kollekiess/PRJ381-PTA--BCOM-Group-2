using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour
{

    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float jumpForce = 5.0f;

    [Header("Look Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 10.0f;
    public float verticalLookLimit = 90.0f;

    [Header("Ground Check")]
    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask;

    private Rigidbody rb;
    private float verticalRotation = 0f;
    private bool isGrounded;
    private Vector2 moveInput;

    void Awake()
    {
        //Getting Rigidbody Component on Awake
        try
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Rigidbody Component not found on " + gameObject.name + " :" + e);
        }

        // Lock and hide the mouse cursor in game view
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //Assign Camera To User
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }  
    }

    void Update()
    {
        //Capture Mouse Input in Update
        HandleLook();
        HandleMovementInput();

        // Ground check using a raycast downwards
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);

        // Handle jumping
        // Jump input using Keyboard.current
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        // Handle movement in FixedUpdate
        HandleMovement();
    }

    void HandleLook()
    {
        if (playerCamera == null || Mouse.current == null) return;

        // Read mouse delta using New Input System
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // Rotate character body horizontally (Y-axis)
        transform.Rotate(Vector3.up * mouseDelta.x);

        // Pitch camera vertically (X-axis) with clamping
        verticalRotation -= mouseDelta.y;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);
        playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
    
    void HandleMovementInput()
    {
        if (Keyboard.current == null) return;

        float moveX = 0f;
        float moveZ = 0f;

        if (Keyboard.current.wKey.isPressed) moveZ += 1f;
        if (Keyboard.current.sKey.isPressed) moveZ -= 1f;
        if (Keyboard.current.aKey.isPressed) moveX -= 1f;
        if (Keyboard.current.dKey.isPressed) moveX += 1f;

        moveInput = new Vector2(moveX, moveZ).normalized;
    }

    void HandleMovement()
    {
        // Calculate move direction relative to player orientation
        Vector3 targetVelocity = (transform.right * moveInput.x + transform.forward * moveInput.y) * moveSpeed;
        // Apply X and Z movement while preserving Rigidbody's Y velocity (gravity/jumping)
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }
}