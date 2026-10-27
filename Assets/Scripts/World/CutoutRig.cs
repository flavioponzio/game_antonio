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
    ///   stride: passo em fração da altura; parts[].sole: [[x,y],[x,y]] calcanhar e ponta do pé (nas canelas)
    /// A escala vem de fora (1 pixel = 0,01 unidade antes de escalar).
    /// </summary>
    public class CutoutRig : MonoBehaviour
    {
        public float HeightPx { get; private set; } = 500f;
        /// <summary>Altura do quadril (pivô do tronco) acima do chão, em pixels do rig.</summary>
        public float HipHeightPx { get; private set; } = 144f;
        /// <summary>Do quadril ao topo da cabeça, em pixels do rig.</summary>
        public float HipToTopPx { get; private set; } = 374f;

        /// <summary>Quanto o pé de apoio anda para trás num passo, em fração da altura (rig.json "stride"). 0 = não informado.</summary>
        public float Stride { get; private set; }
        /// <summary>O rig tem a sola dos pés marcada ("sole" nas canelas): dá para andar com o pé sempre no chão.</summary>
        public bool CanWalk => soles.Count > 0;

        readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
        // calcanhar e ponta do pé de cada canela, em unidades locais da junta
        readonly List<KeyValuePair<Transform, Vector3>> soles = new List<KeyValuePair<Transform, Vector3>>();
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
            rig.Stride = Num(root, "stride", 0f);
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
                if (d.TryGetValue("sole", out var so) && so is List<object> sl)
                    foreach (var sp in sl)
                        if (sp is List<object> pt && pt.Count >= 2 && pt[0] is double sx && pt[1] is double sy)
                            rig.soles.Add(new KeyValuePair<Transform, Vector3>(joint, PxToLocal(new Vector2((float)sx, (float)sy) - pivot)));

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

        /// <summary>Pose sentada de perfil: coxas na horizontal, canelas para baixo, mãos sobre a carteira.</summary>
        public void ApplySit(float breathe)
        {
            transform.localPosition = Vector3.zero;
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

        /// <summary>Pose no ar: joelhos dobrados e braços para cima. rise = +1 subindo … -1 caindo.</summary>
        public void ApplyJump(float rise)
        {
            transform.localPosition = Vector3.zero;
            float tuck = 1f - 0.35f * Mathf.Abs(rise); // mais encolhido no alto do pulo
            SetAngle("body", 0f);
            SetAngle("head", 3f * rise);
            SetAngle("frontLegUpper", 38f * tuck);
            SetAngle("frontLegLower", -62f * tuck);
            SetAngle("backLegUpper", -6f * tuck);
            SetAngle("backLegLower", -48f * tuck);
            SetAngle("frontArmUpper", 35f + 20f * rise);
            SetAngle("frontArmLower", 25f);
            SetAngle("backArmUpper", -18f - 10f * rise);
            SetAngle("backArmLower", 20f);
        }

        // joelho em 8 pontos de um ciclo (phase 0 = passagem com a perna no ar, π/2 = calcanhar no chão,
        // π = passagem com a perna de apoio, 3π/2 = saindo do chão na ponta do pé)
        static readonly float[] KneeKeys = { -45f, -20f, -5f, -8f, -3f, -5f, -20f, -55f };

        /// <summary>Interpolação suave (Catmull-Rom) e periódica entre pontos igualmente espaçados num ciclo de 2π.</summary>
        static float Periodic(float[] keys, float phase)
        {
            int n = keys.Length;
            float u = Mathf.Repeat(phase / (2f * Mathf.PI), 1f) * n;
            int i = (int)u;
            float f = u - i;
            float p0 = keys[(i - 1 + n) % n], p1 = keys[i % n], p2 = keys[(i + 1) % n], p3 = keys[(i + 2) % n];
            return 0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
        }

        /// <summary>
        /// Ciclo de caminhada. phase avança π a cada passo; amount (0..1) = quanto está andando.
        /// Coxas balançam ±27°, o joelho segue KneeKeys e os braços vão ao contrário da perna do mesmo lado.
        /// Depois chame GroundFeet para o pé mais baixo ficar no chão (o sobe-e-desce sai daí).
        /// </summary>
        public void ApplyWalk(float phase, float amount)
        {
            const float thigh = 27f, arm = 20f;
            SetAngle("frontLegUpper", thigh * Mathf.Sin(phase) * amount);
            SetAngle("frontLegLower", Periodic(KneeKeys, phase) * amount);
            SetAngle("backLegUpper", thigh * Mathf.Sin(phase + Mathf.PI) * amount);
            SetAngle("backLegLower", Periodic(KneeKeys, phase + Mathf.PI) * amount);
            float sf = Mathf.Sin(phase + Mathf.PI), sb = Mathf.Sin(phase);
            SetAngle("frontArmUpper", arm * sf * amount);
            SetAngle("frontArmLower", (8f + 16f * Mathf.Max(0f, sf)) * amount);
            SetAngle("backArmUpper", arm * sb * amount);
            SetAngle("backArmLower", (8f + 16f * Mathf.Max(0f, sb)) * amount);
            SetAngle("body", -3f * amount);
            SetAngle("head", (2f + 1.5f * Mathf.Cos(2f * phase)) * amount);
        }

        /// <summary>Sobe ou desce o boneco para a sola mais baixa encostar no chão (y = 0 do rig).</summary>
        public void GroundFeet()
        {
            transform.localPosition = Vector3.zero;
            if (soles.Count == 0) return;
            float minY = float.MaxValue;
            foreach (var s in soles)
                minY = Mathf.Min(minY, transform.InverseTransformPoint(s.Key.TransformPoint(s.Value)).y);
            transform.localPosition = new Vector3(0f, -minY * transform.localScale.y, 0f);
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
