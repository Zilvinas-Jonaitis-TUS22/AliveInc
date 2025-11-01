using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Pedestal Setup")]
    public Transform[] pedestals;

    private readonly Dictionary<ulong, int> clientToPedestal = new();
    private readonly HashSet<int> occupiedSlots = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    public int GetPedestalIndexFor(ulong clientId)
    {
        if (clientToPedestal.TryGetValue(clientId, out int existingIndex))
            return existingIndex;

        int newIndex = FindNextFreePedestal();
        if (newIndex == -1)
        {
            Debug.LogWarning("No free pedestals available!");
            return -1;
        }

        clientToPedestal[clientId] = newIndex;
        occupiedSlots.Add(newIndex);
        return newIndex;
    }

    private int FindNextFreePedestal()
    {
        for (int i = 0; i < pedestals.Length; i++)
        {
            if (!occupiedSlots.Contains(i))
                return i;
        }
        return -1;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (clientToPedestal.TryGetValue(clientId, out int pedestalIndex))
        {
            occupiedSlots.Remove(pedestalIndex);
            clientToPedestal.Remove(clientId);
            Debug.Log($"Freed pedestal {pedestalIndex} (Client {clientId} left)");
        }
    }
}
