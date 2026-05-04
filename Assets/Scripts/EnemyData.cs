using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData",
                 menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public float maxHealth = 100f;
    public float moveSpeed = 3.5f;
    public float damage = 10f;
    public float attackRange = 1.5f;
    public int scoreValue = 10;
}