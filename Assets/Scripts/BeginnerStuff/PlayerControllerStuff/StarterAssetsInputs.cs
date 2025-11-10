using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))]
public class StarterAssetsInputs : NetworkBehaviour
{
    [Header("Testing / Networking")]
    [Tooltip("If false, networking checks are ignored and this acts like a local input controller.")]
    public bool useNetworking = true;

    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool shoot;
    public bool reload; // Keep this name for compatibility

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value)
    {
        if (useNetworking && !IsOwner) return;
        move = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        if (useNetworking && !IsOwner) return;
        look = value.Get<Vector2>();
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

        // Only trigger reload on the frame the button is pressed
        if (value.isPressed)
            reload = true;
    }
#endif

    private void FixedUpdate()
    {
        // Reset jump for one-shot jumps
        jump = false;

        // Reset reload after FixedUpdate so it only triggers once per press
        reload = false;

        // DO NOT reset shoot here for automatic fire
    }
}
