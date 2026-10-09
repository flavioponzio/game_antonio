using RecreioEspacial.Core;
using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.World
{
    /// <summary>
    /// Contorno retangular em volta de um hotspot. Usado para o pulso de dica (vermelho),
    /// destaque do hotspot alcançável pelo teclado e modo de depuração (mostrar hotspots).
    /// </summary>
    public class RectOutline : MonoBehaviour
    {
        public enum Style { Pulse, Highlight, Debug }

        readonly SpriteRenderer[] bars = new SpriteRenderer[4];
        readonly SpriteRenderer[] glow = new SpriteRenderer[4];
        StageRect rect;
        Style style;
        float thickness;

        public static RectOutline Create(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var o = go.AddComponent<RectOutline>();
            for (int i = 0; i < 4; i++)
            {
                o.bars[i] = MakeBar(go.transform, "borda", sortingOrder + 1);
                o.glow[i] = MakeBar(go.transform, "brilho", sortingOrder);
            }
            go.SetActive(false);
            return o;
        }

        static SpriteRenderer MakeBar(Transform parent, string name, int order)
        {
            var b = new GameObject(name);
            b.transform.SetParent(parent, false);
            var sr = b.AddComponent<SpriteRenderer>();
            sr.sprite = GameAssets.White;
            sr.sortingOrder = order;
            return sr;
        }

        public void Show(StageRect r, Style s)
        {
            rect = r;
            style = s;
            // Pulso: 0,25% da largura do palco
            thickness = s == Style.Pulse ? Stage.ScreenW(0.25f) : s == Style.Highlight ? Stage.ScreenW(0.2f) : Stage.ScreenW(0.15f);
            var color = s == Style.Highlight ? Tokens.Bg : Tokens.Accent;
            if (s == Style.Debug) color = Tokens.Accent.WithAlpha(0.8f);
            foreach (var b in bars) b.color = color;
            foreach (var g in glow) g.enabled = s != Style.Debug;
            transform.localPosition = Stage.RectCenter(r);
            gameObject.SetActive(true);
            Layout(bars, Stage.RectSize(r), thickness);
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            var size = Stage.RectSize(rect);
            if (style == Style.Debug) return;
            float period = style == Style.Pulse ? 1.2f : 1.6f;
            float p = Mathf.Repeat(Time.time / period, 1f);
            float w = (1f - Mathf.Cos(p * Mathf.PI * 2f)) / 2f; // 0 → 1 → 0
            float s = 1f + 0.04f * w;
            transform.localScale = new Vector3(s, s, 1f);

            // Brilho que se espalha e some (box-shadow do protótipo)
            float spread = Stage.ScreenW(1.2f) * w;
            var gc = (style == Style.Pulse ? Tokens.Accent : Tokens.Bg).WithAlpha(0.7f * (1f - w));
            foreach (var g in glow) g.color = gc;
            Layout(glow, size + Vector2.one * (2f * thickness), Mathf.Max(0.001f, spread));
        }

        /// <summary>Posiciona 4 barras formando uma moldura por fora de um retângulo de tamanho 'size'.</summary>
        static void Layout(SpriteRenderer[] b, Vector2 size, float th)
        {
            float hw = size.x / 2f, hh = size.y / 2f;
            Set(b[0], new Vector2(0, hh + th / 2f), new Vector2(size.x + th * 2f, th));   // topo
            Set(b[1], new Vector2(0, -hh - th / 2f), new Vector2(size.x + th * 2f, th));  // base
            Set(b[2], new Vector2(-hw - th / 2f, 0), new Vector2(th, size.y));             // esquerda
            Set(b[3], new Vector2(hw + th / 2f, 0), new Vector2(th, size.y));              // direita
        }

        static void Set(SpriteRenderer sr, Vector2 pos, Vector2 scale)
        {
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        }
    }
}
