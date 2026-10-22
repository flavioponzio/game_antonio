using System.Collections.Generic;
using RecreioEspacial.Core;
using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.World
{
    /// <summary>
    /// Quadros de animação de um personagem (folha de poses), lidos de um frames.json:
    ///   refHeight: altura em px do quadro "front" (define a escala de todos)
    ///   frames: { nome: { file, fileHolding?, size:[w,h], pivot:[x 0..1, y 0..1], hold?:{ hand:[px,py], flip, angle } } }
    ///   held: { file, grip:[x 0..1, y 0..1] } — item desenhado na mão (ex.: braço mecânico)
    /// Os quadros olham para a direita; para a esquerda, espelha.
    /// </summary>
    public class FrameSet
    {
        public class Frame
        {
            public Sprite Sprite;
            /// <summary>Versão segurando o item (ex.: com o braço mecânico). Pode ser nula.</summary>
            public Sprite SpriteHolding;
            public Vector2 Size, Pivot;
            public bool HasHold, HoldFlip;
            public Vector2 Hand;
            public float HoldAngle;
        }

        public float RefHeightPx = 490f;
        /// <summary>Quadros do ciclo de caminhada, em ordem (um ciclo = 2 passos). Vazio = walk1..walk4 / walk1..walk2.</summary>
        public readonly List<string> WalkCycle = new List<string>();
        public readonly Dictionary<string, Frame> Frames = new Dictionary<string, Frame>();
        public Sprite Held;

        public Frame Get(string name) => name != null && Frames.TryGetValue(name, out var f) ? f : null;

        public static FrameSet Load(string jsonPath)
        {
            var text = Resources.Load<TextAsset>(GameAssets.ResourcePath(jsonPath));
            if (text == null)
            {
                Debug.LogWarning($"Quadros não encontrados: {jsonPath}");
                return null;
            }
            if (!(MiniJson.Parse(text.text) is Dictionary<string, object> root)) return null;

            var set = new FrameSet { RefHeightPx = Num(root, "refHeight", 490f) };
            if (root.TryGetValue("walkCycle", out var wc) && wc is List<object> wl)
                foreach (var o in wl) if (o is string ws) set.WalkCycle.Add(ws);
            if (root.TryGetValue("frames", out var fo) && fo is Dictionary<string, object> frames)
            {
                foreach (var kv in frames)
                {
                    if (!(kv.Value is Dictionary<string, object> d)) continue;
                    var pivot = Vec(d, "pivot", new Vector2(0.5f, 0f));
                    var sprite = GameAssets.Sprite(d.TryGetValue("file", out var f) ? f as string : null, pivot);
                    if (sprite == null) continue;
                    var fr = new Frame { Sprite = sprite, Size = Vec(d, "size", new Vector2(sprite.rect.width, sprite.rect.height)), Pivot = pivot };
                    if (d.TryGetValue("fileHolding", out var fh) && fh is string fhs) fr.SpriteHolding = GameAssets.Sprite(fhs, pivot);
                    if (d.TryGetValue("hold", out var ho) && ho is Dictionary<string, object> h)
                    {
                        fr.HasHold = true;
                        fr.Hand = Vec(h, "hand", Vector2.zero);
                        fr.HoldFlip = h.TryGetValue("flip", out var fl) && fl is bool b && b;
                        fr.HoldAngle = Num(h, "angle", 0f);
                    }
                    set.Frames[kv.Key] = fr;
                }
            }
            if (root.TryGetValue("held", out var heo) && heo is Dictionary<string, object> held)
                set.Held = GameAssets.Sprite(held.TryGetValue("file", out var hf) ? hf as string : null, Vec(held, "grip", new Vector2(1f, 1f)));
            return set;
        }

        /// <summary>Posição da mão em unidades locais do quadro (pivô do quadro na origem, 1 px = 0,01).</summary>
        public static Vector2 HandLocal(Frame f) =>
            new Vector2((f.Hand.x - f.Pivot.x * f.Size.x) / 100f, (f.Size.y - f.Hand.y - f.Pivot.y * f.Size.y) / 100f);

        static float Num(Dictionary<string, object> d, string k, float fb) => d.TryGetValue(k, out var v) && v is double x ? (float)x : fb;

        static Vector2 Vec(Dictionary<string, object> d, string k, Vector2 fb) =>
            d.TryGetValue(k, out var v) && v is List<object> l && l.Count >= 2 && l[0] is double a && l[1] is double b
                ? new Vector2((float)a, (float)b) : fb;
    }
}
