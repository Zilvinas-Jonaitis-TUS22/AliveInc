using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class LobbyManagerUI : NetworkBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public Button refreshButton;
    public Button backButton;
    public Button startGameButton;
    public TMP_Text statusLabel;
    public TMP_Text lobbySummaryLabel; // new inspector-assigned label
    public Transform lobbyListParent;
    public GameObject lobbyListItemPrefab;
    public GameObject lobbyListPanel;

    private NetworkManager netManager;
    private string joinCode;
    private const int MaxPlayers = 4;

    private Lobby currentLobby;

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
        hostButton.onClick.AddListener(() => _ = HostLobbyAsync());
        joinButton.onClick.AddListener(OnJoinMenuPressed);
        backButton.onClick.AddListener(OnBackPressed);
        startGameButton.onClick.AddListener(OnStartGamePressed);
        if (refreshButton != null)
            refreshButton.onClick.AddListener(() => _ = QueryLobbiesAsync());

        startGameButton.gameObject.SetActive(false);
        if (lobbyListPanel != null)
            lobbyListPanel.SetActive(false);

        UpdateStatus();
    }

    // ------------------------------------------------------
    //  HOST LOBBY
    // ------------------------------------------------------
    async Task HostLobbyAsync()
    {
        if (netManager.IsServer || netManager.IsClient) return;

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "joinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                "Lobby_" + UnityEngine.Random.Range(1000, 9999),
                MaxPlayers,
                options
            );

            Debug.Log($"Lobby created: {currentLobby.Name} | Code: {joinCode}");

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            var utp = netManager.GetComponent<UnityTransport>();
            utp.SetRelayServerData(relayServerData);
            netManager.StartHost();

            startGameButton.gameObject.SetActive(true);
            ToggleLobbyButtons(false);
            UpdateStatus();

            _ = HeartbeatLoopAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to host lobby: {e}");
        }
    }

    async Task HeartbeatLoopAsync()
    {
        while (currentLobby != null)
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            await Task.Delay(15000);
        }
    }

    // ------------------------------------------------------
    //  LOBBY LIST MENU
    // ------------------------------------------------------
    void OnJoinMenuPressed()
    {
        if (lobbyListPanel == null) return;

        lobbyListPanel.SetActive(true);
        ToggleLobbyButtons(false);
        if (refreshButton != null) refreshButton.interactable = true;
        startGameButton.gameObject.SetActive(false);

        _ = QueryLobbiesAsync();
        UpdateStatus();
    }

    void OnBackPressed()
    {
        if (lobbyListPanel == null) return;

        lobbyListPanel.SetActive(false);
        ToggleLobbyButtons(true);
        UpdateStatus();
    }

    // ------------------------------------------------------
    //  QUERY / JOIN LOBBIES
    // ------------------------------------------------------
    async Task QueryLobbiesAsync()
    {
        try
        {
            var response = await LobbyService.Instance.QueryLobbiesAsync();

            foreach (Transform child in lobbyListParent)
                Destroy(child.gameObject);

            int totalPlayers = 0;

            if (response.Results == null || response.Results.Count == 0)
            {
                statusLabel.text = "No available lobbies.";
                if (lobbySummaryLabel != null)
                    lobbySummaryLabel.text = "No lobbies available.";
                return;
            }

            foreach (var lobby in response.Results)
            {
                int playerCount = lobby.Players.Count;
                totalPlayers += playerCount;

                string code = lobby.Data.ContainsKey("joinCode") ? lobby.Data["joinCode"].Value : null;

                GameObject itemObj = Instantiate(lobbyListItemPrefab, lobbyListParent);
                itemObj.transform.SetParent(lobbyListParent, false);

                LobbyListItem item = itemObj.GetComponent<LobbyListItem>();
                if (item == null)
                {
                    Debug.LogError("LobbyListItem component missing on prefab!");
                    continue;
                }

                if (item.lobbyNameText != null)
                    item.lobbyNameText.text = lobby.Name;

                if (item.playerCountText != null)
                    item.playerCountText.text = $"{playerCount}/{MaxPlayers}";

                if (item.joinButton != null)
                {
                    if (playerCount >= MaxPlayers)
                    {
                        item.joinButton.interactable = false;
                        item.playerCountText.text += " [FULL]";
                    }
                    else
                    {
                        item.joinButton.interactable = true;
                        // pass both join code and lobby ID now
                        item.joinButton.onClick.AddListener(() => _ = JoinRelayAsync(code, lobby.Id));
                    }
                }
            }

            statusLabel.text = $"Found {response.Results.Count} lobby(s)";
            if (lobbySummaryLabel != null)
                lobbySummaryLabel.text = $"{response.Results.Count} lobbies | {totalPlayers} total players";
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to query lobbies: {e}");
            if (lobbySummaryLabel != null)
                lobbySummaryLabel.text = "Failed to load lobbies.";
        }
    }

    async Task JoinRelayAsync(string codeToJoin, string lobbyId)
    {
        if (netManager.IsServer || netManager.IsClient) return;

        try
        {
            if (string.IsNullOrEmpty(codeToJoin))
            {
                Debug.LogError("Join code not provided!");
                return;
            }

            // Join the lobby first (so player count updates)
            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            Debug.Log($"Joined Lobby: {currentLobby.Name} ({currentLobby.Id})");

            // Then join Relay
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(codeToJoin);
            var relayServerData = AllocationUtils.ToRelayServerData(joinAlloc, "dtls");

            var utp = netManager.GetComponent<UnityTransport>();
            utp.SetRelayServerData(relayServerData);

            netManager.StartClient();

            ToggleLobbyButtons(false);
            UpdateStatus();

            Debug.Log($"Joining Relay with code: {codeToJoin}");

            await WaitForClientConnection();
            NotifyLobbyJoinRpc(AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join Relay: {e}");
        }
    }

    async Task WaitForClientConnection()
    {
        var timeout = Time.time + 10f;
        while (!netManager.IsConnectedClient && Time.time < timeout)
            await Task.Yield();

        if (!netManager.IsConnectedClient)
            Debug.LogWarning("Client connection timed out before RPC send.");
    }

    // ------------------------------------------------------
    //  UI HELPERS
    // ------------------------------------------------------
    void ResetUI()
    {
        if (netManager.IsHost || netManager.IsClient)
            netManager.Shutdown();

        startGameButton.gameObject.SetActive(false);
        ToggleLobbyButtons(true);
        UpdateStatus();

        if (currentLobby != null)
        {
            _ = LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            currentLobby = null;
        }
    }

    void UpdateStatus()
    {
        if (statusLabel == null) return;

        if (!netManager.IsClient && !netManager.IsServer)
        {
            statusLabel.text = "Idle - Host or Join Game";
        }
        else
        {
            string mode = netManager.IsHost ? "Host (Relay)" : "Client (Relay)";
            string lobbyNameDisplay = currentLobby != null ? currentLobby.Name : "Unknown Lobby";
            statusLabel.text = $"Running as: {mode}\nLobby: {lobbyNameDisplay}";
        }
    }

    void ToggleLobbyButtons(bool state)
    {
        hostButton.interactable = state;
        joinButton.interactable = state;
    }

    // ------------------------------------------------------
    //  GAME START / RPCs
    // ------------------------------------------------------
    void OnStartGamePressed()
    {
        if (!IsHost) return;
        StartGameRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void StartGameRpc()
    {
        Debug.Log("Game starting for all players...");
        NetworkManager.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyLobbyJoinRpc(string playerId)
    {
        Debug.Log($"Player {playerId} joined the lobby!");
    }
}
