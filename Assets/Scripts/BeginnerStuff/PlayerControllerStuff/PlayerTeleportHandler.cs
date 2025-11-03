using UnityEngine;
using Unity.Netcode;

public class PlayerTeleportHandler : NetworkBehaviour
{
    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // Called locally on the owner client to perform the teleport
    public void TeleportLocal(Vector3 pos, Quaternion rot)
    {
        if (!IsOwner) return;

        if (controller != null) controller.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        // reset CharacterController internal state if present
        if (controller != null)
        {
            controller.Move(Vector3.zero);
            controller.enabled = true;
        }
    }
}
