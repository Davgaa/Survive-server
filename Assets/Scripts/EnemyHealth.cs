using UnityEngine;
using UnityEngine.Events;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class EnemyHealth : NetworkBehaviour
{
    [SerializeField] float _maxHp = 100f;
    [SerializeField] GameObject _healthBarPrefab;

    public UnityEvent OnDeath = new UnityEvent(); // ← нэмэх

    readonly SyncVar<float> _hp = new SyncVar<float>();
    EnemyHealthBar _healthBar;

    public override void OnStartServer()
    {
        base.OnStartServer();
        var ctrl = GetComponent<EnemyController>();
        _maxHp = ctrl?.data?.maxHealth ?? _maxHp;
        _hp.Value = _maxHp;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_healthBarPrefab != null)
        {
            GameObject bar = Instantiate(_healthBarPrefab,
                transform.position + Vector3.up * 1.5f,
                Quaternion.identity);
            bar.transform.SetParent(transform);
            bar.transform.localPosition = Vector3.up * 1.5f;
            _healthBar = bar.GetComponent<EnemyHealthBar>();
        }
        _hp.OnChange += OnHpChanged;
        _healthBar?.UpdateHealth(_hp.Value, _maxHp);
    }

    void OnHpChanged(float prev, float next, bool asServer)
    {
        if (_healthBar == null)
            _healthBar = GetComponentInChildren<EnemyHealthBar>();
        _healthBar?.UpdateHealth(next, _maxHp);
    }
    public void TakeDamage(float damage)
    {
        if (!IsServerStarted) return;
        _hp.Value -= damage;
        if (_hp.Value <= 0)
        {
            OnDeath?.Invoke(); // ← үхэх үед дуудах
            ServerManager.Despawn(gameObject);
        }
    }
}