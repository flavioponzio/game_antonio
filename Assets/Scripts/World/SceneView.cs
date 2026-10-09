using System.Collections.Generic;
using RecreioEspacial.Core;
using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.World
{
    /// <summary>
    /// Monta e mantém o mundo de uma cena jogável: fundo, "patches" (itens retirados), NPCs,
    /// caixas provisórias para objetos sem arte e contornos (dica, teclado, depuração).
    /// Também responde perguntas sobre hotspots: retângulo atual, visibilidade, onde andar e clique.
    /// </summary>
    public class SceneView : MonoBehaviour
    {
        public SceneDef Scene { get; private set; }
        public bool ShowDebugOutlines { get; set; }

        EpisodeData ep;
        GameState state;
        readonly List<(PatchDef patch, SpriteRenderer sr)> patches = new List<(PatchDef, SpriteRenderer)>();
        readonly Dictionary<string, NpcActor> npcs = new Dictionary<string, NpcActor>();
        readonly Dictionary<string, GameObject> placeholders = new Dictionary<string, GameObject>();
        readonly Dictionary<string, RectOutline> debugOutlines = new Dictionary<string, RectOutline>();
        RectOutline pulse, highlight;
        string pulseId, highlightId;

        public static SceneView Create(Transform parent, EpisodeData ep, GameState state)
        {
            var go = new GameObject("Cena");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SceneView>();
            v.ep = ep;
            v.state = state;
            return v;
        }

        public void Build(SceneDef scene)
        {
            Clear();
            Scene = scene;
            if (scene == null || scene.IsCutscene) return;

            // Fundo com "cover": altura cheia, centralizado (corta um pouco dos lados).
            var bgSprite = GameAssets.Sprite(scene.Background);
            if (bgSprite != null)
            {
                var bg = NewSprite("Fundo", bgSprite, -100);
                float k = Stage.Height / (bgSprite.rect.height / bgSprite.pixelsPerUnit);
                bg.transform.localScale = new Vector3(k, k, 1f);
            }

            foreach (var h in scene.Hotspots)
            {
                if (h.Patch != null)
                {
                    var ps = GameAssets.Sprite(h.Patch.Image);
                    if (ps != null)
                    {
                        var sr = NewSprite("Patch " + h.Id, ps, -90);
                        FitToRect(sr, h.Patch.Rect);
                        patches.Add((h.Patch, sr));
                    }
                }

                CharacterDef ch = null;
                if (h.IsNpc && ep.Characters.TryGetValue(h.Id, out ch) && !string.IsNullOrEmpty(ch.Sprite))
                {
                    var spr = GameAssets.Sprite(ch.Sprite, new Vector2(0.5f, 0f));
                    var hotspot = h;
                    npcs[h.Id] = NpcActor.Create(transform, h.Id, spr, ch.FaceTowardAntonio, () => RectOf(hotspot));
                }
                else if (h.IsNpc || h.IsProp)
                {
                    placeholders[h.Id] = MakePlaceholder(h);
                }
            }

            foreach (var f in scene.Foreground)
            {
                var fs = GameAssets.Sprite(f.Image);
                if (fs == null) continue;
                // Desenha logo acima de quem tem a base em 'depth' (e abaixo de quem está mais perto).
                var sr = NewSprite("Frente " + f.Id, fs, Stage.SortingOrderForBottom(100f - f.Depth) + 1);
                FitToRect(sr, f.Rect);
            }

            pulse = RectOutline.Create(transform, "Pulso de dica", 400);
            highlight = RectOutline.Create(transform, "Destaque teclado", 398);
            Refresh();
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            patches.Clear();
            npcs.Clear();
            placeholders.Clear();
            debugOutlines.Clear();
            pulse = highlight = null;
            pulseId = highlightId = null;
            Scene = null;
        }

        // ---------- Consultas sobre hotspots ----------

        public StageRect RectOf(HotspotDef h)
        {
            foreach (var kv in h.RectWhenFlag) if (state.Flag(kv.Key)) return kv.Value;
            return h.Rect;
        }

        public float? WalkOf(HotspotDef h)
        {
            foreach (var kv in h.WalkWhenFlag) if (state.Flag(kv.Key)) return kv.Value;
            return h.Walk;
        }

        public bool IsVisible(HotspotDef h) => state.Check(h.Show);

        public NpcActor Npc(string id) => id != null && npcs.TryGetValue(id, out var n) ? n : null;

        public IEnumerable<NpcActor> Npcs => npcs.Values;

        public HotspotDef FindVisible(string id)
        {
            if (Scene == null || id == null) return null;
            var h = Scene.FindHotspot(id);
            return h != null && IsVisible(h) ? h : null;
        }

        /// <summary>Hotspot sob o ponto (x %, y medido do TOPO %). NPCs com arte ficam por cima, como no protótipo.</summary>
        public HotspotDef HitTest(float x, float yFromTop)
        {
            if (Scene == null) return null;
            HotspotDef best = null;
            int bestZ = int.MinValue, bestIndex = -1;
            for (int i = 0; i < Scene.Hotspots.Count; i++)
            {
                var h = Scene.Hotspots[i];
                if (!IsVisible(h)) continue;
                var r = RectOf(h);
                if (!r.Contains(x, yFromTop)) continue;
                int z = npcs.ContainsKey(h.Id) ? Stage.SortingOrderForBottom(r.BottomFromBottom) : 1;
                if (z > bestZ || z == bestZ && i > bestIndex)
                {
                    best = h;
                    bestZ = z;
                    bestIndex = i;
                }
            }
            return best;
        }

        public void SetPulse(string id) => pulseId = id;
        public void SetHighlight(string id) => highlightId = id;

        // ---------- Atualização visual ----------

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (Scene == null) return;

            foreach (var (patch, sr) in patches) sr.enabled = state.Flag(patch.Flag);
            foreach (var kv in npcs) kv.Value.gameObject.SetActive(IsVisible(Scene.FindHotspot(kv.Key)));
            foreach (var kv in placeholders)
            {
                var h = Scene.FindHotspot(kv.Key);
                bool vis = IsVisible(h);
                kv.Value.SetActive(vis);
                if (vis) kv.Value.transform.localPosition = Stage.RectCenter(RectOf(h));
            }

            UpdateOutline(pulse, pulseId, RectOutline.Style.Pulse);
            UpdateOutline(highlight, highlightId != pulseId ? highlightId : null, RectOutline.Style.Highlight);

            foreach (var h in Scene.Hotspots)
            {
                bool show = ShowDebugOutlines && IsVisible(h);
                debugOutlines.TryGetValue(h.Id, out var o);
                if (show && o == null) debugOutlines[h.Id] = o = RectOutline.Create(transform, "Debug " + h.Id, 390);
                if (o == null) continue;
                if (show) o.Show(RectOf(h), RectOutline.Style.Debug);
                else o.Hide();
            }
        }

        void UpdateOutline(RectOutline o, string id, RectOutline.Style style)
        {
            if (o == null) return;
            var h = FindVisible(id);
            if (h == null) { o.Hide(); return; }
            o.Show(RectOf(h), style);
        }

        // ---------- Construção ----------

        SpriteRenderer NewSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void FitToRect(SpriteRenderer sr, StageRect r)
        {
            var s = sr.sprite;
            var size = Stage.RectSize(r);
            sr.transform.localPosition = Stage.RectCenter(r);
            sr.transform.localScale = new Vector3(
                size.x / (s.rect.width / s.pixelsPerUnit),
                size.y / (s.rect.height / s.pixelsPerUnit), 1f);
        }

        /// <summary>Caixa "arte pendente" para objetos que ainda não têm ilustração (ex.: estojo, apontador).</summary>
        GameObject MakePlaceholder(HotspotDef h)
        {
            var root = new GameObject("Provisório " + h.Id);
            root.transform.SetParent(transform, false);
            var r = RectOf(h);
            var size = Stage.RectSize(r);
            root.transform.localPosition = Stage.RectCenter(r);

            var box = new GameObject("Caixa").AddComponent<SpriteRenderer>();
            box.transform.SetParent(root.transform, false);
            box.sprite = GameAssets.White;
            box.color = Tokens.Bg.WithAlpha(0.82f);
            box.sortingOrder = -10;
            box.transform.localScale = new Vector3(size.x, size.y, 1f);

            float th = Stage.ScreenW(0.2f);
            AddBar(root.transform, new Vector2(0, size.y / 2 - th / 2), new Vector2(size.x, th));
            AddBar(root.transform, new Vector2(0, -size.y / 2 + th / 2), new Vector2(size.x, th));
            AddBar(root.transform, new Vector2(-size.x / 2 + th / 2, 0), new Vector2(th, size.y));
            AddBar(root.transform, new Vector2(size.x / 2 - th / 2, 0), new Vector2(th, size.y));

            var font = GameAssets.Font(GameAssets.Weight.ExtraBold);
            var textGo = new GameObject("Texto");
            textGo.transform.SetParent(root.transform, false);
            float pad = Stage.ScreenW(0.5f);
            textGo.transform.localPosition = new Vector3(-size.x / 2 + pad, size.y / 2 - pad, 0);
            var tm = textGo.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = h.Label + "\n<size=34>arte pendente</size>";
            tm.richText = true;
            tm.fontSize = 48;
            tm.characterSize = 0.05f; // ~1,25 cqw de altura
            tm.anchor = TextAnchor.UpperLeft;
            tm.color = Tokens.Ink;
            var mr = textGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = -8;
            return root;
        }

        static void AddBar(Transform parent, Vector2 pos, Vector2 scale)
        {
            var sr = new GameObject("Borda").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = GameAssets.White;
            sr.color = Tokens.Ink;
            sr.sortingOrder = -9;
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        }
    }
}
