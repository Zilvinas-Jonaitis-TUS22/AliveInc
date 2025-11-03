using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Collections;

public class LobbyManagerUI : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public Button backButton;
    public Button startGameButton;
    public TMP_Text statusLabel;
    public TMP_InputField addressInput;

    private NetworkManager netManager;
    private UnityTransport transport;
    private string lastStatus = "";
    private bool isConnecting = false;

    void Awake()
    {
        netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("NetworkManager not found in scene!");
            return;
        }

        transport = netManager.GetComponent<UnityTransport>();
        if (transport == null)
            Debug.LogError("UnityTransport not found on NetworkManager!");
    }

    void Start()
    {
        hostButton.onClick.AddListener(() => StartCoroutine(SafeStartHost()));
        joinButton.onClick.AddListener(() => StartCoroutine(SafeStartClient()));
        backButton.onClick.AddListener(ResetUI);
        startGameButton.onClick.AddListener(StartGame);

        startGameButton.gameObject.SetActive(false);
        UpdateStatus();
    }

    IEnumerator SafeStartHost()
    {
        if (isConnecting) yield break;
        isConnecting = true;

        yield return EnsureShutdownComplete();

        hostButton.interactable = false;
        joinButton.interactable = false;

        transport.SetConnectionData("0.0.0.0", 7777);

        if (!netManager.StartHost())
        {
            Debug.LogError("Failed to start host.");
            isConnecting = false;
            ToggleLobbyButtons(true);
            yield break;
        }

        startGameButton.gameObject.SetActive(true);
        UpdateStatus();
        isConnecting = false;
    }

    IEnumerator SafeStartClient()
    {
        if (isConnecting) yield break;
        isConnecting = true;

        string ip = (addressInput != null) ? addressInput.text.Trim() : "127.0.0.1";
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        if (!IsValidIP(ip))
        {
            Debug.LogError($"Invalid IP '{ip}'");
            isConnecting = false;
            yield break;
        }

        yield return EnsureShutdownComplete();

        joinButton.interactable = false;
        hostButton.interactable = false;

        transport.SetConnectionData(ip, 7777);

        if (!netManager.StartClient())
        {
            Debug.LogError("Failed to start client.");
            isConnecting = false;
            ToggleLobbyButtons(true);
            yield break;
        }

        UpdateStatus();
        isConnecting = false;
    }

    IEnumerator EnsureShutdownComplete()
    {
        if (netManager.IsListening)
        {
            netManager.Shutdown();
            // wait one frame to let transport free unmanaged memory
            yield return null;
        }
    }

    void ResetUI()
    {
        if (netManager.IsListening)
            netManager.Shutdown();

        ToggleLobbyButtons(true);
        startGameButton.gameObject.SetActive(false);
        lastStatus = "";
        UpdateStatus();
        isConnecting = false;
    }

    void UpdateStatus()
    {
        if (statusLabel == null) return;

        string newStatus;
        if (!netManager.IsClient && !netManager.IsServer)
            newStatus = "Idle - Choose Host or Join";
        else
        {
            string mode = netManager.IsHost ? "Host" : netManager.IsServer ? "Server" : "Client";
            newStatus = $"Running as: {mode}";
        }

        if (newStatus != lastStatus)
        {
            statusLabel.text = newStatus;
            lastStatus = newStatus;
        }
    }

    void ToggleLobbyButtons(bool state)
    {
        hostButton.interactable = state;
        joinButton.interactable = state;
    }

    void StartGame()
    {
        if (!netManager.IsHost) return;
        netManager.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }

    bool IsValidIP(string ip) => IPAddress.TryParse(ip, out _);
}
