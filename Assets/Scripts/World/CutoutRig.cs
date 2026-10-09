using System.Collections.Generic;
using RecreioEspacial.Core;
using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.World
{
    /// <summary>
    /// Boneco recortado ("cut-out"): partes do personagem (cabeça, tronco, braços, pernas…) presas em juntas.
    /// Lido de um rig.json (gerado por Design/tools/cortar_rig_antonio.py):
    ///   images: { nome: { file, box:[x,y,w,h] } }  — recorte de cada parte, em pixels da arte original (y para baixo)
    ///   parts:  [ { name, image, parent, pivot:[x,y], tint, order } ] — pivot = junta onde a parte gira
    ///   root:   [x,y] — ponto do chão (entre os pés); height: altura do personagem em pixels
    /// A escala vem de fora (1 pixel = 0,01 unidade antes de escalar).
    /// </summary>
    public class CutoutRig : MonoBehaviour
    {
        public float HeightPx { get; private set; } = 500f;
        /// <summary>Altura do quadril (pivô do tronco) acima do chão, em pixels do rig.</summary>
        public float HipHeightPx { get; private set; } = 144f;
        /// <summary>Do quadril ao topo da cabeça, em pixels do rig.</summary>
        public float HipToTopPx { get; private set; } = 374f;

        readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly Dictionary<string, SpriteRenderer> byPart = new Dictionary<string, SpriteRenderer>();
        readonly HashSet<string> hidden = new HashSet<string>();
        bool visible = true;

        public static CutoutRig Load(Transform parent, string jsonPath)
        {
            var text = Resources.Load<TextAsset>(GameAssets.ResourcePath(jsonPath));
            if (text == null)
            {
                Debug.LogWarning($"Rig não encontrado: {jsonPath}");
                return null;
            }

            var root = MiniJson.Parse(text.text) as Dictionary<string, object>;
            if (root == null) return null;

            var go = new GameObject("Rig");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CutoutRig>();
            rig.HeightPx = Num(root, "height", 500f);
            var rootPt = Vec(root, "root");
            rig.HipToTopPx = rig.HeightPx - 144f;

            var images = root.TryGetValue("images", out var im) ? im as Dictionary<string, object> : null;
            var parts = root.TryGetValue("parts", out var pl) ? pl as List<object> : null;
            if (images == null || parts == null) return rig;

            // 1) cria todas as juntas; 2) liga pais e posições
            var defs = new List<Dictionary<string, object>>();
            foreach (var o in parts)
            {
                if (!(o is Dictionary<string, object> d)) continue;
                string name = Str(d, "name");
                if (name == null) continue;
                defs.Add(d);
                var j = new GameObject(name).transform;
                j.SetParent(go.transform, false);
                rig.joints[name] = j;
            }

            var bodyDef = defs.Find(x => Str(x, "name") == "body");
            if (bodyDef != null)
            {
                rig.HipHeightPx = rootPt.y - Vec(bodyDef, "pivot").y;
                rig.HipToTopPx = rig.HeightPx - rig.HipHeightPx;
            }

            foreach (var d in defs)
            {
                string name = Str(d, "name");
                var joint = rig.joints[name];
                var pivot = Vec(d, "pivot");
                string parentName = Str(d, "parent");
                Vector2 parentPivot = rootPt;
                if (parentName != null && rig.joints.TryGetValue(parentName, out var pj))
                {
                    joint.SetParent(pj, false);
                    var pd = defs.Find(x => Str(x, "name") == parentName);
                    parentPivot = Vec(pd, "pivot");
                }
                joint.localPosition = PxToLocal(pivot - parentPivot);

                // imagem da parte, com o pivô do sprite na junta
                string imageKey = Str(d, "image");
                if (imageKey == null || !(images.TryGetValue(imageKey, out var io) && io is Dictionary<string, object> idef)) continue;
                var box = List4(idef, "box");
                var spritePivot = new Vector2((pivot.x - box.x) / box.z, 1f - (pivot.y - box.y) / box.w);
                var sprite = GameAssets.Sprite(Str(idef, "file"), spritePivot);
                if (sprite == null) continue;

                var art = new GameObject("arte").AddComponent<SpriteRenderer>();
                art.transform.SetParent(joint, false);
                art.sprite = sprite;
                float tint = Num(d, "tint", 1f);
                art.color = new Color(tint, tint, tint, 1f);
                art.sortingOrder = (int)Num(d, "order", 0);
                rig.renderers.Add(art);
                rig.byPart[name] = art;
            }
            return rig;
        }

        /// <summary>Gira uma junta (graus, positivo = anti-horário com o personagem olhando para a direita).</summary>
        public void SetAngle(string joint, float degrees)
        {
            if (joints.TryGetValue(joint, out var t)) t.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            foreach (var kv in byPart) kv.Value.enabled = visible && !hidden.Contains(kv.Key);
        }

        /// <summary>Esconde/mostra uma parte (ex.: o braço mecânico quando ele não está com o item).</summary>
        public void SetPartHidden(string part, bool hide)
        {
            if (part == null) return;
            if (hide) hidden.Add(part); else hidden.Remove(part);
            if (byPart.TryGetValue(part, out var r)) r.enabled = visible && !hide;
        }

        /// <summary>
        /// Ciclo de caminhada procedural. phase avança π a cada passo; amount (0..1) = quanto está andando.
        /// </summary>
        /// <summary>Pose sentada de perfil: coxas na horizontal, canelas para baixo, mãos sobre a carteira.</summary>
        public void ApplySit(float breathe)
        {
            SetAngle("body", -6f);
            SetAngle("head", -6f + breathe * 2f);
            SetAngle("frontLegUpper", 96f);
            SetAngle("backLegUpper", 92f);
            SetAngle("frontLegLower", -96f);
            SetAngle("backLegLower", -90f);
            SetAngle("frontArmUpper", 55f + breathe);
            SetAngle("frontArmLower", 45f);
            SetAngle("backArmUpper", 62f + breathe);
            SetAngle("backArmLower", 42f);
        }

        public void ApplyWalk(float phase, float amount)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            const float thigh = 22f, knee = 40f, arm = 18f;
            SetAngle("frontLegUpper", thigh * s * amount);
            SetAngle("backLegUpper", -thigh * s * amount);
            // o joelho dobra enquanto a perna passa para a frente
            SetAngle("frontLegLower", -knee * Mathf.Max(0f, c) * amount);
            SetAngle("backLegLower", -knee * Mathf.Max(0f, -c) * amount);
            // braços ao contrário das pernas
            SetAngle("frontArmUpper", -arm * s * amount);
            SetAngle("backArmUpper", arm * s * amount);
            SetAngle("frontArmLower", (6f + 6f * Mathf.Max(0f, -s)) * amount);
            SetAngle("backArmLower", (6f + 6f * Mathf.Max(0f, s)) * amount);
            SetAngle("head", 1.5f * c * amount);
            SetAngle("body", 0f);
        }

        static Vector3 PxToLocal(Vector2 deltaPx) => new Vector3(deltaPx.x / 100f, -deltaPx.y / 100f, 0f);

        static string Str(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as string : null;

        static float Num(Dictionary<string, object> d, string k, float fallback) =>
            d != null && d.TryGetValue(k, out var v) && v is double x ? (float)x : fallback;

        static Vector2 Vec(Dictionary<string, object> d, string k)
        {
            if (d != null && d.TryGetValue(k, out var v) && v is List<object> l && l.Count >= 2 && l[0] is double a && l[1] is double b)
                return new Vector2((float)a, (float)b);
            return Vector2.zero;
        }

        static Vector4 List4(Dictionary<string, object> d, string k)
        {
            if (d != null && d.TryGetValue(k, out var v) && v is List<object> l && l.Count >= 4)
            {
                float f(int i) => l[i] is double x ? (float)x : 0f;
                return new Vector4(f(0), f(1), Mathf.Max(1f, f(2)), Mathf.Max(1f, f(3)));
            }
            return new Vector4(0, 0, 1, 1);
        }
    }
}
