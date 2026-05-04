using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

public class WeaponController : NetworkBehaviour
{
    [SerializeField] float _damage = 25f;
    [SerializeField] float _range = 20f;
    [SerializeField] float _fireRate = 0.3f;
    float _nextFireTime;

    Camera _cam;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner) return;
        _cam = Camera.main;
    }

    void Update()
    {
        // 1. Хэрэв энэ дүр таных биш бол шууд зогсоо. (Лог бичих хэрэггүй)
        if (!IsOwner) return;

        // 2. Camera шалгах (Лог-ыг зөвхөн нэг удаа харуулна)
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam != null) Debug.Log("Camera successfully attached.");
            return;
        }

        // 3. Буудах логик
        bool shooting = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (shooting && Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + _fireRate;
            Shoot(); // "Shooting!" гэсэн лог-ыг бас устгавал Build хийхэд хэрэгтэй
        }
    }

    void Shoot()
    {
        // Animator олох
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger("Attack");

        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(ray, out RaycastHit hit, _range, layerMask))
        {
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                NetworkObject nob = enemy.GetComponent<NetworkObject>();
                DamageServerRpc(nob, _damage);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void DamageServerRpc(NetworkObject enemyObj, float damage)
    {
        Debug.Log("[RPC] DamageServerRpc called");

        if (!IsServer)
        {
            Debug.LogError("[RPC] Not running on server!");
            return;
        }

        if (enemyObj == null)
        {
            Debug.LogError("[RPC] enemyObj is NULL");
            return;
        }

        if (!enemyObj.IsSpawned)
        {
            Debug.LogError("[RPC] enemyObj is NOT SPAWNED");
            return;
        }

        if (!enemyObj.TryGetComponent<EnemyHealth>(out var enemy))
        {
            Debug.LogError("[RPC] EnemyHealth not found on object: " + enemyObj.name);
            return;
        }

        Debug.Log("[RPC] Applying damage: " + damage);

        enemy.TakeDamage(damage);
    }
}