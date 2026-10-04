using System;
using RecreioEspacial.Core;
using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.World
{
    /// <summary>
    /// NPC desenhado dentro do retângulo do hotspot (altura = altura do rect, pé na base, centrado).
    /// Respira, dá pulinhos quando fala, inclina para o Antônio e anda com passinhos (npcMove).
    /// </summary>
    public class NpcActor : MonoBehaviour
    {
        public string Id { get; private set; }
        public bool Talking { get; set; }
        public float AntonioX { get; set; } = 50f;

        Func<StageRect> rectSource;
        bool faceTowardAntonio;
        Transform body;
        SpriteRenderer sr;
        float breatheOffset;

        // posição atual (centro x %, base %, altura %)
        Vector3 cur;
        bool initialized;
        bool moving;
        Vector3 moveFrom;
        float moveStart, moveDuration;
        float lean;

        public static NpcActor Create(Transform parent, string id, Sprite sprite, bool faceTowardAntonio, Func<StageRect> rectSource)
        {
            var go = new GameObject("NPC " + id);
            go.transform.SetParent(parent, false);
            var n = go.AddComponent<NpcActor>();
            n.Id = id;
            n.rectSource = rectSource;
            n.faceTowardAntonio = faceTowardAntonio;
            n.breatheOffset = (id.Length > 0 ? id[0] % 7 : 0) * 0.45f;

            var b = new GameObject("Corpo");
            b.transform.SetParent(go.transform, false);
            n.body = b.transform;
            n.sr = b.AddComponent<SpriteRenderer>();
            n.sr.sprite = sprite;
            return n;
        }

        /// <summary>Anda até a posição calculada pelas flags durante 'seconds' (a posição-alvo é relida a cada frame).</summary>
        public void StartMove(float seconds)
        {
            moveFrom = cur;
            moveStart = Time.time;
            moveDuration = Mathf.Max(0.01f, seconds);
            moving = true;
        }

        static Vector3 FromRect(StageRect r) => new Vector3(r.CenterX, r.BottomFromBottom, r.H);

        void Update()
        {
            if (rectSource == null || sr == null) return;
            var target = FromRect(rectSource());
            if (!initialized) { cur = target; initialized = true; }

            float now = Time.time;
            if (moving)
            {
                float k = Mathf.Clamp01((now - moveStart) / moveDuration);
                float e = k * k * (3f - 2f * k); // ease-in-out
                cur = Vector3.Lerp(moveFrom, target, e);
                if (k >= 1f) moving = false;
            }
            else cur = target;

            float heightUnits = Stage.H(cur.z);
            var spr = sr.sprite;
            float spriteUnits = spr != null ? spr.rect.height / spr.pixelsPerUnit : 1f;
            float k0 = heightUnits / spriteUnits;

            // Animações (equivalentes aos keyframes do protótipo)
            float ty = 0f, sx = 1f, sy = 1f;
            if (moving)
            {
                float p = Mathf.Repeat(now / 0.32f, 1f);
                float w = (1f - Mathf.Cos(p * Mathf.PI * 2f)) / 2f; // 0 → 1 → 0
                ty = 2.6f * w;
                sx = Mathf.Lerp(1.02f, 0.99f, w);
                sy = Mathf.Lerp(0.98f, 1.02f, w);
            }
            else if (Talking)
            {
                float p = Mathf.Repeat(now / 0.5f, 1f);
                if (p < 0.3f) { float w = Smooth(p / 0.3f); ty = 1.8f * w; sx = Mathf.Lerp(1f, 0.985f, w); sy = Mathf.Lerp(1f, 1.03f, w); }
                else if (p < 0.6f) { float w = Smooth((p - 0.3f) / 0.3f); ty = 1.8f * (1f - w); sx = Mathf.Lerp(0.985f, 1.025f, w); sy = Mathf.Lerp(1.03f, 0.975f, w); }
                else { float w = Smooth((p - 0.6f) / 0.4f); sx = Mathf.Lerp(1.025f, 1f, w); sy = Mathf.Lerp(0.975f, 1f, w); }
            }
            else
            {
                float w = (1f - Mathf.Cos((now + breatheOffset) / 3.4f * Mathf.PI * 2f)) / 2f;
                sx = 1f + 0.008f * w;
                sy = 1f + 0.016f * w;
            }

            // Inclina na direção do Antônio quando ele está perto
            float d = AntonioX - cur.x;
            float sg = d >= 0 ? 1f : -1f;
            float leanTarget = Talking ? sg * 3f : Mathf.Abs(d) < 28f ? sg * 1.5f : 0f;
            lean += (leanTarget - lean) * Mathf.Min(1f, Time.deltaTime * 6f);
            float flip = faceTowardAntonio && d > 0 ? -1f : 1f;

            transform.localPosition = Stage.Point(cur.x, cur.y);
            body.localPosition = new Vector3(0f, ty / 100f * heightUnits, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, -lean); // topo inclina para o lado do Antônio
            body.localScale = new Vector3(k0 * sx * flip, k0 * sy, 1f);
            sr.sortingOrder = Stage.SortingOrderForBottom(cur.y);
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
