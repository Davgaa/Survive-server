using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    private static NetworkBootstrap _instance;

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}