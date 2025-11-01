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
    public LayerMask groundLayers;
    public float groundedOffset = -0.1f;
    public float groundedRadius = 0.5f;

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
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
        grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
    }

    private void HandleMovement()
    {
        Vector3 moveDir = (transform.right * input.move.x + transform.forward * input.move.y);
        if (moveDir.magnitude > 1f) moveDir.Normalize();

        if (grounded && input.jump)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(moveDir * moveSpeed * Time.deltaTime + Vector3.up * verticalVelocity * Time.deltaTime);
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
