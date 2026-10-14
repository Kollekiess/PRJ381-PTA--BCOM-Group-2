using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

public static class SignTextureGenerator
{
    [MenuItem("Tools/Generate Remaining Sign Textures")]
    public static void GenerateAll()
    {
        string dir = "Assets/Textures/";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        GenerateSign(dir + "asl_sad.png", "SAD", 1);
        GenerateSign(dir + "asl_love.png", "LOVE", 2);
        GenerateSign(dir + "asl_who.png", "WHO", 3);
        GenerateSign(dir + "asl_what.png", "WHAT", 4);
        GenerateSign(dir + "asl_where.png", "WHERE", 5);
        GenerateSign(dir + "asl_today.png", "TODAY", 6);
        GenerateSign(dir + "asl_tomorrow.png", "TOMORROW", 7);
        GenerateSign(dir + "asl_good.png", "GOOD", 8);

        AssetDatabase.Refresh();
        Debug.Log("Generated all remaining sign textures!");
    }

    private static void GenerateSign(string path, string title, int type)
    {
        int w = 512, h = 512;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color bg = new Color(0.11f, 0.13f, 0.14f, 1f);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.03f - 0.015f;
                pixels[y * w + x] = new Color(bg.r + n, bg.g + n, bg.b + n, 1f);
            }
        }
        tex.SetPixels(pixels);

        Color chalk = new Color(0.92f, 0.94f, 0.96f, 0.95f);
        Color yellow = new Color(1f, 0.86f, 0.22f, 0.95f);

        // Border
        for (int i = 18; i < w - 18; i++)
        {
            tex.SetPixel(i, 18, chalk * 0.6f);
            tex.SetPixel(i, h - 18, chalk * 0.6f);
        }
        for (int j = 18; j < h - 18; j++)
        {
            tex.SetPixel(18, j, chalk * 0.6f);
            tex.SetPixel(w - 18, j, chalk * 0.6f);
        }

        // Draw Diagram based on sign type
        switch (type)
        {
            case 1: // SAD: Face with hands moving downward
                DrawCircle(tex, 256, 320, 60, chalk);
                DrawLine(tex, 256, 260, 256, 170, chalk, 5);
                DrawLine(tex, 200, 310, 195, 230, chalk, 4);
                DrawLine(tex, 312, 310, 317, 230, chalk, 4);
                DrawArrow(tex, 160, 300, 160, 210, yellow);
                DrawArrow(tex, 352, 300, 352, 210, yellow);
                break;
            case 2: // LOVE: Crossed arms hugging chest
                DrawCircle(tex, 256, 330, 55, chalk);
                DrawLine(tex, 256, 275, 256, 165, chalk, 5);
                DrawLine(tex, 190, 260, 315, 205, chalk, 6);
                DrawLine(tex, 322, 260, 197, 205, chalk, 6);
                DrawCircle(tex, 315, 205, 14, chalk, true);
                DrawCircle(tex, 197, 205, 14, chalk, true);
                DrawCircle(tex, 256, 235, 24, yellow, false);
                break;
            case 3: // WHO: Thumb on chin, index bends
                DrawCircle(tex, 256, 310, 65, chalk);
                DrawCircle(tex, 256, 245, 15, yellow, true);
                DrawLine(tex, 256, 245, 275, 285, chalk, 5);
                DrawArrow(tex, 290, 295, 265, 265, yellow);
                DrawLine(tex, 256, 230, 256, 155, chalk, 4);
                break;
            case 4: // WHAT: Open palms upward shaking
                DrawLine(tex, 150, 235, 210, 255, chalk, 6);
                DrawLine(tex, 362, 235, 302, 255, chalk, 6);
                DrawCircle(tex, 180, 245, 20, chalk, false);
                DrawCircle(tex, 332, 245, 20, chalk, false);
                DrawArrow(tex, 130, 280, 220, 280, yellow);
                DrawArrow(tex, 220, 260, 130, 260, yellow);
                DrawArrow(tex, 290, 280, 380, 280, yellow);
                DrawArrow(tex, 380, 260, 290, 260, yellow);
                break;
            case 5: // WHERE: Index finger pointing up and swaying
                DrawCircle(tex, 256, 195, 24, chalk, true);
                DrawLine(tex, 256, 195, 256, 325, chalk, 6);
                DrawCircle(tex, 256, 325, 7, chalk, true);
                DrawArrow(tex, 200, 345, 310, 345, yellow);
                DrawArrow(tex, 310, 315, 200, 315, yellow);
                break;
            case 6: // TODAY: Both Y-hands drop downward
                DrawLine(tex, 175, 270, 175, 215, chalk, 6);
                DrawLine(tex, 337, 270, 337, 215, chalk, 6);
                DrawCircle(tex, 175, 270, 14, chalk, true);
                DrawCircle(tex, 337, 270, 14, chalk, true);
                DrawArrow(tex, 175, 270, 175, 175, yellow);
                DrawArrow(tex, 337, 270, 337, 175, yellow);
                break;
            case 7: // TOMORROW: Thumb arcs forward from cheek
                DrawCircle(tex, 256, 295, 60, chalk);
                DrawCircle(tex, 220, 275, 15, chalk, true);
                DrawArrow(tex, 220, 265, 310, 235, yellow);
                DrawLine(tex, 256, 235, 256, 160, chalk, 4);
                break;
            case 8: // GOOD: Flat hand moves down into palm
                DrawCircle(tex, 256, 320, 55, chalk);
                DrawLine(tex, 256, 300, 256, 245, chalk, 5);
                DrawLine(tex, 215, 185, 297, 185, chalk, 6);
                DrawArrow(tex, 256, 245, 256, 200, yellow);
                break;
        }

        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void DrawCircle(Texture2D tex, int cx, int cy, int r, Color col, bool filled = false)
    {
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (filled ? d <= r : Mathf.Abs(d - r) <= 2.5f)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                    {
                        tex.SetPixel(px, py, col);
                    }
                }
            }
        }
    }

    private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col, int thickness = 3)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int x = x0, y = y0;
        int maxSteps = 1000;
        for (int step = 0; step < maxSteps; step++)
        {
            for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
            {
                for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                {
                    if (x + tx >= 0 && x + tx < tex.width && y + ty >= 0 && y + ty < tex.height)
                        tex.SetPixel(x + tx, y + ty, col);
                }
            }
            if (x == x1 && y == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x += sx; }
            if (e2 <= dx) { err += dx; y += sy; }
        }
    }

    private static void DrawArrow(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        DrawLine(tex, x0, y0, x1, y1, col, 4);
        Vector2 dir = new Vector2(x1 - x0, y1 - y0).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 head1 = new Vector2(x1, y1) - dir * 18f + perp * 10f;
        Vector2 head2 = new Vector2(x1, y1) - dir * 18f - perp * 10f;
        DrawLine(tex, x1, y1, (int)head1.x, (int)head1.y, col, 4);
        DrawLine(tex, x1, y1, (int)head2.x, (int)head2.y, col, 4);
    }
}
#endif
