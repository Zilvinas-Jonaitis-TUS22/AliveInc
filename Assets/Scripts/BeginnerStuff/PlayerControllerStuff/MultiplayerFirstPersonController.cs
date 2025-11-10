using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(NetworkObject))]
public class MultiplayerFirstPersonController : NetworkBehaviour
{
    [Header("Testing / Networking")]
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

    [Header("Recoil (controlled by gun)")]
    private float recoilPitch;
    private float recoilYaw;

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

        // Camera should always be enabled for offline testing
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
        Vector3 inputDir = transform.right * input.move.x + transform.forward * input.move.y;
        if (inputDir.magnitude > 1f) inputDir.Normalize();
        Vector3 targetVelocity = inputDir * moveSpeed;

        if (inputDir.sqrMagnitude > 0.01f)
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.deltaTime);
        else
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);

        if (grounded && input.jump)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (!grounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            else
                verticalVelocity += gravity * jumpMultiplier * Time.deltaTime;

            if (verticalVelocity < terminalVelocity)
                verticalVelocity = terminalVelocity;
        }

        Vector3 move = currentVelocity + Vector3.up * verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    private void HandleLook()
    {
        // Mouse input
        if (input.look.sqrMagnitude > threshold)
        {
            float mouseX = input.look.x * lookSpeed * Time.deltaTime;
            float mouseY = input.look.y * lookSpeed * Time.deltaTime;

            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);
            transform.Rotate(Vector3.up * mouseX);
        }

        // Apply recoil instantly (no decay)
        cameraPitch = Mathf.Clamp(cameraPitch - recoilPitch, -maxLookAngle, maxLookAngle);
        transform.Rotate(Vector3.up * recoilYaw);

        // Apply to camera
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

        // Clear recoil each frame (so ApplyRecoilInstant only applies once)
        recoilPitch = 0f;
        recoilYaw = 0f;
    }

    // Called by gun when shooting
    public void ApplyRecoilInstant(float pitchDeg, float yawDeg)
    {
        recoilPitch += pitchDeg;
        recoilYaw += yawDeg;
    }
}
