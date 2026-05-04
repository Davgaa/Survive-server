using UnityEngine;
using FishNet.Object;

public class CameraAttacher : NetworkBehaviour
{
    [SerializeField] Transform _cameraHolder;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner) return;

        StartCoroutine(AttachCamera());
    }

    System.Collections.IEnumerator AttachCamera()
    {
        // Camera бэлэн болтол хүлээх
        Camera cam = null;
        while (cam == null)
        {
            cam = Camera.main;
            yield return null;
        }

        cam.transform.SetParent(_cameraHolder);
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;

        Debug.Log("Camera амжилттай холбогдлоо!");
    }
}