using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PlayerLobbyPositioner : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(SetLobbyPositionWhenReady());
        }
    }

    private IEnumerator SetLobbyPositionWhenReady()
    {
        yield return new WaitUntil(() =>
            LobbyManager.Instance != null &&
            LobbyManager.Instance.pedestals != null &&
            LobbyManager.Instance.pedestals.Length > 0);

        var manager = LobbyManager.Instance;
        var pedestalIndex = manager.GetPedestalIndexFor(OwnerClientId);

        if (pedestalIndex >= 0 && pedestalIndex < manager.pedestals.Length)
        {
            Transform pedestal = manager.pedestals[pedestalIndex];
            transform.SetPositionAndRotation(pedestal.position, pedestal.rotation);
        }
    }
}
