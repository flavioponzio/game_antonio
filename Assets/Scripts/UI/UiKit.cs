using System;
using RecreioEspacial.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RecreioEspacial.UI
{
    /// <summary>
    /// Fábrica de elementos de UI no estilo do design: plano, sem cantos arredondados,
    /// bordas de 2px na cor da tinta, um vermelho de destaque. Medidas em pixels de referência (1920×1080).
    /// </summary>
    public static class UiKit
    {
        public static float Cq(float v) => v * Stage.Cq;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Âncora + pivô num mesmo ponto (0..1) e posição relativa a ele.</summary>
        public static void Anchor(RectTransform rt, Vector2 anchorAndPivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchorAndPivot;
            rt.pivot = anchorAndPivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = null;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Borda chapada de 'px' em volta de um Image retangular.</summary>
        public static Outline Border(Graphic g, Color color, float px = 2.5f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(px, -px);
            o.useGraphicAlpha = false;
            return o;
        }

        public static Text Label(string name, Transform parent, string text, GameAssets.Weight weight, float size, Color color,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = GameAssets.Font(weight);
            t.text = text;
            t.fontSize = Mathf.RoundToInt(size);
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.lineSpacing = 1f;
            return t;
        }

        /// <summary>Tamanho que o texto ocupa limitado a uma largura máxima (em unidades do canvas).</summary>
        public static Vector2 Measure(Text t, float maxWidth)
        {
            var gen = t.cachedTextGeneratorForLayout;
            float ppu = t.pixelsPerUnit > 0 ? t.pixelsPerUnit : 1f;
            float w = gen.GetPreferredWidth(t.text, t.GetGenerationSettings(Vector2.zero)) / ppu;
            w = Mathf.Min(maxWidth, Mathf.Ceil(w) + 2f);
            float h = gen.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(w, 0f))) / ppu;
            return new Vector2(w, Mathf.Ceil(h));
        }

        public class ButtonParts
        {
            public Button Button;
            public Image Image;
            public Text Label;
            public RectTransform Rect;
        }

        public enum ButtonStyle { Primary, Secondary, Ghost }

        public static ButtonParts Button(string name, Transform parent, string text, ButtonStyle style, float fontSize, Action onClick,
            GameAssets.Weight weight = GameAssets.Weight.ExtraBold)
        {
            var img = Panel(name, parent, Color.white, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.fadeDuration = 0.08f;
            colors.colorMultiplier = 1f;
            Color fg;
            switch (style)
            {
                case ButtonStyle.Primary:
                    colors.normalColor = Tokens.Accent;
                    colors.highlightedColor = Tokens.AccentHover;
                    colors.pressedColor = Tokens.AccentDark;
                    colors.selectedColor = Tokens.Accent;
                    fg = Color.white;
                    break;
                case ButtonStyle.Ghost:
                    // (a cor do botão também tinge a borda, então nada de fundo transparente)
                    colors.normalColor = Tokens.Paper;
                    colors.highlightedColor = Tokens.AccentLight;
                    colors.pressedColor = Tokens.AccentLight;
                    colors.selectedColor = Tokens.Paper;
                    fg = Tokens.Ink;
                    Border(img, Tokens.Ink);
                    break;
                default:
                    colors.normalColor = Tokens.Bg;
                    colors.highlightedColor = Tokens.AccentLight;
                    colors.pressedColor = Tokens.AccentLight;
                    colors.selectedColor = Tokens.Bg;
                    fg = Tokens.Ink;
                    Border(img, Tokens.Ink);
                    break;
            }
            colors.disabledColor = Tokens.Neutral400;
            btn.colors = colors;
            btn.targetGraphic = img;

            var label = Label("Texto", img.transform, text, weight, fontSize, fg, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);
            btn.onClick.AddListener(() =>
            {
                // Evita que Espaço/Enter "cliquem" de novo o botão selecionado.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick?.Invoke();
            });
            return new ButtonParts { Button = btn, Image = img, Label = label, Rect = img.rectTransform };
        }

        /// <summary>Ajusta o retângulo do botão ao texto com padding (px de referência).</summary>
        public static Vector2 FitButton(ButtonParts b, float padX, float padY, float padRight = -1f)
        {
            if (padRight < 0) padRight = padX;
            var size = Measure(b.Label, 4000f);
            var total = new Vector2(size.x + padX + padRight, size.y + padY * 2f);
            b.Rect.sizeDelta = total;
            b.Label.rectTransform.offsetMin = new Vector2(padX, padY);
            b.Label.rectTransform.offsetMax = new Vector2(-padRight, -padY);
            return total;
        }
    }

    /// <summary>Animação "pop" (escala 0,9 → 1 e opacidade) ao aparecer.</summary>
    public class PopIn : MonoBehaviour
    {
        public float Duration = 0.15f;
        CanvasGroup group;
        float t;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable() => Play();

        public void Play()
        {
            t = 0f;
            Apply();
        }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, Duration));
            Apply();
        }

        void Apply()
        {
            float e = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(0.9f, 1f, e);
            transform.localScale = new Vector3(s, s, 1f);
            if (group != null) group.alpha = e;
        }
    }

    /// <summary>Pulso de destaque (escala 1 → 1,04) usado no item sugerido pela dica.</summary>
    public class UiPulse : MonoBehaviour
    {
        public float Period = 1.2f;
        public float Amount = 0.04f;

        void Update()
        {
            float p = Mathf.Repeat(Time.unscaledTime / Period, 1f);
            float s = 1f + Amount * (1f - Mathf.Cos(p * Mathf.PI * 2f)) / 2f;
            transform.localScale = new Vector3(s, s, 1f);
        }

        void OnDisable() => transform.localScale = Vector3.one;
    }
}
