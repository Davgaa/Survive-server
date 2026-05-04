using UnityEngine;
using System.Collections.Generic;

public class TreeSpawner : MonoBehaviour
{
    public GameObject[] treePrefabs;
    public int treeCount = 15;
    public float mapRadius = 35f;
    public float arenaRadius = 5f;
    public float minScale = 0.8f;
    public float maxScale = 1f;
    public float minTreeDistance = 5f;

    readonly List<Vector3> _pos = new List<Vector3>();

    [ContextMenu("Spawn Trees")]
    public void SpawnTreesEditor()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        _pos.Clear();
        Spawn(treePrefabs, treeCount, mapRadius,
              arenaRadius, minTreeDistance, true);
    }

    public void SpawnTreesRuntime(GameObject[] p, int c,
        float mr, float ar, float md)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        _pos.Clear();
        Spawn(p, c, mr, ar, md, false);
    }

    void Spawn(GameObject[] prefabs, int count,
        float mr, float ar, float md, bool editor)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        int spawned = 0, tries = 0;

        while (spawned < count && tries < count * 20)
        {
            tries++;
            float x = Random.Range(-mr, mr);
            float z = Random.Range(-mr, mr);
            var f = new Vector2(x, z);
            if (f.magnitude < ar || f.magnitude > mr) continue;

            var cand = new Vector3(x, 0, z);
            bool close = false;
            foreach (var p in _pos)
                if (Vector3.Distance(cand, p) < md)
                { close = true; break; }
            if (close) continue;

            var ray = new Ray(new Vector3(x, 100f, z), Vector3.down);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) continue;
            if (hit.collider.gameObject.layer !=
                LayerMask.NameToLayer("Ground")) continue;

            var pref = prefabs[Random.Range(0, prefabs.Length)];
            GameObject tree;
#if UNITY_EDITOR
            tree = editor
                ? UnityEditor.PrefabUtility
                    .InstantiatePrefab(pref) as GameObject
                : Instantiate(pref);
#else
            tree = Instantiate(pref);
#endif
            tree.transform.SetPositionAndRotation(hit.point,
                Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            tree.transform.localScale =
                Vector3.one * Random.Range(minScale, maxScale);
            tree.transform.SetParent(transform);
            _pos.Add(cand);
            spawned++;
        }
        Debug.Log($"{spawned} мод байрлуулав!");
    }
}