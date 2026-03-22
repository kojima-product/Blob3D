using UnityEngine;
using UnityEditor;
using System.IO;

public class IconGenerator : EditorWindow
{
    [MenuItem("Blob3D/Generate App Icons")]
    public static void GenerateIcons()
    {
        int[] sizes = { 180, 167, 152, 120, 76, 1024, 192, 144, 96, 72, 48, 512 };

        string dir = "Assets/Icons";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        foreach (int size in sizes)
        {
            Texture2D icon = CreateIcon(size);
            byte[] png = icon.EncodeToPNG();
            string path = $"{dir}/icon_{size}x{size}.png";
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(icon);
        }

        AssetDatabase.Refresh();
        Debug.Log("[Blob3D] App icons generated in Assets/Icons/");
    }

    private static Texture2D CreateIcon(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        // Background gradient (dark blue to teal)
        Color bgTop = new Color(0.1f, 0.15f, 0.3f);
        Color bgBottom = new Color(0.05f, 0.25f, 0.35f);

        // Blob color (player blue with glow)
        Color blobColor = new Color(0.2f, 0.7f, 1.0f);
        Color blobGlow = new Color(0.4f, 0.85f, 1.0f, 0.5f);

        float center = size * 0.5f;
        float blobRadius = size * 0.3f;
        float glowRadius = size * 0.4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = (float)y / size;
                Color bg = Color.Lerp(bgBottom, bgTop, t);

                // Distance from center
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Glow
                if (dist < glowRadius)
                {
                    float glowT = 1f - (dist / glowRadius);
                    bg = Color.Lerp(bg, blobGlow, glowT * 0.3f);
                }

                // Blob circle
                if (dist < blobRadius)
                {
                    float blobT = dist / blobRadius;
                    // Fresnel-like edge brightening
                    float edge = Mathf.Pow(blobT, 3f);
                    Color blob = Color.Lerp(blobColor * 0.7f, blobColor * 1.3f, edge);
                    // Subsurface warm center
                    float centerGlow = 1f - blobT;
                    blob = Color.Lerp(blob, new Color(1f, 0.5f, 0.2f, 1f), centerGlow * 0.15f);
                    bg = blob;
                }

                // Rounded corners (for iOS)
                float cornerRadius = size * 0.18f;
                float cornerDist = CornerDistance(x, y, size, cornerRadius);
                if (cornerDist > cornerRadius)
                {
                    bg.a = 0f;
                }

                tex.SetPixel(x, y, bg);
            }
        }

        tex.Apply();
        return tex;
    }

    private static float CornerDistance(int x, int y, int size, float r)
    {
        // Check each corner
        float[][] corners = {
            new float[] { r, r },
            new float[] { size - r, r },
            new float[] { r, size - r },
            new float[] { size - r, size - r }
        };

        foreach (var c in corners)
        {
            if ((x < c[0] && y < c[1]) || (x > c[0] && y < c[1] && c[0] == size - r) ||
                (x < c[0] && y > c[1] && c[1] == size - r) || (x > c[0] && y > c[1] && c[0] == size - r && c[1] == size - r))
            {
                float dx = x - c[0];
                float dy = y - c[1];
                return Mathf.Sqrt(dx * dx + dy * dy);
            }
        }
        return 0f; // Inside, not near corner
    }
}
