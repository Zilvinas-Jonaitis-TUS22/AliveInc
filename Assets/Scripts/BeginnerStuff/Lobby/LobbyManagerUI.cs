using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class LobbyManagerUI : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public Button backButton;
    public Button startGameButton;
    public TMP_Text statusLabel;

    private NetworkManager netManager;

    void Awake()
    {
        netManager = NetworkManager.Singleton;
        if (netManager == null)
            Debug.LogError("NetworkManager not found in scene!");
    }

    void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);
        backButton.onClick.AddListener(ResetUI);
        startGameButton.onClick.AddListener(StartGame);

        startGameButton.gameObject.SetActive(false);
        UpdateStatus();
    }

    void StartHost()
    {
        if (netManager.IsServer || netManager.IsClient) return;

        netManager.StartHost();
        startGameButton.gameObject.SetActive(true);
        ToggleLobbyButtons(false);
        UpdateStatus();
    }

    void StartClient()
    {
        if (netManager.IsServer || netManager.IsClient) return;

        netManager.StartClient();
        ToggleLobbyButtons(false);
        UpdateStatus();
    }

    void ResetUI()
    {
        if (netManager.IsHost || netManager.IsClient)
            netManager.Shutdown();

        ToggleLobbyButtons(true);
        startGameButton.gameObject.SetActive(false);
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
            string mode = netManager.IsHost ? "Host" : netManager.IsServer ? "Server" : "Client";
            statusLabel.text = $"Running as: {mode}";
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
}
