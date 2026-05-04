using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Connection;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Тохиргоо")]
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private float _spawnRadius = 5f;
    [SerializeField] private float _heightOffset = 0.1f;

    [Header("Газар илрүүлэлт")]
    [SerializeField] private LayerMask _groundMask;

    private readonly List<NetworkConnection> _pendingConnections = new List<NetworkConnection>();
    private bool _isMapReady = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[PlayerSpawner] OnStartServer called");

        if (InstanceFinder.SceneManager != null)
        {
            InstanceFinder.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            Debug.Log("[PlayerSpawner] Subscribed to OnClientLoadedStartScenes");
        }
        else
        {
            Debug.LogError("[PlayerSpawner] SceneManager is NULL on server");
        }

        ChunkManager.OnMapReady += HandleMapReady;
        Debug.Log("[PlayerSpawner] Subscribed to ChunkManager.OnMapReady");

        _isMapReady = ChunkManager.IsMapReady;
        Debug.Log($"[PlayerSpawner] Initial map ready state = {_isMapReady}");
        Debug.Log($"[PlayerSpawner] Player prefab assigned = {_playerPrefab != null}");
        Debug.Log($"[PlayerSpawner] Ground mask value = {_groundMask.value}");

        if (_isMapReady)
        {
            Debug.Log("[PlayerSpawner] Map was already ready before PlayerSpawner subscribed.");
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[PlayerSpawner] OnStopServer called");

        if (InstanceFinder.SceneManager != null)
        {
            InstanceFinder.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            Debug.Log("[PlayerSpawner] Unsubscribed from OnClientLoadedStartScenes");
        }

        ChunkManager.OnMapReady -= HandleMapReady;
        Debug.Log("[PlayerSpawner] Unsubscribed from ChunkManager.OnMapReady");
    }

    private void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        Debug.Log($"[PlayerSpawner] OnClientLoadedStartScenes fired | client={conn?.ClientId} asServer={asServer}");

        if (!asServer)
            return;

        if (conn == null)
        {
            Debug.LogError("[PlayerSpawner] OnClientLoadedStartScenes received null connection");
            return;
        }

        Debug.Log($"[PlayerSpawner] Client {conn.ClientId} loaded start scenes");
        SpawnForConnection(conn);
    }

    public void SpawnForConnection(NetworkConnection conn)
    {
        Debug.Log($"[PlayerSpawner] SpawnForConnection called | client={conn?.ClientId}");

        if (!IsServerInitialized)
        {
            Debug.LogWarning("[PlayerSpawner] Server is not initialized yet");
            return;
        }

        if (conn == null)
        {
            Debug.LogWarning("[PlayerSpawner] Connection is null");
            return;
        }

        if (!conn.IsActive)
        {
            Debug.LogWarning($"[PlayerSpawner] Connection {conn.ClientId} is not active");
            return;
        }

        if (_playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] Player prefab is not assigned");
            return;
        }

        if (conn.FirstObject != null)
        {
            Debug.Log($"[PlayerSpawner] Client {conn.ClientId} already has a spawned object");
            return;
        }

        if (!_isMapReady)
        {
            if (!_pendingConnections.Contains(conn))
            {
                _pendingConnections.Add(conn);
                Debug.Log($"[PlayerSpawner] Client {conn.ClientId} queued until map is ready");
            }
            else
            {
                Debug.Log($"[PlayerSpawner] Client {conn.ClientId} already queued");
            }

            return;
        }

        Debug.Log($"[PlayerSpawner] Map ready already true, spawning client {conn.ClientId} now");
        SpawnPlayer(conn);
    }

    private void HandleMapReady()
    {
        _isMapReady = true;
        Debug.Log("[PlayerSpawner] HandleMapReady called -> map is ready");
        Debug.Log($"[PlayerSpawner] Pending connections count = {_pendingConnections.Count}");

        for (int i = 0; i < _pendingConnections.Count; i++)
        {
            NetworkConnection conn = _pendingConnections[i];

            if (conn == null)
            {
                Debug.LogWarning($"[PlayerSpawner] Pending connection index {i} is null");
                continue;
            }

            if (!conn.IsActive)
            {
                Debug.LogWarning($"[PlayerSpawner] Pending client {conn.ClientId} is inactive");
                continue;
            }

            if (conn.FirstObject != null)
            {
                Debug.Log($"[PlayerSpawner] Pending client {conn.ClientId} already has FirstObject");
                continue;
            }

            Debug.Log($"[PlayerSpawner] Spawning queued client {conn.ClientId}");
            SpawnPlayer(conn);
        }

        _pendingConnections.Clear();
        Debug.Log("[PlayerSpawner] Pending connections cleared");
    }

    private void SpawnPlayer(NetworkConnection conn)
    {
        Debug.Log($"[PlayerSpawner] SpawnPlayer called | client={conn?.ClientId}");

        if (conn == null)
        {
            Debug.LogError("[PlayerSpawner] SpawnPlayer received null connection");
            return;
        }

        if (_playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] _playerPrefab is null inside SpawnPlayer");
            return;
        }

        float x = Random.Range(-_spawnRadius, _spawnRadius);
        float z = Random.Range(-_spawnRadius, _spawnRadius);

        Vector3 rayStart = new Vector3(x, 50f, z);
        Vector3 pos = GetGroundPosition(rayStart);

        Debug.Log($"[PlayerSpawner] Random spawn candidate for client {conn.ClientId}: x={x}, z={z}");
        Debug.Log($"[PlayerSpawner] Final spawn position for client {conn.ClientId}: {pos}");

        NetworkObject player = Instantiate(_playerPrefab, pos, Quaternion.identity);

        if (player == null)
        {
            Debug.LogError("[PlayerSpawner] Instantiate returned null");
            return;
        }

        Debug.Log($"[PlayerSpawner] Instantiated player object: {player.name}");

        ServerManager.Spawn(player, conn);

        if (conn.FirstObject == null)
            Debug.LogWarning($"[PlayerSpawner] Spawn called but conn.FirstObject is still null for client {conn.ClientId}");
        else
            Debug.Log($"[PlayerSpawner] conn.FirstObject assigned for client {conn.ClientId}: {conn.FirstObject.name}");

        Debug.Log($"[PlayerSpawner] Spawned player for client {conn.ClientId} at {pos}");
    }

    private Vector3 GetGroundPosition(Vector3 checkPos)
    {
        Debug.Log($"[PlayerSpawner] Checking ground position from {checkPos}");

        if (Physics.Raycast(checkPos, Vector3.down, out RaycastHit hit, 100f, _groundMask))
        {
            Vector3 groundedPos = hit.point + Vector3.up * _heightOffset;
            Debug.Log($"[PlayerSpawner] Ground hit at {hit.point}, final grounded pos = {groundedPos}, collider = {hit.collider.name}");
            return groundedPos;
        }

        Vector3 fallbackPos = new Vector3(checkPos.x, 5f, checkPos.z);
        Debug.LogWarning($"[PlayerSpawner] Ground raycast missed. Using fallback position {fallbackPos}");
        return fallbackPos;
    }
}