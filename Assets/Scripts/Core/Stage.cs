using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.Core
{
    /// <summary>
    /// Conversão entre % do palco (como no JSON) e unidades do mundo Unity.
    /// O palco tem 19,2 × 10,8 unidades (1920×1080 a 100 PPU), centrado na origem.
    /// </summary>
    public static class Stage
    {
        public const float Width = 19.2f;
        public const float Height = 10.8f;
        public const float Aspect = Width / Height;
        /// <summary>1 "cqw" do protótipo HTML = 1% da largura do palco, em pixels de referência (1920 px).</summary>
        public const float Cq = 19.2f;

        /// <summary>x em % → unidade do mundo.</summary>
        public static float X(float xPct) => (xPct / 100f - 0.5f) * Width;

        /// <summary>y medido da base em % → unidade do mundo.</summary>
        public static float YFromBottom(float yPct) => (yPct / 100f - 0.5f) * Height;

        /// <summary>y medido do topo em % → unidade do mundo.</summary>
        public static float YFromTop(float yPct) => (0.5f - yPct / 100f) * Height;

        /// <summary>Altura em % → unidades.</summary>
        public static float H(float pct) => pct / 100f * Height;

        /// <summary>Largura em % → unidades.</summary>
        public static float W(float pct) => pct / 100f * Width;

        public static Vector3 Point(float xPct, float yFromBottomPct, float z = 0f) =>
            new Vector3(X(xPct), YFromBottom(yFromBottomPct), z);

        /// <summary>Centro de um retângulo do JSON em unidades do mundo.</summary>
        public static Vector3 RectCenter(StageRect r, float z = 0f) =>
            new Vector3(X(r.X + r.W / 2f), YFromTop(r.Y + r.H / 2f), z);

        public static Vector2 RectSize(StageRect r) => new Vector2(W(r.W), H(r.H));

        /// <summary>Posição de tela → % do palco (x, y medido da BASE). Pode sair de 0..100 nas faixas pretas.</summary>
        public static Vector2 ScreenToStage(Camera cam, Vector2 screen)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2((w.x / Width + 0.5f) * 100f, (w.y / Height + 0.5f) * 100f);
        }

        /// <summary>Ordem de desenho: quem está mais perto da base desenha por cima.</summary>
        public static int SortingOrderForBottom(float yFromBottomPct) =>
            100 + Mathf.RoundToInt(2f * (100f - yFromBottomPct));
    }

    /// <summary>Tokens visuais do design (README do handoff).</summary>
    public static class Tokens
    {
        public static readonly Color Bg = Hex("#f3f2f2");
        public static readonly Color Ink = Hex("#201e1d");
        public static readonly Color Accent = Hex("#ec3013");
        public static readonly Color AccentHover = Hex("#d42a10");
        public static readonly Color AccentLight = Hex("#fde4dd");
        public static readonly Color AccentDark = Hex("#a8200b");
        public static readonly Color Neutral700 = Hex("#5c5856");
        public static readonly Color Neutral400 = Hex("#a19d9a");
        public static readonly Color Paper = Hex("#fbfaf6");
        public static readonly Color PaperLine = Hex("#c9d6e6");
        public static readonly Color White = Color.white;

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
    }
}
