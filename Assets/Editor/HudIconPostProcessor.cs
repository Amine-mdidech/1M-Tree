using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// One-off (but reusable) post-processing pipeline for fal.ai-generated icon art, per the
// background-removal recipe documented in docs/art-design-document.md: flood-fill from the
// corners (follows a gradient/shadow smoothly, unlike a fixed color-distance threshold), then
// keep only the largest connected component to strip stray specks, then crop/pad/resize.
public static class HudIconPostProcessor
{
    public static void ProcessIcon(string sourceAssetPath, string finalAssetPath, int finalSize)
    {
        string sourceFullPath = Path.Combine(Directory.GetCurrentDirectory(), sourceAssetPath);
        byte[] bytes = File.ReadAllBytes(sourceFullPath);

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(bytes);

        Color[] pixels = source.GetPixels();
        int width = source.width;
        int height = source.height;
        bool[] isBackground = new bool[pixels.Length];

        FloodFillBackground(pixels, width, height, isBackground);
        KeepLargestForegroundComponent(pixels, width, height, isBackground);

        // Zeroing color+alpha together (not just alpha) here matters: the crop/resize below does
        // bilinear blending, and an alpha=0 pixel that still carries the old magenta/background
        // RGB would blend a visible magenta fringe into the resized edge pixels even though it's
        // fully transparent at native resolution. Premultiplied-style zeroing avoids that.
        for (int i = 0; i < pixels.Length; i++)
        {
            if (isBackground[i])
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            }
        }

        RectInt bounds = ComputeOpaqueBounds(pixels, width, height);
        RectInt padded = PadAndSquare(bounds, width, height);

        Color[] cropped = new Color[padded.width * padded.height];
        for (int y = 0; y < padded.height; y++)
        {
            int srcY = padded.y + y;
            for (int x = 0; x < padded.width; x++)
            {
                int srcX = padded.x + x;
                bool inBounds = srcX >= 0 && srcX < width && srcY >= 0 && srcY < height;
                cropped[y * padded.width + x] = inBounds ? pixels[srcY * width + srcX] : Color.clear;
            }
        }

        Color[] resized = ResizeBilinear(cropped, padded.width, padded.height, finalSize, finalSize);

        // Un-premultiply: the crop/resize pipeline works in premultiplied color (background is
        // true black at alpha 0, so edge blending never mixes in stray background hue), but
        // Texture2D/PNG storage and Unity's alphaIsTransparency import expect straight alpha.
        for (int i = 0; i < resized.Length; i++)
        {
            float a = resized[i].a;
            if (a > 0.001f)
            {
                resized[i] = new Color(resized[i].r / a, resized[i].g / a, resized[i].b / a, a);
            }
        }

        Texture2D result = new Texture2D(finalSize, finalSize, TextureFormat.RGBA32, false);
        result.SetPixels(resized);
        result.Apply();

        string finalFullPath = Path.Combine(Directory.GetCurrentDirectory(), finalAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(finalFullPath));
        File.WriteAllBytes(finalFullPath, result.EncodeToPNG());

        Object.DestroyImmediate(source);
        Object.DestroyImmediate(result);

