using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RecreioEspacial.Core
{
    /// <summary>
    /// Carrega imagens e fontes de Assets/Resources.
    /// Os caminhos do JSON ("assets/scenes/ep01-sala.png") viram "EP01/assets/scenes/ep01-sala" no Resources.
    /// </summary>
    public static class GameAssets
    {
        public const string EpisodeRoot = "EP01/";
        public const string EpisodeJson = "EP01/ep01";

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static Sprite white, shadow;

        public static string ResourcePath(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath)) return null;
            string noExt = Path.ChangeExtension(jsonPath, null).Replace('\\', '/');
            return EpisodeRoot + noExt;
        }

        public static Texture2D Texture(string jsonPath)
        {
            string path = ResourcePath(jsonPath);
            if (path == null) return null;
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) Debug.LogWarning($"Imagem não encontrada em Resources: {path}");
            return tex;
        }

        /// <summary>Sprite a partir de um caminho do JSON. pivot (0..1): (0.5, 0) = pé no centro da base.</summary>
        public static Sprite Sprite(string jsonPath, Vector2 pivot)
        {
            if (string.IsNullOrEmpty(jsonPath)) return null;
            string key = jsonPath + "|" + pivot.x + "," + pivot.y;
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = Texture(jsonPath);
            if (tex == null) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f);
            s.name = Path.GetFileNameWithoutExtension(jsonPath);
            sprites[key] = s;
            return s;
        }

        public static Sprite Sprite(string jsonPath) => Sprite(jsonPath, new Vector2(0.5f, 0.5f));

        /// <summary>Sprite branco 1×1 unidade (para retângulos, contornos e fades).</summary>
        public static Sprite White
        {
            get
            {
                if (white != null) return white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "white", filterMode = FilterMode.Bilinear };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                white = UnityEngine.Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                return white;
            }
        }

        /// <summary>Sombra elíptica com degradê radial (1×1 unidade).</summary>
        public static Sprite Shadow
        {
            get
            {
                if (shadow != null) return shadow;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "shadow", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        px[y * n + x] = new Color32(32, 30, 29, (byte)(t * 0.32f * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                shadow = UnityEngine.Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return shadow;
            }
        }

        public enum Weight { Regular, Bold, ExtraBold }

        public static Font Font(Weight w)
        {
            string name = w == Weight.Regular ? "Archivo-Regular" : w == Weight.Bold ? "Archivo-Bold" : "Archivo-ExtraBold";
            if (fonts.TryGetValue(name, out var f) && f != null) return f;
            f = Resources.Load<Font>("Fonts/" + name);
            if (f == null)
            {
                Debug.LogWarning($"Fonte {name} não encontrada em Resources/Fonts. Usando a fonte padrão.");
                f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            fonts[name] = f;
            return f;
        }
    }
}
