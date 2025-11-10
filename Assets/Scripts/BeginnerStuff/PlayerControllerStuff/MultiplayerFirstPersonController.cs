using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(NetworkObject))]
public class MultiplayerFirstPersonController : NetworkBehaviour
{
    [Header("Testing / Networking")]
    [Tooltip("If false, networking checks are ignored and this acts like a local input controller.")]
    public bool useNetworking = true;

    [Header("Movement Settings")]
    public float moveSpeed = 4f;
    public float jumpHeight = 1.2f;
    public float gravity = -15f;
    public float fallMultiplier = 2.5f;
    public float jumpMultiplier = 2f;
    public float terminalVelocity = -20f;
    public float acceleration = 20f;
    public float deceleration = 25f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float lookSpeed = 1f;
    public float maxLookAngle = 90f;

    [Header("Recoil Settings (Controlled Externally)")]
    [Tooltip("Vertical recoil offset applied from weapon script.")]
    public float recoilOffsetX = 0f;
    [Tooltip("Horizontal recoil offset applied from weapon script.")]
    public float recoilOffsetY = 0f;
    [Tooltip("How smoothly recoil is blended into camera movement.")]
    public float recoilBlendSpeed = 10f;

    private CharacterController controller;
    private StarterAssetsInputs input;
    private float verticalVelocity;
    private bool grounded;
    private float cameraPitch;
    private const float threshold = 0.01f;
    private Vector3 currentVelocity;

    private float recoilPitchOffset = 0f;
    private float recoilYawOffset = 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<StarterAssetsInputs>();

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
        if (useNetworking && !IsOwner) return;
        if (playerCamera != null)
            playerCamera.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (useNetworking && !IsOwner) return;

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
        // Input direction
        Vector3 inputDir = transform.right * input.move.x + transform.forward * input.move.y;
        if (inputDir.magnitude > 1f) inputDir.Normalize();
        Vector3 targetVelocity = inputDir * moveSpeed;

        // Acceleration/Deceleration
        if (inputDir.sqrMagnitude > 0.01f)
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.deltaTime);
        else
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);

        // Jumping
        if (grounded && input.jump)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Gravity
        if (!grounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            else
                verticalVelocity += gravity * jumpMultiplier * Time.deltaTime;

            if (verticalVelocity < terminalVelocity)
                verticalVelocity = terminalVelocity;
        }

        // Apply movement
        Vector3 move = currentVelocity + Vector3.up * verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (input.look.sqrMagnitude < threshold) return;

        float mouseX = input.look.x * lookSpeed * Time.deltaTime;
        float mouseY = input.look.y * lookSpeed * Time.deltaTime;

        // Recoil offsets (smoothed)
        recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, recoilOffsetX, Time.deltaTime * recoilBlendSpeed);
        recoilYawOffset = Mathf.Lerp(recoilYawOffset, recoilOffsetY, Time.deltaTime * recoilBlendSpeed);

        // Apply pitch (with recoil)
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);
        float finalPitch = cameraPitch - recoilPitchOffset;

        // Apply yaw (with recoil)
        float finalYaw = mouseX + recoilYawOffset;

        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);

        transform.Rotate(Vector3.up * finalYaw);
    }
}
