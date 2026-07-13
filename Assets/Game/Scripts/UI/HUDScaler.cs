using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class HUDScaler : MonoBehaviour
{
    [SerializeField] private RectTransform hudRoot;

    [SerializeField] private float portraitScale = 1.2f;
    [SerializeField] private float landscapeScale = 0.85f;

    private int lastWidth;
    private int lastHeight;

    private void Start()
    {
        UpdateScale();
    }

    private void OnRectTransformDimensionsChange()
    {
        // Evita chamadas redundantes se as dimensões não mudaram de verdade
        if (Screen.width == lastWidth && Screen.height == lastHeight) return;

        UpdateScale();
    }

    private void UpdateScale()
    {
        lastWidth  = Screen.width;
        lastHeight = Screen.height;

        bool portrait = Screen.height > Screen.width;

        float scale = portrait ? portraitScale : landscapeScale;
        hudRoot.localScale = Vector3.one * scale;
    }
}