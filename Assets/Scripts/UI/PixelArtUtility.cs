using UnityEngine;

public static class PixelArtUtility
{
    // In-memory pixel canvas used while composing a sprite. Shape draws write into this managed
    // array (cheap random-access) instead of calling Texture2D.SetPixel/GetPixel per pixel (each
    // of which is a slow native call); the whole buffer is uploaded to the GPU with a single
    // SetPixels call once composition is finished.
    public sealed class PixelBuffer
    {
        public readonly Color[] Pixels;
        public readonly int Width;
        public readonly int Height;

        public PixelBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color[width * height];
        }

        public void Set(int x, int y, Color color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }

            Pixels[y * Width + x] = color;
        }

        public void Blend(int x, int y, Color color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }

            int index = y * Width + x;
            Pixels[index] = color.a >= 0.999f ? color : Color.Lerp(Pixels[index], color, color.a);
        }

        public Texture2D ToTexture()
        {
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(Pixels);
            texture.Apply();
            return texture;
        }
    }

    public static void FillCircle(PixelBuffer buffer, Vector2 centerPixel, float radius, Color fill, Color border, int borderWidth)
    {
        int xMin = Mathf.Max(0, Mathf.FloorToInt(centerPixel.x - radius));
        int xMax = Mathf.Min(buffer.Width - 1, Mathf.CeilToInt(centerPixel.x + radius));
        int yMin = Mathf.Max(0, Mathf.FloorToInt(centerPixel.y - radius));
        int yMax = Mathf.Min(buffer.Height - 1, Mathf.CeilToInt(centerPixel.y + radius));

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centerPixel) - radius;
                if (dist > 0f)
                {
                    continue;
                }

                Color color = borderWidth > 0 && dist > -borderWidth ? border : fill;
                buffer.Blend(x, y, color);
            }
        }
    }

    private static void FillDiamond(PixelBuffer buffer, Vector2 centerPixel, float radius, Color fill, Color border, int borderWidth)
    {
        int xMin = Mathf.Max(0, Mathf.FloorToInt(centerPixel.x - radius));
        int xMax = Mathf.Min(buffer.Width - 1, Mathf.CeilToInt(centerPixel.x + radius));
        int yMin = Mathf.Max(0, Mathf.FloorToInt(centerPixel.y - radius));
        int yMax = Mathf.Min(buffer.Height - 1, Mathf.CeilToInt(centerPixel.y + radius));

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - centerPixel.x);
                float dy = Mathf.Abs(y + 0.5f - centerPixel.y);
                float dist = (dx + dy) - radius;
                if (dist > 0f)
                {
                    continue;
                }

                Color color = borderWidth > 0 && dist > -borderWidth ? border : fill;
                buffer.Blend(x, y, color);
            }
        }
    }

    private static void FillRoundedRectRegion(PixelBuffer buffer, RectInt region, float cornerRadius, Color fill, Color border, int borderWidth)
    {
        int xMin = Mathf.Max(0, region.xMin);
        int xMax = Mathf.Min(buffer.Width - 1, region.xMax - 1);
        int yMin = Mathf.Max(0, region.yMin);
        int yMax = Mathf.Min(buffer.Height - 1, region.yMax - 1);

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                float outside = RoundedRectDistanceInRegion(x, y, region, cornerRadius);
                if (outside > 0f)
                {
                    continue;
                }

                Color color = borderWidth > 0 && outside > -borderWidth ? border : fill;
                buffer.Blend(x, y, color);
            }
        }
    }

    private static float RoundedRectDistanceInRegion(int x, int y, RectInt region, float radius)
    {
        float px = x + 0.5f - region.xMin;
        float py = y + 0.5f - region.yMin;
        float halfW = region.width / 2f;
        float halfH = region.height / 2f;

        float qx = Mathf.Max(Mathf.Abs(px - halfW) - (halfW - radius), 0f);
        float qy = Mathf.Max(Mathf.Abs(py - halfH) - (halfH - radius), 0f);

        return Mathf.Sqrt(qx * qx + qy * qy) - radius;
    }

    public static void FillRect(PixelBuffer buffer, RectInt rect, Color color)
    {
        int xMin = Mathf.Max(0, rect.xMin);
        int xMax = Mathf.Min(buffer.Width - 1, rect.xMax - 1);
        int yMin = Mathf.Max(0, rect.yMin);
        int yMax = Mathf.Min(buffer.Height - 1, rect.yMax - 1);

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                buffer.Blend(x, y, color);
            }
        }
    }

    private static void FillEllipse(PixelBuffer buffer, Vector2 centerPixel, float radiusX, float radiusY, Color fill, Color border, int borderWidth)
    {
        float minRadius = Mathf.Max(0.001f, Mathf.Min(radiusX, radiusY));

        int xMin = Mathf.Max(0, Mathf.FloorToInt(centerPixel.x - radiusX));
        int xMax = Mathf.Min(buffer.Width - 1, Mathf.CeilToInt(centerPixel.x + radiusX));
        int yMin = Mathf.Max(0, Mathf.FloorToInt(centerPixel.y - radiusY));
        int yMax = Mathf.Min(buffer.Height - 1, Mathf.CeilToInt(centerPixel.y + radiusY));

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                float nx = (x + 0.5f - centerPixel.x) / radiusX;
                float ny = (y + 0.5f - centerPixel.y) / radiusY;
                float dist = (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * minRadius;
                if (dist > 0f)
                {
                    continue;
                }

                Color color = borderWidth > 0 && dist > -borderWidth ? border : fill;
                buffer.Blend(x, y, color);
            }
        }
    }

    private static void FillTriangleUp(PixelBuffer buffer, RectInt bounds, Color fill)
    {
        int height = Mathf.Max(1, bounds.height);
        int centerX = bounds.xMin + bounds.width / 2;

        int yMin = Mathf.Max(0, bounds.yMin);
        int yMax = Mathf.Min(buffer.Height - 1, bounds.yMax - 1);

        for (int y = yMin; y <= yMax; y++)
        {
            float t = height <= 1 ? 0f : (y - bounds.yMin) / (float)(height - 1);
            int halfWidth = Mathf.RoundToInt(Mathf.Lerp(bounds.width / 2f, 0f, t));

            int xMin = Mathf.Max(0, centerX - halfWidth);
            int xMax = Mathf.Min(buffer.Width - 1, centerX + halfWidth);
            for (int x = xMin; x <= xMax; x++)
            {
                buffer.Blend(x, y, fill);
            }
        }
    }

    private static void FillTriangleDown(PixelBuffer buffer, RectInt bounds, Color fill)
    {
        int height = Mathf.Max(1, bounds.height);
        int centerX = bounds.xMin + bounds.width / 2;

        int yMin = Mathf.Max(0, bounds.yMin);
        int yMax = Mathf.Min(buffer.Height - 1, bounds.yMax - 1);

        for (int y = yMin; y <= yMax; y++)
        {
            float t = height <= 1 ? 0f : (y - bounds.yMin) / (float)(height - 1);
            int halfWidth = Mathf.RoundToInt(Mathf.Lerp(0f, bounds.width / 2f, t));

            int xMin = Mathf.Max(0, centerX - halfWidth);
            int xMax = Mathf.Min(buffer.Width - 1, centerX + halfWidth);
            for (int x = xMin; x <= xMax; x++)
            {
                buffer.Blend(x, y, fill);
            }
        }
    }

    private static void FillTriangleRight(PixelBuffer buffer, RectInt bounds, Color fill)
    {
        int width = Mathf.Max(1, bounds.width);
        int centerY = bounds.yMin + bounds.height / 2;

        int xMin = Mathf.Max(0, bounds.xMin);
        int xMax = Mathf.Min(buffer.Width - 1, bounds.xMax - 1);

        for (int x = xMin; x <= xMax; x++)
        {
            float t = width <= 1 ? 0f : (x - bounds.xMin) / (float)(width - 1);
            int halfHeight = Mathf.RoundToInt(Mathf.Lerp(bounds.height / 2f, 0f, t));

            int yMin = Mathf.Max(0, centerY - halfHeight);
            int yMax = Mathf.Min(buffer.Height - 1, centerY + halfHeight);
            for (int y = yMin; y <= yMax; y++)
            {
                buffer.Blend(x, y, fill);
            }
        }
    }

    private static void DrawFrondLine(PixelBuffer buffer, Vector2 from, Vector2 to, float thickness, Color color)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to)));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = Vector2.Lerp(from, to, t);
            FillCircle(buffer, point, thickness * 0.5f, color, color, 0);
        }
    }

    public static Texture2D CreateWoodGrainTexture(int width, int height, Color baseColor)
    {
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.02f, y * 0.15f);
                float rings = Mathf.Sin(y * 0.3f + Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 6f) * 0.5f + 0.5f;
                float shade = Mathf.Lerp(0.82f, 1.12f, grain * 0.6f + rings * 0.4f);
                Color color = baseColor * shade;
                color.a = 1f;
                pixels[y * width + x] = color;
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    public static Texture2D CreateParchmentTexture(int width, int height, Color baseColor)
    {
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                float fineGrain = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.15f;
                float shade = Mathf.Lerp(0.9f, 1.08f, noise) - fineGrain;
                Color color = baseColor * shade;
                color.a = 1f;
                pixels[y * width + x] = color;
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    // Warm brass/bronze pill background with a faint moss fleck hugging the bottom edge - the HUD
    // badge frame's small nod to the game's "built + reclaimed by nature" identity (see the
    // Laboratory building in art-design-document.md), kept subtle since this sits behind a
    // compact badge rather than a full panel.
    public static Texture2D CreateBrassMossTexture(int width, int height, Color baseColor)
    {
        PixelBuffer buffer = new PixelBuffer(width, height);

        Color topColor = Color.Lerp(baseColor, Color.white, 0.22f);
        Color bottomColor = baseColor * 0.7f;
        bottomColor.a = baseColor.a;

        for (int y = 0; y < height; y++)
        {
            float t = y / (float)Mathf.Max(1, height - 1);
            Color rowColor = Color.Lerp(topColor, bottomColor, t);
            for (int x = 0; x < width; x++)
            {
                buffer.Set(x, y, rowColor);
            }
        }

        ApplyGrain(buffer, baseColor.GetHashCode(), 0.07f);
        ApplyMossFleck(buffer, baseColor.GetHashCode() ^ 0x5bd1e995);

        return buffer.ToTexture();
    }

    private static void ApplyMossFleck(PixelBuffer buffer, int seed)
    {
        System.Random rng = new System.Random(seed);
        Color moss = new Color(0.35f, 0.5f, 0.22f);
        int flecks = Mathf.Max(3, (buffer.Width * buffer.Height) / 900);

        for (int i = 0; i < flecks; i++)
        {
            int x = rng.Next(buffer.Width);
            // Hugs the bottom third of the badge, like moss creeping up from a base edge rather
            // than scattered at random across the whole face.
            int y = rng.Next((int)(buffer.Height * 0.35f));
            float alpha = 0.12f + (float)rng.NextDouble() * 0.18f;
            buffer.Blend(x, y, new Color(moss.r, moss.g, moss.b, alpha));
        }
    }

    // Horizontal gradient with a brighter streak through the upper-middle fading to darker at the
    // bottom edge - the same top-lit bevel convention as every other beveled HUD element, applied
    // to a progress-bar fill instead of a panel/button face. Stretch-to-fill means this reads
    // correctly at any fill width, from a sliver to the full bar.
    public static Texture2D CreateGlowBarTexture(int width, int height, Color colorA, Color colorB)
    {
        PixelBuffer buffer = new PixelBuffer(width, height);

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)Mathf.Max(1, width - 1);
            Color baseColor = Color.Lerp(colorA, colorB, t);

            for (int y = 0; y < height; y++)
            {
                float rowT = y / (float)Mathf.Max(1, height - 1);
                float shade = Mathf.Lerp(1.25f, 0.75f, rowT);
                Color color = baseColor * shade;
                color.a = baseColor.a;
                buffer.Set(x, y, color);
            }
        }

        return buffer.ToTexture();
    }

    public static Texture2D CreateStoneTexture(int width, int height, Color baseColor)
    {
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float blockNoise = Mathf.PerlinNoise(x * 0.04f, y * 0.04f);
                float speckle = Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.2f;
                float shade = Mathf.Lerp(0.8f, 1.14f, blockNoise) - speckle;
                Color color = baseColor * shade;
                color.a = 1f;
                pixels[y * width + x] = color;
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    public static Sprite ToSprite(Texture2D texture, float pixelsPerUnit = 100f)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    // A soft-edged ground decal (solid center, feathered falloff toward the rim) rather than a
    // hard-edged FillCircle - a crisp circle would read as a sticker pasted on the dirt, while the
    // feather lets it blend into the surrounding ground texture the way a real patch of growth
    // would.
    public static Sprite CreateGroundPatchSprite(int size, Color color, float pixelsPerUnit)
    {
        PixelBuffer buffer = new PixelBuffer(size, size);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;
        const float solidFraction = 0.55f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                float alpha;
                if (t >= 1f)
                {
                    alpha = 0f;
                }
                else if (t < solidFraction)
                {
                    alpha = 1f;
                }
                else
                {
                    alpha = 1f - Mathf.SmoothStep(0f, 1f, (t - solidFraction) / (1f - solidFraction));
                }

                Color pixelColor = color;
                pixelColor.a = alpha * color.a;
                buffer.Set(x, y, pixelColor);
            }
        }

        ApplyGrain(buffer, color.GetHashCode(), 0.05f);
        return ToSprite(buffer.ToTexture(), pixelsPerUnit);
    }

    public static Sprite CreateDotSprite(int size, Color color)
    {
        PixelBuffer buffer = new PixelBuffer(size, size);
        FillCircle(buffer, new Vector2(size * 0.5f, size * 0.5f), size * 0.42f, color, Color.clear, 0);
        return ToSprite(buffer.ToTexture());
    }

    public static Sprite CreateIconButtonSprite(int size, Color backgroundColor, Color borderColor, System.Action<PixelBuffer> drawGlyph)
    {
        PixelBuffer buffer = new PixelBuffer(size, size);

        int shadowOffset = Mathf.Max(3, size / 12);
        Color shadowColor = new Color(0.07f, 0.05f, 0.04f);
        Color faceColor = Color.Lerp(backgroundColor, Color.white, 0.08f);

        FillRoundedRectRegion(buffer, new RectInt(shadowOffset, 0, size - shadowOffset, size - shadowOffset), size * 0.22f, shadowColor, shadowColor, 0);
        FillRoundedRectRegion(buffer, new RectInt(0, shadowOffset, size - shadowOffset, size - shadowOffset), size * 0.22f, faceColor, borderColor, Mathf.Max(2, size / 16));

        drawGlyph?.Invoke(buffer);
        return ToSprite(buffer.ToTexture());
    }

    public static Sprite CreateGlyphSprite(int size, System.Action<PixelBuffer> drawGlyph)
    {
        PixelBuffer buffer = new PixelBuffer(size, size);
        drawGlyph?.Invoke(buffer);
        return ToSprite(buffer.ToTexture());
    }

    public static Sprite CreateBadgedGlyphSprite(int size, Color badgeColor, System.Action<PixelBuffer> drawGlyph)
    {
        PixelBuffer buffer = new PixelBuffer(size, size);

        Color shadeColor = badgeColor * 0.6f;
        shadeColor.a = 1f;
        Color highlightColor = Color.Lerp(badgeColor, Color.white, 0.3f);

        FillCircle(buffer, new Vector2(size * 0.52f, size * 0.46f), size * 0.46f, shadeColor, Color.clear, 0);
        FillCircle(buffer, new Vector2(size * 0.48f, size * 0.5f), size * 0.42f, badgeColor, Color.clear, 0);
        FillCircle(buffer, new Vector2(size * 0.34f, size * 0.36f), size * 0.14f, highlightColor, Color.clear, 0);

        // Subtle per-pixel grain on top of the flat fill/shade/highlight discs above — a perfectly
        // flat color reads as "1-dimensional"/plasticky; a little brightness jitter gives it a
        // brushed-material feel instead, closer to how a real painted/anodized badge catches light
        // unevenly. Seeded from the badge color so it's deterministic (same color -> same grain,
        // no flicker on regeneration) while still varying visibly badge-to-badge.
        ApplyGrain(buffer, badgeColor.GetHashCode(), 0.06f);

        drawGlyph?.Invoke(buffer);
        return ToSprite(buffer.ToTexture());
    }

    // Reusable material-grain pass: nudges each already-opaque pixel's brightness by a small random
    // amount so flat fills read as a textured surface instead of a perfectly flat color swatch.
    // Skips fully-transparent pixels so it never "dirties" empty canvas area around a shape.
    private static void ApplyGrain(PixelBuffer buffer, int seed, float strength)
    {
        System.Random rng = new System.Random(seed);
        for (int i = 0; i < buffer.Pixels.Length; i++)
        {
            Color pixel = buffer.Pixels[i];
            if (pixel.a <= 0f)
            {
                continue;
            }

            float jitter = ((float)rng.NextDouble() - 0.5f) * strength;
            buffer.Pixels[i] = new Color(
                Mathf.Clamp01(pixel.r + jitter),
                Mathf.Clamp01(pixel.g + jitter),
                Mathf.Clamp01(pixel.b + jitter),
                pixel.a);
        }
    }

    // Small tileable-ish textured fill for compact flat-color HUD chrome (badges/pills) that isn't
    // large enough to warrant the full wood/parchment/stone Perlin textures — a soft top-to-bottom
    // gradient (reads as a rounded/lit surface instead of a flat swatch) plus the same grain pass.
    public static Texture2D CreateTexturedPillBackground(int width, int height, Color baseColor)
    {
        PixelBuffer buffer = new PixelBuffer(width, height);

        Color topColor = Color.Lerp(baseColor, Color.white, 0.12f);
        Color bottomColor = baseColor * 0.82f;
        bottomColor.a = baseColor.a;

        for (int y = 0; y < height; y++)
        {
            float t = y / (float)Mathf.Max(1, height - 1);
            Color rowColor = Color.Lerp(topColor, bottomColor, t);
            for (int x = 0; x < width; x++)
            {
                buffer.Set(x, y, rowColor);
            }
        }

        ApplyGrain(buffer, baseColor.GetHashCode(), 0.05f);

        return buffer.ToTexture();
    }

    public static void DrawBookGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color cover = new Color(0.55f, 0.35f, 0.18f);
        Color page = new Color(0.96f, 0.92f, 0.78f);

        FillRect(canvas, new RectInt((int)(size * 0.22f), (int)(size * 0.2f), (int)(size * 0.56f), (int)(size * 0.6f)), cover);
        FillRect(canvas, new RectInt((int)(size * 0.27f), (int)(size * 0.25f), (int)(size * 0.2f), (int)(size * 0.5f)), page);
        FillRect(canvas, new RectInt((int)(size * 0.53f), (int)(size * 0.25f), (int)(size * 0.2f), (int)(size * 0.5f)), page);
        FillRect(canvas, new RectInt((int)(size * 0.47f), (int)(size * 0.2f), (int)(size * 0.06f), (int)(size * 0.6f)), cover);
    }

    public static void DrawFlaskGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color glass = new Color(0.85f, 0.93f, 0.95f);
        Color liquid = new Color(0.35f, 0.75f, 0.55f);

        FillRect(canvas, new RectInt((int)(size * 0.42f), (int)(size * 0.12f), (int)(size * 0.16f), (int)(size * 0.28f)), glass);
        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.62f), size * 0.28f, glass, glass, 0);
        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.72f), size * 0.24f, liquid, liquid, 0);
    }

    public static void DrawLeafGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color trunk = new Color(0.45f, 0.3f, 0.15f);
        Color canopy = new Color(0.3f, 0.65f, 0.25f);
        Color canopyLight = new Color(0.45f, 0.78f, 0.35f);

        FillRect(canvas, new RectInt((int)(size * 0.46f), (int)(size * 0.55f), (int)(size * 0.08f), (int)(size * 0.32f)), trunk);
        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.4f), size * 0.3f, canopy, canopy, 0);
        FillCircle(canvas, new Vector2(size * 0.4f, size * 0.32f), size * 0.14f, canopyLight, canopyLight, 0);
    }

    // Trunk at the bottom, two tapering triangular tiers stacked above it - a right-side-up pine
    // silhouette (canvas Y=0 is the bottom, per PixelBuffer/Texture2D convention), unlike
    // DrawLeafGlyph above whose trunk sits in the upper half and canopy in the lower half.
    public static void DrawPineGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color trunk = new Color(0.45f, 0.3f, 0.15f);
        Color canopy = new Color(0.22f, 0.5f, 0.28f);
        Color canopyLight = new Color(0.35f, 0.65f, 0.38f);

        FillRect(canvas, new RectInt((int)(size * 0.46f), (int)(size * 0.08f), (int)(size * 0.08f), (int)(size * 0.2f)), trunk);
        FillTriangleUp(canvas, new RectInt((int)(size * 0.16f), (int)(size * 0.22f), (int)(size * 0.68f), (int)(size * 0.32f)), canopy);
        FillTriangleUp(canvas, new RectInt((int)(size * 0.28f), (int)(size * 0.48f), (int)(size * 0.44f), (int)(size * 0.3f)), canopyLight);
    }

    public static void DrawGemGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color gem = new Color(0.35f, 0.75f, 0.9f);
        Color gemLight = new Color(0.75f, 0.95f, 1f);

        FillDiamond(canvas, new Vector2(size * 0.5f, size * 0.5f), size * 0.34f, gem, gem, 0);
        FillDiamond(canvas, new Vector2(size * 0.42f, size * 0.42f), size * 0.12f, gemLight, gemLight, 0);
    }

    public static void DrawCoinGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color coin = new Color(0.95f, 0.78f, 0.25f);
        Color coinLight = new Color(1f, 0.92f, 0.6f);

        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.5f), size * 0.34f, coin, coin, 0);
        FillCircle(canvas, new Vector2(size * 0.4f, size * 0.4f), size * 0.1f, coinLight, coinLight, 0);
    }

    public static void DrawPersonGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color body = new Color(0.9f, 0.9f, 0.95f);

        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.15f), size * 0.34f, body, body, 0);
        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.64f), size * 0.17f, body, body, 0);
    }

    public static void DrawPawGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color pad = new Color(0.95f, 0.93f, 0.85f);

        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.28f), size * 0.26f, pad, pad, 0);
        FillCircle(canvas, new Vector2(size * 0.26f, size * 0.62f), size * 0.13f, pad, pad, 0);
        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.72f), size * 0.14f, pad, pad, 0);
        FillCircle(canvas, new Vector2(size * 0.74f, size * 0.62f), size * 0.13f, pad, pad, 0);
    }

    public static void DrawFangGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color fang = new Color(0.95f, 0.93f, 0.85f);

        FillTriangleDown(canvas, new RectInt((int)(size * 0.18f), (int)(size * 0.3f), (int)(size * 0.26f), (int)(size * 0.42f)), fang);
        FillTriangleDown(canvas, new RectInt((int)(size * 0.56f), (int)(size * 0.3f), (int)(size * 0.26f), (int)(size * 0.42f)), fang);
    }

    public static void DrawSproutGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color stem = new Color(0.35f, 0.55f, 0.25f);
        Color leaf = new Color(0.45f, 0.7f, 0.3f);

        FillRect(canvas, new RectInt((int)(size * 0.47f), (int)(size * 0.1f), Mathf.Max(2, size / 32), (int)(size * 0.35f)), stem);
        FillCircle(canvas, new Vector2(size * 0.34f, size * 0.42f), size * 0.16f, leaf, Color.clear, 0);
        FillCircle(canvas, new Vector2(size * 0.64f, size * 0.5f), size * 0.14f, leaf, Color.clear, 0);
    }

    public static void DrawStarGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color star = new Color(1f, 0.85f, 0.3f);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        FillDiamond(canvas, center, size * 0.38f, star, star, 0);
        FillCircle(canvas, center, size * 0.13f, Color.Lerp(star, Color.white, 0.5f), Color.clear, 0);
    }

    public static void DrawHourglassGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color glass = new Color(0.85f, 0.7f, 0.4f);
        Color frame = new Color(0.4f, 0.28f, 0.15f);

        FillTriangleDown(canvas, new RectInt((int)(size * 0.28f), (int)(size * 0.5f), (int)(size * 0.44f), (int)(size * 0.36f)), glass);
        FillTriangleUp(canvas, new RectInt((int)(size * 0.28f), (int)(size * 0.14f), (int)(size * 0.44f), (int)(size * 0.36f)), glass);
        FillRect(canvas, new RectInt((int)(size * 0.24f), (int)(size * 0.12f), (int)(size * 0.52f), Mathf.Max(2, size / 24)), frame);
        FillRect(canvas, new RectInt((int)(size * 0.24f), (int)(size * 0.86f), (int)(size * 0.52f), Mathf.Max(2, size / 24)), frame);
    }

    public static void DrawGearGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color tool = new Color(0.85f, 0.85f, 0.9f);
        Color accent = new Color(0.5f, 0.3f, 0.55f);

        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.5f), size * 0.28f, tool, tool, 0);
        FillRect(canvas, new RectInt((int)(size * 0.44f), (int)(size * 0.05f), (int)(size * 0.12f), (int)(size * 0.16f)), tool);
        FillRect(canvas, new RectInt((int)(size * 0.44f), (int)(size * 0.79f), (int)(size * 0.12f), (int)(size * 0.16f)), tool);
        FillRect(canvas, new RectInt((int)(size * 0.05f), (int)(size * 0.44f), (int)(size * 0.16f), (int)(size * 0.12f)), tool);
        FillRect(canvas, new RectInt((int)(size * 0.79f), (int)(size * 0.44f), (int)(size * 0.16f), (int)(size * 0.12f)), tool);
        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.5f), size * 0.12f, accent, accent, 0);
    }

    public static void DrawSpeakerGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color body = new Color(0.92f, 0.92f, 0.95f);
        Color wave = new Color(0.55f, 0.85f, 0.95f);

        FillRect(canvas, new RectInt((int)(size * 0.14f), (int)(size * 0.38f), (int)(size * 0.16f), (int)(size * 0.24f)), body);
        FillSpeakerCone(canvas, size, body);
        FillCircle(canvas, new Vector2(size * 0.62f, size * 0.5f), size * 0.14f, Color.clear, wave, Mathf.Max(2, size / 24));
        FillCircle(canvas, new Vector2(size * 0.62f, size * 0.5f), size * 0.26f, Color.clear, wave, Mathf.Max(2, size / 24));
    }

    private static void FillSpeakerCone(PixelBuffer canvas, int size, Color fill)
    {
        int xMin = (int)(size * 0.3f);
        int xMax = (int)(size * 0.5f);
        int centerY = (int)(size * 0.5f);
        int halfHeightAtLeft = (int)(size * 0.12f);
        int halfHeightAtRight = (int)(size * 0.28f);

        for (int x = xMin; x <= xMax; x++)
        {
            float t = (x - xMin) / (float)Mathf.Max(1, xMax - xMin);
            int halfHeight = Mathf.RoundToInt(Mathf.Lerp(halfHeightAtLeft, halfHeightAtRight, t));

            for (int y = centerY - halfHeight; y <= centerY + halfHeight; y++)
            {
                canvas.Blend(x, y, fill);
            }
        }
    }

    public static void DrawPlayGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color fill = Color.white;

        FillTriangleRight(canvas, new RectInt((int)(size * 0.28f), (int)(size * 0.2f), (int)(size * 0.44f), (int)(size * 0.6f)), fill);
    }

    public static void DrawExitGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color line = Color.white;
        int thickness = Mathf.Max(2, size / 16);

        FillRect(canvas, new RectInt((int)(size * 0.22f), (int)(size * 0.2f), thickness, (int)(size * 0.6f)), line);
        FillRect(canvas, new RectInt((int)(size * 0.22f), (int)(size * 0.44f), (int)(size * 0.5f), thickness), line);
        FillTriangleRight(canvas, new RectInt((int)(size * 0.58f), (int)(size * 0.32f), (int)(size * 0.24f), (int)(size * 0.36f)), line);
    }

    public static void DrawTerritoryGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color fill = new Color(0.55f, 0.75f, 0.4f);
        Color border = new Color(0.3f, 0.45f, 0.22f);

        FillRect(canvas, new RectInt((int)(size * 0.18f), (int)(size * 0.18f), (int)(size * 0.64f), (int)(size * 0.64f)), fill);
        FillRect(canvas, new RectInt((int)(size * 0.18f), (int)(size * 0.18f), (int)(size * 0.64f), Mathf.Max(2, size / 16)), border);
        FillRect(canvas, new RectInt((int)(size * 0.18f), (int)(size * 0.18f), Mathf.Max(2, size / 16), (int)(size * 0.64f)), border);
    }

    public static Sprite CreateVegetationSprite(int size, VegetationDefinition.PlantSilhouette silhouette, Color foliageColor, Color trunkColor, int seed)
    {
        PixelBuffer buffer = new PixelBuffer(size, size);
        System.Random rng = new System.Random(seed);

        FillEllipse(buffer, new Vector2(size * 0.5f, size * 0.1f), size * 0.26f, size * 0.075f, new Color(0f, 0f, 0f, 0.32f), Color.clear, 0);

        switch (silhouette)
        {
            case VegetationDefinition.PlantSilhouette.Conifer:
                DrawConifer(buffer, size, foliageColor, trunkColor);
                break;
            case VegetationDefinition.PlantSilhouette.WeepingTree:
                DrawWeepingTree(buffer, size, foliageColor, trunkColor, rng);
                break;
            case VegetationDefinition.PlantSilhouette.Shrub:
                DrawShrub(buffer, size, foliageColor, rng);
                break;
            case VegetationDefinition.PlantSilhouette.StalkCluster:
                DrawStalkCluster(buffer, size, foliageColor, trunkColor, rng);
                break;
            case VegetationDefinition.PlantSilhouette.Fern:
                DrawFern(buffer, size, foliageColor);
                break;
            case VegetationDefinition.PlantSilhouette.Flower:
                DrawFlower(buffer, size, foliageColor, trunkColor);
                break;
            case VegetationDefinition.PlantSilhouette.RootedTree:
                DrawRootedTree(buffer, size, foliageColor, trunkColor, rng);
                break;
            default:
                DrawBroadleafTree(buffer, size, foliageColor, trunkColor, rng);
                break;
        }

        return ToSprite(buffer.ToTexture());
    }

    private static Vector2 RandomOffset(System.Random rng, float spread)
    {
        float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
        float dist = (float)rng.NextDouble() * spread * 0.35f;
        return new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
    }

    private static void DrawFoliageCluster(PixelBuffer canvas, Vector2 center, float spread, int blobCount, Color foliageColor, System.Random rng)
    {
        Color shadeColor = foliageColor * 0.54f;
        shadeColor.a = 1f;
        Color highlightColor = Color.Lerp(foliageColor, Color.white, 0.42f);

        for (int i = 0; i < blobCount; i++)
        {
            Vector2 offset = RandomOffset(rng, spread) + new Vector2(spread * 0.14f, -spread * 0.12f);
            float radius = spread * (0.45f + (float)rng.NextDouble() * 0.25f);
            FillCircle(canvas, center + offset, radius, shadeColor, shadeColor, 0);
        }

        for (int i = 0; i < blobCount; i++)
        {
            Vector2 offset = RandomOffset(rng, spread);
            float radius = spread * (0.5f + (float)rng.NextDouble() * 0.3f);
            FillCircle(canvas, center + offset, radius, foliageColor, foliageColor, 0);
        }

        FillCircle(canvas, center + new Vector2(-spread * 0.28f, spread * 0.24f), spread * 0.3f, highlightColor, Color.clear, 0);
    }

    private static void DrawBroadleafTree(PixelBuffer canvas, int size, Color foliageColor, Color trunkColor, System.Random rng)
    {
        FillRect(canvas, new RectInt((int)(size * 0.45f), (int)(size * 0.14f), (int)(size * 0.1f), (int)(size * 0.36f)), trunkColor);
        DrawFoliageCluster(canvas, new Vector2(size * 0.5f, size * 0.62f), size * 0.32f, 5, foliageColor, rng);
    }

    private static void DrawConifer(PixelBuffer canvas, int size, Color foliageColor, Color trunkColor)
    {
        FillRect(canvas, new RectInt((int)(size * 0.46f), (int)(size * 0.1f), (int)(size * 0.08f), (int)(size * 0.2f)), trunkColor);

        Color darkTier = foliageColor * 0.64f;
        darkTier.a = 1f;
        Color lightTier = Color.Lerp(foliageColor, Color.white, 0.28f);

        FillTriangleUp(canvas, new RectInt((int)(size * 0.18f), (int)(size * 0.22f), (int)(size * 0.64f), (int)(size * 0.26f)), darkTier);
        FillTriangleUp(canvas, new RectInt((int)(size * 0.24f), (int)(size * 0.42f), (int)(size * 0.52f), (int)(size * 0.26f)), foliageColor);
        FillTriangleUp(canvas, new RectInt((int)(size * 0.32f), (int)(size * 0.6f), (int)(size * 0.36f), (int)(size * 0.24f)), lightTier);
    }

    private static void DrawWeepingTree(PixelBuffer canvas, int size, Color foliageColor, Color trunkColor, System.Random rng)
    {
        FillRect(canvas, new RectInt((int)(size * 0.46f), (int)(size * 0.2f), (int)(size * 0.09f), (int)(size * 0.28f)), trunkColor);
        DrawFoliageCluster(canvas, new Vector2(size * 0.5f, size * 0.68f), size * 0.3f, 4, foliageColor, rng);

        Color frondColor = foliageColor * 0.85f;
        frondColor.a = 1f;
        int frondCount = 7;
        for (int i = 0; i < frondCount; i++)
        {
            float t = i / (float)(frondCount - 1);
            float edgeFactor = 1f - Mathf.Abs(t - 0.5f) * 2f;
            float x = Mathf.Lerp(size * 0.26f, size * 0.74f, t);
            float topY = size * 0.62f;
            float bottomY = Mathf.Lerp(size * 0.44f, size * 0.16f, edgeFactor);
            Vector2 top = new Vector2(x, topY);
            Vector2 bottom = new Vector2(Mathf.Lerp(x, size * 0.5f, 0.25f), bottomY);
            DrawFrondLine(canvas, top, bottom, Mathf.Max(2f, size / 48f), frondColor);
        }
    }

    private static void DrawShrub(PixelBuffer canvas, int size, Color foliageColor, System.Random rng)
    {
        DrawFoliageCluster(canvas, new Vector2(size * 0.5f, size * 0.3f), size * 0.34f, 5, foliageColor, rng);

        Color berryColor = new Color(0.75f, 0.15f, 0.25f);
        for (int i = 0; i < 6; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
            float dist = size * 0.2f * (float)rng.NextDouble();
            Vector2 pos = new Vector2(size * 0.5f + Mathf.Cos(angle) * dist, size * 0.3f + Mathf.Sin(angle) * dist);
            FillCircle(canvas, pos, size * 0.035f, berryColor, berryColor, 0);
        }
    }

    private static void DrawStalkCluster(PixelBuffer canvas, int size, Color foliageColor, Color trunkColor, System.Random rng)
    {
        const int stalkCount = 5;
        for (int i = 0; i < stalkCount; i++)
        {
            float t = i / (float)(stalkCount - 1);
            float x = Mathf.Lerp(size * 0.3f, size * 0.7f, t);
            float heightFactor = 1f - Mathf.Abs(t - 0.5f) * 0.5f + (float)(rng.NextDouble() * 0.14 - 0.07);
            float stalkHeight = size * 0.55f * heightFactor;

            FillRect(canvas, new RectInt((int)x - 1, (int)(size * 0.1f), Mathf.Max(2, size / 40), (int)stalkHeight), trunkColor);
            FillCircle(canvas, new Vector2(x, size * 0.1f + stalkHeight), size * 0.09f, foliageColor, foliageColor, 0);
        }
    }

    private static void DrawFern(PixelBuffer canvas, int size, Color foliageColor)
    {
        Vector2 basePos = new Vector2(size * 0.5f, size * 0.12f);
        const int frondCount = 6;

        for (int i = 0; i < frondCount; i++)
        {
            float t = i / (float)(frondCount - 1);
            float angle = Mathf.Lerp(25f, 155f, t) * Mathf.Deg2Rad;
            float length = size * (0.3f + 0.06f * Mathf.Sin(t * Mathf.PI));
            Vector2 tip = basePos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
            DrawFrondLine(canvas, basePos, tip, Mathf.Max(2f, size / 44f), foliageColor);
        }
    }

    private static void DrawFlower(PixelBuffer canvas, int size, Color foliageColor, Color trunkColor)
    {
        FillRect(canvas, new RectInt((int)(size * 0.48f), (int)(size * 0.1f), Mathf.Max(2, size / 32), (int)(size * 0.26f)), trunkColor);
        FillCircle(canvas, new Vector2(size * 0.4f, size * 0.2f), size * 0.08f, trunkColor, Color.clear, 0);
        FillCircle(canvas, new Vector2(size * 0.6f, size * 0.24f), size * 0.07f, trunkColor, Color.clear, 0);

        Vector2 flowerCenter = new Vector2(size * 0.5f, size * 0.42f);
        Color centerColor = new Color(0.95f, 0.85f, 0.3f);
        for (int i = 0; i < 5; i++)
        {
            float angle = i / 5f * Mathf.PI * 2f;
            Vector2 petalPos = flowerCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * 0.09f;
            FillCircle(canvas, petalPos, size * 0.075f, foliageColor, Color.clear, 0);
        }

        FillCircle(canvas, flowerCenter, size * 0.06f, centerColor, Color.clear, 0);
    }

    private static void DrawRootedTree(PixelBuffer canvas, int size, Color foliageColor, Color trunkColor, System.Random rng)
    {
        Vector2 trunkBase = new Vector2(size * 0.5f, size * 0.32f);
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            float x = Mathf.Lerp(size * 0.3f, size * 0.7f, t);
            DrawFrondLine(canvas, new Vector2(x, size * 0.08f), trunkBase, Mathf.Max(2f, size / 26f), trunkColor);
        }

        FillRect(canvas, new RectInt((int)(size * 0.45f), (int)(size * 0.28f), (int)(size * 0.1f), (int)(size * 0.22f)), trunkColor);
        DrawFoliageCluster(canvas, new Vector2(size * 0.5f, size * 0.66f), size * 0.3f, 5, foliageColor, rng);
    }

    public enum AnimalEarStyle { Round, Pointed, Long, Flappy }
    public enum AnimalOrnamentStyle { None, ThinHorns, CurvedHorns, Antlers, Tusks }
    public enum AnimalTailStyle { Nub, Bushy, Tuft, Long }
    public enum AnimalPattern { None, Stripes, Spots }

    // One flexible quadruped generator replaces what used to be several near-duplicate
    // per-species/per-category methods (Gazelle, Wolf, generic herbivore, generic predator).
    // Every species gets its own combination of traits instead of two shared silhouettes, and —
    // since leg drawing now takes a walkPhase — every species gets a 2-frame walk cycle for free.
    public struct QuadrupedTraits
    {
        public Color bodyColor;
        public AnimalEarStyle earStyle;
        public AnimalOrnamentStyle ornamentStyle;
        public AnimalTailStyle tailStyle;
        public bool hasMane;
        public bool hasFangs;
        public AnimalPattern pattern;
        public Color patternColor;
    }

    public static Sprite CreateQuadrupedSprite(int size, QuadrupedTraits traits, int walkPhase)
    {
        PixelBuffer canvas = new PixelBuffer(size, size);

        Color bodyColor = traits.bodyColor;
        Color shadeColor = bodyColor * 0.6f;
        shadeColor.a = 1f;
        Color highlightColor = Color.Lerp(bodyColor, Color.white, 0.35f);
        Color legColor = bodyColor * 0.55f;
        legColor.a = 1f;

        FillEllipse(canvas, new Vector2(size * 0.5f, size * 0.1f), size * 0.34f, size * 0.08f, new Color(0f, 0f, 0f, 0.3f), Color.clear, 0);

        DrawQuadrupedLegs(canvas, size, legColor, walkPhase);
        DrawAnimalTail(canvas, size, traits.tailStyle, bodyColor, highlightColor);

        FillEllipse(canvas, new Vector2(size * 0.5f, size * 0.4f), size * 0.3f, size * 0.19f, shadeColor, Color.clear, 0);
        FillEllipse(canvas, new Vector2(size * 0.46f, size * 0.44f), size * 0.28f, size * 0.17f, bodyColor, Color.clear, 0);
        FillEllipse(canvas, new Vector2(size * 0.34f, size * 0.5f), size * 0.11f, size * 0.07f, highlightColor, Color.clear, 0);

        if (traits.hasMane)
        {
            FillEllipse(canvas, new Vector2(size * 0.66f, size * 0.5f), size * 0.2f, size * 0.19f, shadeColor, Color.clear, 0);
        }

        FillCircle(canvas, new Vector2(size * 0.76f, size * 0.52f), size * 0.15f, bodyColor, Color.clear, 0);
        FillCircle(canvas, new Vector2(size * 0.71f, size * 0.57f), size * 0.055f, highlightColor, Color.clear, 0);

        DrawAnimalEars(canvas, size, traits.earStyle, bodyColor);
        DrawAnimalOrnament(canvas, size, traits.ornamentStyle);

        if (traits.hasFangs)
        {
            Color fangColor = new Color(0.96f, 0.94f, 0.88f);
            FillTriangleDown(canvas, new RectInt((int)(size * 0.85f), (int)(size * 0.42f), (int)(size * 0.032f), (int)(size * 0.055f)), fangColor);
            FillTriangleDown(canvas, new RectInt((int)(size * 0.9f), (int)(size * 0.42f), (int)(size * 0.032f), (int)(size * 0.055f)), fangColor);
        }

        FillCircle(canvas, new Vector2(size * 0.89f, size * 0.49f), size * 0.05f, bodyColor, Color.clear, 0);

        if (traits.pattern != AnimalPattern.None)
        {
            DrawAnimalPattern(canvas, size, traits.pattern, traits.patternColor);
        }

        Color eyeColor = new Color(0.1f, 0.1f, 0.1f);
        FillCircle(canvas, new Vector2(size * 0.8f, size * 0.55f), size * 0.026f, eyeColor, eyeColor, 0);

        return ToSprite(canvas.ToTexture());
    }

    // walkPhase alternates which diagonal-ish leg pair reads as "lifted" (shorter, raised) each
    // frame — a classic 2-frame chunky-pixel walk cycle without needing separate leg GameObjects.
    private static void DrawQuadrupedLegs(PixelBuffer canvas, int size, Color legColor, int walkPhase)
    {
        float[] legX = { 0.28f, 0.42f, 0.58f, 0.72f };
        float legWidth = size * 0.06f;
        float legHeight = size * 0.2f;
        float legTop = size * 0.12f;

        for (int i = 0; i < legX.Length; i++)
        {
            bool lifted = (i % 2 == 0) == (walkPhase == 0);
            float height = lifted ? legHeight * 0.68f : legHeight;
            float top = lifted ? legTop + legHeight * 0.32f : legTop;
            FillRect(canvas, new RectInt((int)(size * legX[i]), (int)top, (int)legWidth, (int)height), legColor);
        }
    }

    private static void DrawAnimalTail(PixelBuffer canvas, int size, AnimalTailStyle style, Color bodyColor, Color highlightColor)
    {
        switch (style)
        {
            case AnimalTailStyle.Bushy:
                FillTriangleDown(canvas, new RectInt((int)(size * 0.05f), (int)(size * 0.3f), (int)(size * 0.16f), (int)(size * 0.2f)), bodyColor);
                FillCircle(canvas, new Vector2(size * 0.1f, size * 0.28f), size * 0.06f, highlightColor, Color.clear, 0);
                break;
            case AnimalTailStyle.Tuft:
                FillRect(canvas, new RectInt((int)(size * 0.08f), (int)(size * 0.36f), (int)(size * 0.04f), (int)(size * 0.16f)), bodyColor);
                FillCircle(canvas, new Vector2(size * 0.1f, size * 0.34f), size * 0.045f, bodyColor, Color.clear, 0);
                break;
            case AnimalTailStyle.Long:
                FillRect(canvas, new RectInt((int)(size * 0.06f), (int)(size * 0.4f), (int)(size * 0.035f), (int)(size * 0.16f)), bodyColor);
                break;
            case AnimalTailStyle.Nub:
            default:
                FillTriangleDown(canvas, new RectInt((int)(size * 0.2f), (int)(size * 0.4f), (int)(size * 0.05f), (int)(size * 0.1f)), bodyColor);
                break;
        }
    }

    private static void DrawAnimalEars(PixelBuffer canvas, int size, AnimalEarStyle style, Color bodyColor)
    {
        switch (style)
        {
            case AnimalEarStyle.Long:
                FillEllipse(canvas, new Vector2(size * 0.74f, size * 0.72f), size * 0.035f, size * 0.13f, bodyColor, Color.clear, 0);
                FillEllipse(canvas, new Vector2(size * 0.83f, size * 0.72f), size * 0.035f, size * 0.13f, bodyColor, Color.clear, 0);
                break;
            case AnimalEarStyle.Pointed:
                FillTriangleUp(canvas, new RectInt((int)(size * 0.7f), (int)(size * 0.62f), (int)(size * 0.06f), (int)(size * 0.13f)), bodyColor);
                FillTriangleUp(canvas, new RectInt((int)(size * 0.8f), (int)(size * 0.62f), (int)(size * 0.06f), (int)(size * 0.13f)), bodyColor);
                break;
            case AnimalEarStyle.Flappy:
                FillCircle(canvas, new Vector2(size * 0.68f, size * 0.52f), size * 0.09f, bodyColor, Color.clear, 0);
                FillCircle(canvas, new Vector2(size * 0.84f, size * 0.52f), size * 0.09f, bodyColor, Color.clear, 0);
                break;
            case AnimalEarStyle.Round:
            default:
                FillCircle(canvas, new Vector2(size * 0.73f, size * 0.66f), size * 0.045f, bodyColor, Color.clear, 0);
                FillCircle(canvas, new Vector2(size * 0.82f, size * 0.66f), size * 0.045f, bodyColor, Color.clear, 0);
                break;
        }
    }

    private static void DrawAnimalOrnament(PixelBuffer canvas, int size, AnimalOrnamentStyle style)
    {
        Color hornColor = new Color(0.35f, 0.3f, 0.22f);
        Color tuskColor = new Color(0.94f, 0.92f, 0.85f);

        switch (style)
        {
            case AnimalOrnamentStyle.ThinHorns:
                FillRect(canvas, new RectInt((int)(size * 0.75f), (int)(size * 0.62f), (int)(size * 0.022f), (int)(size * 0.15f)), hornColor);
                FillRect(canvas, new RectInt((int)(size * 0.81f), (int)(size * 0.62f), (int)(size * 0.022f), (int)(size * 0.15f)), hornColor);
                break;
            case AnimalOrnamentStyle.CurvedHorns:
                FillRect(canvas, new RectInt((int)(size * 0.74f), (int)(size * 0.6f), (int)(size * 0.03f), (int)(size * 0.14f)), hornColor);
                FillRect(canvas, new RectInt((int)(size * 0.71f), (int)(size * 0.56f), (int)(size * 0.05f), (int)(size * 0.05f)), hornColor);
                FillRect(canvas, new RectInt((int)(size * 0.83f), (int)(size * 0.6f), (int)(size * 0.03f), (int)(size * 0.14f)), hornColor);
                FillRect(canvas, new RectInt((int)(size * 0.83f), (int)(size * 0.56f), (int)(size * 0.05f), (int)(size * 0.05f)), hornColor);
                break;
            case AnimalOrnamentStyle.Antlers:
                FillRect(canvas, new RectInt((int)(size * 0.75f), (int)(size * 0.63f), (int)(size * 0.02f), (int)(size * 0.16f)), hornColor);
                FillRect(canvas, new RectInt((int)(size * 0.71f), (int)(size * 0.68f), (int)(size * 0.05f), (int)(size * 0.02f)), hornColor);
                FillRect(canvas, new RectInt((int)(size * 0.82f), (int)(size * 0.63f), (int)(size * 0.02f), (int)(size * 0.16f)), hornColor);
                FillRect(canvas, new RectInt((int)(size * 0.82f), (int)(size * 0.68f), (int)(size * 0.06f), (int)(size * 0.02f)), hornColor);
                break;
            case AnimalOrnamentStyle.Tusks:
                FillRect(canvas, new RectInt((int)(size * 0.9f), (int)(size * 0.5f), (int)(size * 0.025f), (int)(size * 0.09f)), tuskColor);
                FillRect(canvas, new RectInt((int)(size * 0.94f), (int)(size * 0.48f), (int)(size * 0.025f), (int)(size * 0.09f)), tuskColor);
                break;
            case AnimalOrnamentStyle.None:
            default:
                break;
        }
    }

    private static void DrawAnimalPattern(PixelBuffer canvas, int size, AnimalPattern pattern, Color patternColor)
    {
        if (pattern == AnimalPattern.Stripes)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = size * (0.3f + i * 0.08f);
                FillRect(canvas, new RectInt((int)x, (int)(size * 0.3f), Mathf.Max(1, size / 32), (int)(size * 0.22f)), patternColor);
            }
        }
        else if (pattern == AnimalPattern.Spots)
        {
            FillCircle(canvas, new Vector2(size * 0.4f, size * 0.36f), size * 0.035f, patternColor, Color.clear, 0);
            FillCircle(canvas, new Vector2(size * 0.5f, size * 0.46f), size * 0.03f, patternColor, Color.clear, 0);
            FillCircle(canvas, new Vector2(size * 0.6f, size * 0.35f), size * 0.032f, patternColor, Color.clear, 0);
            FillCircle(canvas, new Vector2(size * 0.44f, size * 0.5f), size * 0.025f, patternColor, Color.clear, 0);
        }
    }

    // Birds don't fit the quadruped template at all — separate silhouette, with wing-flap frames
    // standing in for the walk cycle.
    public static Sprite CreateBirdSprite(int size, Color bodyColor, int wingPhase)
    {
        PixelBuffer canvas = new PixelBuffer(size, size);

        Color shadeColor = bodyColor * 0.6f;
        shadeColor.a = 1f;
        Color highlightColor = Color.Lerp(bodyColor, Color.white, 0.35f);
        Color beakColor = new Color(0.85f, 0.6f, 0.15f);

        FillEllipse(canvas, new Vector2(size * 0.5f, size * 0.1f), size * 0.3f, size * 0.06f, new Color(0f, 0f, 0f, 0.28f), Color.clear, 0);

        FillRect(canvas, new RectInt((int)(size * 0.46f), (int)(size * 0.14f), (int)(size * 0.03f), (int)(size * 0.12f)), shadeColor);
        FillRect(canvas, new RectInt((int)(size * 0.54f), (int)(size * 0.14f), (int)(size * 0.03f), (int)(size * 0.12f)), shadeColor);

        float wingLift = wingPhase == 0 ? size * 0.14f : -size * 0.04f;
        FillTriangleDown(canvas, new RectInt((int)(size * 0.28f), (int)(size * 0.34f - wingLift), (int)(size * 0.24f), (int)(size * 0.16f)), shadeColor);

        FillEllipse(canvas, new Vector2(size * 0.5f, size * 0.36f), size * 0.24f, size * 0.17f, shadeColor, Color.clear, 0);
        FillEllipse(canvas, new Vector2(size * 0.5f, size * 0.4f), size * 0.22f, size * 0.15f, bodyColor, Color.clear, 0);

        float otherWingLift = wingPhase == 1 ? size * 0.14f : -size * 0.04f;
        FillTriangleDown(canvas, new RectInt((int)(size * 0.5f), (int)(size * 0.34f - otherWingLift), (int)(size * 0.24f), (int)(size * 0.16f)), bodyColor);

        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.62f), size * 0.14f, bodyColor, Color.clear, 0);
        FillCircle(canvas, new Vector2(size * 0.45f, size * 0.66f), size * 0.05f, highlightColor, Color.clear, 0);

        FillTriangleUp(canvas, new RectInt((int)(size * 0.46f), (int)(size * 0.72f), (int)(size * 0.08f), (int)(size * 0.09f)), beakColor);

        Color eyeColor = new Color(0.1f, 0.1f, 0.1f);
        FillCircle(canvas, new Vector2(size * 0.54f, size * 0.66f), size * 0.024f, eyeColor, eyeColor, 0);

        return ToSprite(canvas.ToTexture());
    }

    public static Sprite CreateLabBuildingSprite(int size)
    {
        PixelBuffer canvas = new PixelBuffer(size, size);

        Color wall = new Color(0.85f, 0.89f, 0.89f);
        Color wallShade = new Color(0.72f, 0.78f, 0.78f);
        Color trim = new Color(0.16f, 0.42f, 0.45f);
        Color trimDark = new Color(0.06f, 0.22f, 0.24f);
        Color glass = new Color(0.35f, 0.85f, 0.85f);
        Color glassLight = new Color(0.75f, 0.98f, 0.98f);
        Color door = new Color(0.1f, 0.28f, 0.3f);

        FillEllipse(canvas, new Vector2(size * 0.5f, size * 0.06f), size * 0.36f, size * 0.09f, new Color(0f, 0f, 0f, 0.32f), Color.clear, 0);

        FillRoundedRectRegion(canvas, new RectInt((int)(size * 0.1f), (int)(size * 0.12f), (int)(size * 0.8f), (int)(size * 0.56f)), size * 0.06f, wall, trimDark, Mathf.Max(2, size / 32));
        FillRect(canvas, new RectInt((int)(size * 0.72f), (int)(size * 0.12f), (int)(size * 0.18f), (int)(size * 0.56f)), wallShade);

        FillRoundedRectRegion(canvas, new RectInt((int)(size * 0.06f), (int)(size * 0.62f), (int)(size * 0.88f), (int)(size * 0.14f)), size * 0.05f, trim, trimDark, Mathf.Max(2, size / 32));
        FillRect(canvas, new RectInt((int)(size * 0.24f), (int)(size * 0.7f), (int)(size * 0.07f), (int)(size * 0.14f)), trimDark);
        FillRect(canvas, new RectInt((int)(size * 0.66f), (int)(size * 0.7f), (int)(size * 0.07f), (int)(size * 0.16f)), trimDark);
        FillCircle(canvas, new Vector2(size * 0.275f, size * 0.86f), size * 0.045f, trim, trim, 0);
        FillCircle(canvas, new Vector2(size * 0.695f, size * 0.88f), size * 0.045f, trim, trim, 0);

        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.44f), size * 0.16f, glass, trimDark, Mathf.Max(2, size / 40));
        FillCircle(canvas, new Vector2(size * 0.44f, size * 0.5f), size * 0.05f, glassLight, Color.clear, 0);

        FillRoundedRectRegion(canvas, new RectInt((int)(size * 0.4f), (int)(size * 0.12f), (int)(size * 0.2f), (int)(size * 0.2f)), size * 0.04f, door, trimDark, Mathf.Max(2, size / 40));

        return ToSprite(canvas.ToTexture());
    }
}
