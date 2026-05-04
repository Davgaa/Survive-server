using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine.Events;

public class WaveManager : NetworkBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private NetworkObject _banditPrefab;
    [SerializeField] private NetworkObject _knightPrefab;
    [SerializeField] private NetworkObject _skeletonPrefab;

    [Header("Газар илрүүлэлт")]
    [SerializeField] private LayerMask _groundMask;

    [Header("Wave тохиргоо")]
    [SerializeField] private float _timeBetweenWaves = 15f;
    [SerializeField] private float _spawnInterval = 0.5f;

    private readonly SyncVar<int> _currentWave = new SyncVar<int>();
    private readonly SyncVar<int> _enemiesAlive = new SyncVar<int>();

    public int CurrentWave => _currentWave.Value;
    public int EnemiesAlive => _enemiesAlive.Value;

    private bool _canStartWaves = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[WaveManager] OnStartServer called");

        if (ChunkManager.IsLoaded)
        {
            HandleMapReady();
        }
        else
        {
            ChunkManager.OnMapReady += HandleMapReady;
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        ChunkManager.OnMapReady -= HandleMapReady;
    }

    private void HandleMapReady()
    {
        if (_canStartWaves)
            return;

        _canStartWaves = true;
        Debug.Log("[WaveManager] Map ready. Starting waves.");
        StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        yield return new WaitForSeconds(2f);

        while (_canStartWaves)
        {
            _currentWave.Value++;
            Debug.Log($"[WaveManager] Starting wave {_currentWave.Value}");

            yield return StartCoroutine(SpawnWave(_currentWave.Value));

            while (_enemiesAlive.Value > 0)
                yield return new WaitForSeconds(1f);

            Debug.Log($"[WaveManager] Wave {_currentWave.Value} дууслаа!");
            yield return new WaitForSeconds(_timeBetweenWaves);
        }
    }

    private IEnumerator SpawnWave(int wave)
    {
        int banditCount = 3 + wave * 2;
        int knightCount = wave >= 3 ? wave : 0;
        int skeletonCount = wave >= 5 ? wave - 3 : 0;

        for (int i = 0; i < banditCount; i++)
        {
            SpawnEnemy(_banditPrefab);
            yield return new WaitForSeconds(_spawnInterval);
        }

        for (int i = 0; i < knightCount; i++)
        {
            SpawnEnemy(_knightPrefab);
            yield return new WaitForSeconds(_spawnInterval * 1.5f);
        }

        for (int i = 0; i < skeletonCount; i++)
        {
            SpawnEnemy(_skeletonPrefab);
            yield return new WaitForSeconds(_spawnInterval * 2f);
        }
    }

    [Server]
    private void SpawnEnemy(NetworkObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[WaveManager] Enemy prefab is null.");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();
        if (spawnPos == Vector3.zero)
        {
            Debug.LogWarning("[WaveManager] Could not find valid spawn position.");
            return;
        }

        NetworkObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
        ServerManager.Spawn(enemy);

        _enemiesAlive.Value++;
        Debug.Log($"[WaveManager] Spawned enemy. Alive = {_enemiesAlive.Value}");
        Debug.Log($"[WaveManager] Spawning enemy at {spawnPos}");
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.OnDeath.AddListener(() =>
            {
                _enemiesAlive.Value = Mathf.Max(0, _enemiesAlive.Value - 1);
                Debug.Log($"[WaveManager] Enemy died. Alive = {_enemiesAlive.Value}");
            });
        }
        else
        {
            Debug.LogWarning("[WaveManager] EnemyHealth component missing on enemy prefab.");
        }
    }

    private Vector3 GetSpawnPosition()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(20f, 30f);

        float x = Mathf.Cos(angle) * dist;
        float z = Mathf.Sin(angle) * dist;

        Vector3 rayStart = new Vector3(x, 50f, z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, _groundMask))
            return hit.point + Vector3.up * 1f;

        return Vector3.zero;
    }
}