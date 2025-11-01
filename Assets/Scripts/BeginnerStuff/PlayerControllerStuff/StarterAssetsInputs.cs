using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Netcode;

[RequireComponent(typeof(PlayerInput))]
public class StarterAssetsInputs : NetworkBehaviour
{
    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputValue value)
    {
        if (!IsOwner) return;
        move = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        if (!IsOwner) return;
        look = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (!IsOwner) return;
        jump = value.isPressed;
    }
#endif

    private void FixedUpdate()
    {
        // Optional: reset jump after FixedUpdate so it doesn't stick
        jump = false;
    }
}
