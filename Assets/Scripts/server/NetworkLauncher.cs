using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Bayou;
using UnityEngine;

public class NetworkLauncher : MonoBehaviour
{
    [SerializeField] private string serverIP = "127.0.0.1";
    [SerializeField] private ushort port = 10000;

    private bool _subscribed;

    private void OnClientState(ClientConnectionStateArgs args)
    {
        Debug.Log("[ClientState] " + args.ConnectionState);

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("[Client] CONNECT SUCCESS");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopping)
        {
            Debug.LogError("[Client] CONNECT FAILED");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.LogError("[Client] CONNECT STOPPED");
        }
    }

    public void OnJoinClick()
    {
        Debug.Log("[NetworkLauncher] OnJoinClick called");
        Debug.Log("[Client] Target IP: " + serverIP + ", Port: " + port);

        if (InstanceFinder.NetworkManager == null)
        {
            Debug.LogError("NetworkManager is missing!");
            return;
        }

        var bayou = InstanceFinder.NetworkManager.GetComponent<Bayou>();
        if (bayou == null)
        {
            Debug.LogError("Bayou not found on NetworkManager!");
            return;
        }

        if (!_subscribed)
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientState;
            _subscribed = true;
        }

        bayou.SetClientAddress(serverIP);
        bayou.SetPort(port);

        InstanceFinder.ClientManager.StartConnection();
    }

    private void OnDestroy()
    {
        if (_subscribed && InstanceFinder.NetworkManager != null)
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientState;
    }
}