using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] Image _fillImage;
    Camera _cam;

    void Start()
    {
        _cam = Camera.main;
    }

    void LateUpdate()
    {
        // Camera руу үргэлж харуулах
        if (_cam != null)
            transform.rotation = _cam.transform.rotation;
    }

    public void UpdateHealth(float current, float max)
    {
        if (_fillImage != null)
            _fillImage.fillAmount = current / max;
    }
}