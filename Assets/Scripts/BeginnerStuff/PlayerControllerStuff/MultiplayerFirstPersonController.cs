using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(NetworkObject))]
public class MultiplayerFirstPersonController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float jumpHeight = 1.2f;
    public float gravity = -15f;
    public float fallMultiplier = 2.5f;
    public float jumpMultiplier = 2f;
    public float terminalVelocity = -20f;
    public float acceleration = 20f; // How fast the player speeds up
    public float deceleration = 25f; // How fast the player slows down

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float lookSpeed = 1f;
    public float maxLookAngle = 90f;

    private CharacterController controller;
    private StarterAssetsInputs input;
    private float verticalVelocity;
    private bool grounded;
    private float cameraPitch;
    private const float threshold = 0.01f;

    private Vector3 currentVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<StarterAssetsInputs>();

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner && playerCamera != null)
            playerCamera.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!IsOwner) return;

        GroundedCheck();
        HandleMovement();
        HandleLook();
    }

    private void GroundedCheck()
    {
        grounded = controller.isGrounded;

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
    }

    private void HandleMovement()
    {
        // Desired horizontal movement
        Vector3 inputDir = transform.right * input.move.x + transform.forward * input.move.y;
        if (inputDir.magnitude > 1f) inputDir.Normalize();
        Vector3 targetVelocity = inputDir * moveSpeed;

        // Apply acceleration or deceleration
        if (inputDir.sqrMagnitude > 0.01f)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.deltaTime);
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        // Jump
        if (grounded && input.jump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Apply gravity
        if (!grounded)
        {
            if (verticalVelocity < 0f) // falling
                verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            else // rising
                verticalVelocity += gravity * jumpMultiplier * Time.deltaTime;

            if (verticalVelocity < terminalVelocity)
                verticalVelocity = terminalVelocity;
        }

        // Move the character
        Vector3 move = currentVelocity + Vector3.up * verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (input.look.sqrMagnitude < threshold) return;

        float mouseX = input.look.x * lookSpeed * Time.deltaTime;
        float mouseY = input.look.y * lookSpeed * Time.deltaTime;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);

        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }
}
