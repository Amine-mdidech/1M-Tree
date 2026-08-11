using UnityEngine;

public class LandBoundaryVisual : MonoBehaviour
{
    [SerializeField] private Color barrierColor = new Color(1f, 0.85f, 0.3f, 0.85f);
    [SerializeField] private float thickness = 0.15f;

    private static Sprite pixelSprite;

    private SpriteRenderer top;
    private SpriteRenderer bottom;
    private SpriteRenderer left;
    private SpriteRenderer right;

    private float lastHalfExtent = -1f;

    private void Awake()
    {
        top = CreateBar("LandBoundary_Top");
        bottom = CreateBar("LandBoundary_Bottom");
        left = CreateBar("LandBoundary_Left");
        right = CreateBar("LandBoundary_Right");
    }

    private void Update()
    {
        if (LandManager.Instance == null)
        {
            return;
        }

        float halfExtent = LandManager.Instance.CurrentLandHalfExtent;
        if (Mathf.Approximately(halfExtent, lastHalfExtent))
        {
            return;
        }

        lastHalfExtent = halfExtent;
        ApplyLayout(halfExtent);
    }

    private SpriteRenderer CreateBar(string barName)
    {
        GameObject barObject = new GameObject(barName);
        barObject.transform.SetParent(transform, false);
        SpriteRenderer barRenderer = barObject.AddComponent<SpriteRenderer>();
        barRenderer.sprite = GetPixelSprite();
        barRenderer.color = barrierColor;
        barRenderer.sortingOrder = 1;
        return barRenderer;
    }

    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null)
        {
            return pixelSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return pixelSprite;
    }

    private void ApplyLayout(float halfExtent)
    {
        float size = halfExtent * 2f;

        top.transform.localPosition = new Vector3(0f, halfExtent, 0f);
        top.transform.localScale = new Vector3(size, thickness, 1f);

        bottom.transform.localPosition = new Vector3(0f, -halfExtent, 0f);
        bottom.transform.localScale = new Vector3(size, thickness, 1f);

        left.transform.localPosition = new Vector3(-halfExtent, 0f, 0f);
        left.transform.localScale = new Vector3(thickness, size, 1f);

        right.transform.localPosition = new Vector3(halfExtent, 0f, 0f);
        right.transform.localScale = new Vector3(thickness, size, 1f);
    }
}
