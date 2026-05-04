using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData",
                 menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Мэдээлэл")]
    public string characterName;
    public Sprite portrait;        // UI зураг
    public GameObject prefab;      // Network prefab

    [Header("Статус")]
    public float maxHealth = 100f;
    public float speed = 5f;
    public float damage = 25f;

    [Header("Тайлбар")]
    [TextArea] public string description;
}