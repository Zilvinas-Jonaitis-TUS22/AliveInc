using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))]
public class StarterAssetsInputs : NetworkBehaviour
{
    [Header("Networking Settings")]
    [Tooltip("If false, networking checks are ignored and this acts like a local input controller.")]
    public bool useNetworking = true;

    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool shoot;
    public bool reload;
    public bool ads; // 🎯 NEW: Aim Down Sights input

    [Header("Camera Settings")]
    [Tooltip("Mouse sensitivity multiplier for look input.")]
    public float lookSensitivity = 1.0f;

    private bool cursorLocked = true;

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value)
    {
        if (useNetworking && !IsOwner) return;
        move = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        if (useNetworking && !IsOwner) return;

        Vector2 inputLook = value.Get<Vector2>();
        look = inputLook * lookSensitivity;
    }

    public void OnJump(InputValue value)
    {
        if (useNetworking && !IsOwner) return;
        jump = value.isPressed;
    }

    public void OnShoot(InputValue value)
    {
        if (useNetworking && !IsOwner) return;
        shoot = value.isPressed;
    }

    public void OnReload(InputValue value)
    {
        if (useNetworking && !IsOwner) return;

        // Trigger reload on button press only
        if (value.isPressed)
            reload = true;
    }

    // 🎯 NEW: Aim Down Sights input handler
    public void OnADS(InputValue value)
    {
        if (useNetworking && !IsOwner) return;
        ads = value.isPressed;
    }
#endif

    private void OnEnable()
    {
        if (!useNetworking || IsOwner)
            LockCursor(true);
    }

    private void OnDisable()
    {
        LockCursor(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!useNetworking || IsOwner)
            LockCursor(hasFocus);
    }

    private void LockCursor(bool shouldLock)
    {
        cursorLocked = shouldLock;
        Cursor.visible = !shouldLock;
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void FixedUpdate()
    {
        // Reset one-shot actions after they're used each physics step
        jump = false;
        reload = false;
        // shoot and ads stay persistent until released
    }
}
