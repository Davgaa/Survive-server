using UnityEngine;
using UnityEngine.AI;
using FishNet.Object;

public class EnemyController : NetworkBehaviour
{
    [SerializeField] public EnemyData data;

    NavMeshAgent _agent;
    Transform _target;
    Animator _animator;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        // Animator-г child-аас хайх
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
            Debug.Log($"{gameObject.name}: Animator олдсонгүй!");
        else
            Debug.Log($"{gameObject.name}: Animator олдлоо!");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!IsServerStarted)
        {
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
                agent.enabled = false;
        }
    }

    [Server]
    void FindTarget()
    {
        GameObject[] players =
            GameObject.FindGameObjectsWithTag("Player");
        float closest = Mathf.Infinity;
        foreach (var p in players)
        {
            float dist = Vector3.Distance(
                transform.position, p.transform.position);
            if (dist < closest)
            {
                closest = dist;
                _target = p.transform;
            }
        }
    }

    void Update()
    {
        if (!IsServerStarted || _target == null) return;

        float dist = Vector3.Distance(
            transform.position, _target.position);
        float range = data != null ? data.attackRange : 1.5f;

        if (dist > range)
        {
            _agent.SetDestination(_target.position);
            UpdateAnimationRpc(1f, false);
        }
        else
        {
            _agent.ResetPath();
            UpdateAnimationRpc(0f, true);
        }
    }

    [ObserversRpc]
    void UpdateAnimationRpc(float speed, bool attack)
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
        _animator?.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        if (attack) _animator?.SetTrigger("Attack");
    }
}