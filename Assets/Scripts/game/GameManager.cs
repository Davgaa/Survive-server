using UnityEngine;
using FishNet;
using FishNet.Managing.Scened;

public class GameManager : MonoBehaviour
{
    void Start()
    {
#if !UNITY_SERVER
        UnityEngine.SceneManagement
            .SceneManager.LoadScene("Mainmenu");
#endif
    }

    public void StartAsHost()
    {
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager
            .StartConnection("localhost");
        var data = new SceneLoadData("Game");
        InstanceFinder.SceneManager
            .LoadGlobalScenes(data);
    }
}