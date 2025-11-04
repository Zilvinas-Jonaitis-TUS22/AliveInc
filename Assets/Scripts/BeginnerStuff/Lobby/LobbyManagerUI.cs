using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System;
using System.Threading.Tasks;

public class LobbyManagerUI : NetworkBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public Button backButton;
    public Button startGameButton;
    public TMP_Text statusLabel;
    public TMP_InputField joinCodeInput;

    private NetworkManager netManager;
    private string joinCode;
    private const int MaxPlayers = 8;

    async void Awake()
    {
        netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("NetworkManager not found in scene!");
            return;
        }

        await InitializeUnityServices();
    }

    async Task InitializeUnityServices()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"Signed in as Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Unity Services failed to initialize: {e}");
        }
    }

    void Start()
    {
        hostButton.onClick.AddListener(() => _ = HostRelayAsync());
        joinButton.onClick.AddListener(() => _ = JoinRelayAsync());
        backButton.onClick.AddListener(ResetUI);
        startGameButton.onClick.AddListener(OnStartGamePressed);

        startGameButton.gameObject.SetActive(false);
        UpdateStatus();
    }

    async Task HostRelayAsync()
    {
        if (netManager.IsServer || netManager.IsClient) return;

        try
        {
            // Create a Relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Configure transport with Relay server data
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            var utp = netManager.GetComponent<UnityTransport>();
            utp.SetRelayServerData(relayServerData);

            netManager.StartHost();

            startGameButton.gameObject.SetActive(true);
            ToggleLobbyButtons(false);
            UpdateStatus();

            Debug.Log($"Host started via Relay. Join Code: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to host via Relay: {e}");
        }
    }

    async Task JoinRelayAsync()
    {
        if (netManager.IsServer || netManager.IsClient) return;

        try
        {
            string codeToJoin = joinCodeInput != null ? joinCodeInput.text.Trim() : joinCode;

            if (string.IsNullOrEmpty(codeToJoin))
            {
                Debug.LogError("Join code not provided!");
                return;
            }

            // Join existing Relay allocation
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(codeToJoin);
            var relayServerData = AllocationUtils.ToRelayServerData(joinAlloc, "dtls");

            var utp = netManager.GetComponent<UnityTransport>();
            utp.SetRelayServerData(relayServerData);

            netManager.StartClient();

            ToggleLobbyButtons(false);
            UpdateStatus();

            Debug.Log("Joining Relay as client...");

            // ✅ Wait for connection before sending any RPCs
            await WaitForClientConnection();

            // Now it’s safe to call RPCs
            NotifyLobbyJoinRpc(AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join Relay: {e}");
        }
    }

    async Task WaitForClientConnection()
    {
        var timeout = Time.time + 10f; // wait max 10 seconds
        while (!netManager.IsConnectedClient && Time.time < timeout)
        {
            await Task.Yield();
        }

        if (!netManager.IsConnectedClient)
            Debug.LogWarning("Client connection timed out before RPC send.");
    }

    void ResetUI()
    {
        if (netManager.IsHost || netManager.IsClient)
            netManager.Shutdown();

        startGameButton.gameObject.SetActive(false);
        ToggleLobbyButtons(true);
        UpdateStatus();
    }

    void UpdateStatus()
    {
        if (statusLabel == null) return;

        if (!netManager.IsClient && !netManager.IsServer)
        {
            statusLabel.text = "Idle - Choose Host or Join";
        }
        else
        {
            string mode = netManager.IsHost ? "Host (Relay)" : "Client (Relay)";
            statusLabel.text = $"Running as: {mode}\nJoin Code: {joinCode}";
        }
    }

    void ToggleLobbyButtons(bool state)
    {
        hostButton.interactable = state;
        joinButton.interactable = state;
    }

    void OnStartGamePressed()
    {
        if (!IsHost) return;
        StartGameRpc();
    }

    // --- New NGO 2.0+ style RPCs ---

    // Called by the host to start the game for all connected players
    [Rpc(SendTo.ClientsAndHost)]
    void StartGameRpc()
    {
        Debug.Log("Game starting for all players...");
        NetworkManager.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    // Called to announce new player joins to everyone
    [Rpc(SendTo.ClientsAndHost)]
    void NotifyLobbyJoinRpc(string playerId)
    {
        Debug.Log($"Player {playerId} joined the lobby!");
    }
}
