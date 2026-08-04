using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BurstParticle : MonoBehaviour
{
    private Vector3 velocity;
    private float lifetime;
    private float age;
    private Color baseColor;
    private SpriteRenderer spriteRenderer;

    public void Initialize(Vector3 initialVelocity, float particleLifetime, Color color, Sprite sprite, int sortingOrder)
    {
        velocity = initialVelocity;
        lifetime = Mathf.Max(0.05f, particleLifetime);
        age = 0f;
        baseColor = color;

        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float t = age / lifetime;
        transform.position += velocity * Time.deltaTime;
        velocity *= 0.9f;
        transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, t);

        Color color = baseColor;
        color.a = Mathf.Lerp(1f, 0f, t);
        spriteRenderer.color = color;
    }
}
