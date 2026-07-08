using UnityEngine;

public class HUDScaler : MonoBehaviour
{
    [SerializeField] private RectTransform hudRoot;

    [SerializeField] private float portraitScale = 1.2f;
    [SerializeField] private float landscapeScale = 0.85f;

    void Start()
    {
        UpdateScale();
    }
   
    void UpdateScale()
    {
        bool portrait = Screen.height > Screen.width;

        float scale = portrait ? portraitScale : landscapeScale;
        hudRoot.localScale = Vector3.one * scale;
    }   
}