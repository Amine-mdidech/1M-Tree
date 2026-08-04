using UnityEngine;

public static class VfxUtility
{
    private static Sprite cachedDotSprite;

    private static Sprite GetDotSprite()
    {
        if (cachedDotSprite == null)
        {
            cachedDotSprite = PixelArtUtility.CreateDotSprite(16, Color.white);
        }

        return cachedDotSprite;
    }

    public static void SpawnBurst(Vector3 position, Color color, int particleCount = 7, float speed = 1.6f, float lifetime = 0.45f, int sortingOrder = 5)
    {
        Sprite dotSprite = GetDotSprite();

        for (int i = 0; i < particleCount; i++)
        {
            float angle = (i / (float)particleCount) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            float particleSpeed = speed * Random.Range(0.6f, 1.1f);
            Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * particleSpeed;

            GameObject go = new GameObject("BurstParticle");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.14f);

            BurstParticle particle = go.AddComponent<BurstParticle>();
            particle.Initialize(velocity, lifetime * Random.Range(0.85f, 1.15f), color, dotSprite, sortingOrder);
        }
    }
}