        AssetDatabase.ImportAsset(finalAssetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(finalAssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    // Hue-based (not raw RGB-distance) classification: the magenta chroma key's cast shadow is
    // the SAME hue as the bright background but much darker/less saturated, so its RGB distance
    // from a bright corner sample is large even though it's obviously still "background" to the
    // eye. Hue is stable across that brightness swing, so comparing hue (with a saturation floor
    // to avoid near-white/near-black pixels, whose hue is numerically unstable, matching by
    // coincidence) is anchored the same way regardless of shadow/highlight lighting.
    private static void FloodFillBackground(Color[] pixels, int width, int height, bool[] isBackground)
    {
        const float hueThreshold = 0.09f;
        const float minSaturation = 0.03f;

        int[] corners = { 0, width - 1, (height - 1) * width, height * width - 1 };
        Color referenceRgb = Color.clear;
        foreach (int corner in corners)
        {
            referenceRgb += pixels[corner];
        }
        referenceRgb /= corners.Length;

        float referenceHue, referenceSat, referenceVal;
        Color.RGBToHSV(referenceRgb, out referenceHue, out referenceSat, out referenceVal);

        Queue<int> queue = new Queue<int>();
        foreach (int corner in corners)
        {
            if (!isBackground[corner])
            {
                isBackground[corner] = true;
                queue.Enqueue(corner);
            }
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;

            TryEnqueueNeighbor(pixels, isBackground, queue, width, height, x + 1, y, referenceHue, hueThreshold, minSaturation);
            TryEnqueueNeighbor(pixels, isBackground, queue, width, height, x - 1, y, referenceHue, hueThreshold, minSaturation);
            TryEnqueueNeighbor(pixels, isBackground, queue, width, height, x, y + 1, referenceHue, hueThreshold, minSaturation);
            TryEnqueueNeighbor(pixels, isBackground, queue, width, height, x, y - 1, referenceHue, hueThreshold, minSaturation);
        }
    }

    private static void TryEnqueueNeighbor(Color[] pixels, bool[] isBackground, Queue<int> queue, int width, int height, int x, int y, float referenceHue, float hueThreshold, float minSaturation)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int index = y * width + x;
        if (isBackground[index])
        {
            return;
        }

        float hue, sat, val;
        Color.RGBToHSV(pixels[index], out hue, out sat, out val);

        if (sat < minSaturation || CircularHueDistance(hue, referenceHue) > hueThreshold)
        {
            return;
        }

        isBackground[index] = true;
        queue.Enqueue(index);
    }

    // Hue wraps at 1.0 -> 0.0, so a plain subtraction would treat e.g. 0.98 and 0.02 (visually
    // 7 degrees apart) as 0.96 apart.
    private static float CircularHueDistance(float a, float b)
    {
        float diff = Mathf.Abs(a - b);
        return Mathf.Min(diff, 1f - diff);
    }

    private static void KeepLargestForegroundComponent(Color[] pixels, int width, int height, bool[] isBackground)
    {
        int[] componentId = new int[pixels.Length];
        for (int i = 0; i < componentId.Length; i++)
        {
            componentId[i] = -1;
        }

        List<int> componentSizes = new List<int>();
        Queue<int> queue = new Queue<int>();

        for (int start = 0; start < pixels.Length; start++)
        {
            if (isBackground[start] || componentId[start] != -1)
            {
                continue;
            }

            int id = componentSizes.Count;
            int size = 0;
            componentId[start] = id;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                size++;
                int x = index % width;
                int y = index / width;

                TryEnqueueComponent(isBackground, componentId, queue, width, height, x + 1, y, id);
                TryEnqueueComponent(isBackground, componentId, queue, width, height, x - 1, y, id);
                TryEnqueueComponent(isBackground, componentId, queue, width, height, x, y + 1, id);
                TryEnqueueComponent(isBackground, componentId, queue, width, height, x, y - 1, id);
            }

            componentSizes.Add(size);
        }

        int largestId = -1;
        int largestSize = -1;
        for (int i = 0; i < componentSizes.Count; i++)
        {
            if (componentSizes[i] > largestSize)
            {
                largestSize = componentSizes[i];
                largestId = i;
            }
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            if (!isBackground[i] && componentId[i] != largestId)
            {
                isBackground[i] = true;
            }
        }
    }

    private static void TryEnqueueComponent(bool[] isBackground, int[] componentId, Queue<int> queue, int width, int height, int x, int y, int id)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int index = y * width + x;
        if (isBackground[index] || componentId[index] != -1)
        {
            return;
        }

        componentId[index] = id;
        queue.Enqueue(index);
    }

    private static RectInt ComputeOpaqueBounds(Color[] pixels, int width, int height)
    {
        int minX = width, maxX = -1, minY = height, maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a > 0.5f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new RectInt(0, 0, width, height);
        }

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static RectInt PadAndSquare(RectInt bounds, int textureWidth, int textureHeight)
    {
        int padding = Mathf.RoundToInt(Mathf.Max(bounds.width, bounds.height) * 0.06f);
        int paddedWidth = bounds.width + padding * 2;
        int paddedHeight = bounds.height + padding * 2;
        int side = Mathf.Max(paddedWidth, paddedHeight);

        int centerX = bounds.x + bounds.width / 2;
        int centerY = bounds.y + bounds.height / 2;

        int x = centerX - side / 2;
        int y = centerY - side / 2;

        return new RectInt(x, y, side, side);
    }

    private static Color[] ResizeBilinear(Color[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        Color[] result = new Color[targetWidth * targetHeight];

        for (int ty = 0; ty < targetHeight; ty++)
        {
            float v = sourceHeight <= 1 ? 0f : ty / (float)(targetHeight - 1) * (sourceHeight - 1);
            int y0 = Mathf.FloorToInt(v);
            int y1 = Mathf.Min(y0 + 1, sourceHeight - 1);
            float fy = v - y0;

            for (int tx = 0; tx < targetWidth; tx++)
            {
                float u = sourceWidth <= 1 ? 0f : tx / (float)(targetWidth - 1) * (sourceWidth - 1);
                int x0 = Mathf.FloorToInt(u);
                int x1 = Mathf.Min(x0 + 1, sourceWidth - 1);
                float fx = u - x0;

                Color c00 = source[y0 * sourceWidth + x0];
                Color c10 = source[y0 * sourceWidth + x1];
                Color c01 = source[y1 * sourceWidth + x0];
                Color c11 = source[y1 * sourceWidth + x1];

                Color top = Color.Lerp(c00, c10, fx);
                Color bottom = Color.Lerp(c01, c11, fx);
                result[ty * targetWidth + tx] = Color.Lerp(top, bottom, fy);
            }
        }

        return result;
    }
}
