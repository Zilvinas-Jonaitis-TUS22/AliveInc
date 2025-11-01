using Unity.Netcode;
using UnityEngine;

public class AutoHost : MonoBehaviour
{
    [Tooltip("Only the first instance should auto-host. Others will manually join.")]
    public bool autoStartHost = false;

    void Start()
    {
        if (autoStartHost && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.StartHost();
        }
    }
}
