using System;
using System.Collections.Generic;
using System.Globalization;

namespace RecreioEspacial.Data
{
    // Modelo de dados do episódio, lido de Resources/EP01/ep01.json.
    // Coordenadas sempre em % do palco 16:9 (ver README do handoff).

    /// <summary>Retângulo em % do palco. X/Y = canto superior esquerdo, Y medido do topo.</summary>
    [Serializable]
    public struct StageRect
    {
        public float X, Y, W, H;
        public StageRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float CenterX => X + W / 2f;
        /// <summary>Base do retângulo medida a partir da base do palco (bottom %).</summary>
        public float BottomFromBottom => 100f - (Y + H);
        /// <summary>Topo do retângulo medido a partir da base do palco (bottom %).</summary>
        public float TopFromBottom => 100f - Y;
        public bool Contains(float x, float yFromTop) => x >= X && x <= X + W && yFromTop >= Y && yFromTop <= Y + H;
    }

    public class EpisodeData
    {
        public string Episode;
        public string Title;
        public Dictionary<string, CharacterDef> Characters = new Dictionary<string, CharacterDef>();
        public Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public List<string> FailLines = new List<string>();
        public Dictionary<string, SceneDef> Scenes = new Dictionary<string, SceneDef>();
        public Dictionary<string, DebugStartState> DebugStartStates = new Dictionary<string, DebugStartState>();

        public string NameOf(string who) =>
            who != null && Characters.TryGetValue(who, out var c) ? c.Name : who;

        public static EpisodeData FromJson(string json)
        {
            var root = J.Dict(MiniJson.Parse(json));
            var ep = new EpisodeData
            {
                Episode = J.Str(root, "episode"),
                Title = J.Str(root, "title"),
            };

            foreach (var kv in J.Dict(J.Get(root, "characters")))
            {
                var d = J.Dict(kv.Value);
                var c = new CharacterDef
                {
                    Id = kv.Key,
                    Name = J.Str(d, "name") ?? kv.Key,
                    Sprite = J.Str(d, "sprite"),
                    FaceTowardAntonio = J.Bool(d, "faceTowardAntonio"),
                    VoiceOnly = J.Bool(d, "voiceOnly"),
                };
                var sprites = J.Dict(J.Get(d, "sprites"));
                c.SpriteFront = J.Str(sprites, "front");
                c.SpriteSide = J.Str(sprites, "side");
                c.RigSide = J.Str(d, "rigSide");
                c.SpriteFrontHolding = J.Str(sprites, "frontHolding");
                c.HoldingItem = J.Str(d, "holdingItem");
                c.HoldingRigPart = J.Str(d, "holdingRigPart");
                ep.Characters[kv.Key] = c;
            }

            foreach (var kv in J.Dict(J.Get(root, "items")))
            {
                var d = J.Dict(kv.Value);
                ep.Items[kv.Key] = new ItemDef { Id = kv.Key, Label = J.Str(d, "label") ?? kv.Key, Icon = J.Str(d, "icon"), Article = J.Str(d, "article") };
            }

            ep.FailLines = J.StrList(J.Get(root, "failLines"));

            foreach (var kv in J.Dict(J.Get(root, "scenes")))
                ep.Scenes[kv.Key] = SceneDef.Parse(kv.Key, J.Dict(kv.Value));

            foreach (var kv in J.Dict(J.Get(root, "debugStartStates")))
            {
                var d = J.Dict(kv.Value);
                ep.DebugStartStates[kv.Key] = new DebugStartState
                {
                    Flags = J.StrList(J.Get(d, "flags")),
                    Inventory = J.StrList(J.Get(d, "inventory")),
                };
            }
            return ep;
        }
    }

    public class CharacterDef
    {
        public string Id, Name, Sprite, SpriteFront, SpriteSide;
        /// <summary>rig.json do boneco recortado de perfil (opcional).</summary>
        public string RigSide;
        /// <summary>Versão de frente segurando o item HoldingItem (ex.: braço mecânico); a parte HoldingRigPart do rig só aparece com ele.</summary>
        public string SpriteFrontHolding, HoldingItem, HoldingRigPart;
        public bool FaceTowardAntonio, VoiceOnly;
    }

    public class ItemDef
    {
        public string Id, Label, Icon;
        /// <summary>Artigo ("o"/"a") para frases como "Ainda falta a corda".</summary>
        public string Article;
    }

    /// <summary>
    /// Restrição de cena enquanto uma flag está ligada (ex.: nas costas do Caio):
    /// só os hotspots em Allow respondem, os outros dizem Say; NoWalk impede andar.
    /// </summary>
    public class RestrictionDef
    {
        public string Flag, Say;
        public List<string> Allow = new List<string>();
        public bool NoWalk;
    }

    public class DebugStartState
    {
        public List<string> Flags = new List<string>();
        public List<string> Inventory = new List<string>();
    }

    public class FloorDef
    {
        public float NearY = 2, FarY = 2, FarScale = 1;
        public float NearXMin = 6, NearXMax = 94, FarXMin = 6, FarXMax = 94;
    }

    public class SceneDef
    {
        public string Id, Title, Type = "play", Background;
        public float AntonioHeight = 50, StartX = 50;
        public FloorDef Floor = new FloorDef();
        public List<ActionDef> Intro = new List<ActionDef>();
        public List<HotspotDef> Hotspots = new List<HotspotDef>();
        public Dictionary<string, PuzzleDef> Puzzles = new Dictionary<string, PuzzleDef>();
        public List<ObjectiveDef> Objectives = new List<ObjectiveDef>();
        public RestrictionDef Restriction;
        public List<ForegroundDef> Foreground = new List<ForegroundDef>();

        // Cutscene
        public List<PanelDef> Panels = new List<PanelDef>();
        public float PanelSeconds = 3.2f, CrossfadeSeconds = 1.2f;
        public string Then;

        public bool IsCutscene => Type == "cutscene";

        public HotspotDef FindHotspot(string id) => Hotspots.Find(h => h.Id == id);

        public static SceneDef Parse(string id, Dictionary<string, object> d)
        {
            var s = new SceneDef
            {
                Id = id,
                Title = J.Str(d, "title") ?? id,
                Type = J.Str(d, "type") ?? "play",
                Background = J.Str(d, "background"),
                AntonioHeight = J.Float(d, "antonioHeight", 50),
                StartX = J.Float(J.Dict(J.Get(d, "start")), "x", 50),
                Intro = ActionDef.ParseList(J.Get(d, "intro")),
                PanelSeconds = J.Float(d, "panelSeconds", 3.2f),
                CrossfadeSeconds = J.Float(d, "crossfadeSeconds", 1.2f),
                Then = J.Str(d, "then"),
            };

            var rd = J.Dict(J.Get(d, "restrictWhenFlag"));
            if (rd.Count > 0)
                s.Restriction = new RestrictionDef
                {
                    Flag = J.Str(rd, "flag"),
                    Say = J.Str(rd, "say"),
                    Allow = J.StrList(J.Get(rd, "allow")),
                    NoWalk = J.Bool(rd, "noWalk"),
                };

            var fl = J.Dict(J.Get(d, "floor"));
            if (fl.Count > 0)
            {
                var nx = J.FloatList(J.Get(fl, "nearX"));
                var fx = J.FloatList(J.Get(fl, "farX"));
                s.Floor = new FloorDef
                {
                    NearY = J.Float(fl, "nearY", 2),
                    FarY = J.Float(fl, "farY", 2),
                    FarScale = J.Float(fl, "farScale", 1),
                    NearXMin = nx.Count > 0 ? nx[0] : 6,
                    NearXMax = nx.Count > 1 ? nx[1] : 94,
                    FarXMin = fx.Count > 0 ? fx[0] : 6,
                    FarXMax = fx.Count > 1 ? fx[1] : 94,
                };
            }

            foreach (var f in J.List(J.Get(d, "foreground")))
            {
                var fd = J.Dict(f);
                s.Foreground.Add(new ForegroundDef
                {
                    Id = J.Str(fd, "id"),
                    Image = J.Str(fd, "image"),
                    Rect = J.Rect(J.Get(fd, "rect")),
                    Depth = J.Float(fd, "depth", 100),
                });
            }

            foreach (var h in J.List(J.Get(d, "hotspots")))
                s.Hotspots.Add(HotspotDef.Parse(J.Dict(h)));

            foreach (var kv in J.Dict(J.Get(d, "puzzles")))
                s.Puzzles[kv.Key] = PuzzleDef.Parse(kv.Key, J.Dict(kv.Value));

            foreach (var o in J.List(J.Get(d, "objectives")))
            {
                var od = J.Dict(o);
                s.Objectives.Add(new ObjectiveDef
                {
                    If = Condition.Parse(J.Get(od, "if")),
                    Target = J.Str(od, "target"),
                    Item = J.Str(od, "item"),
                    Text = J.Str(od, "text") ?? "",
                });
            }

            foreach (var p in J.List(J.Get(d, "panels")))
            {
                var pd = J.Dict(p);
                s.Panels.Add(new PanelDef { Image = J.Str(pd, "image"), Caption = J.Str(pd, "caption") ?? "" });
            }
            return s;
        }
    }

    public class HotspotDef
    {
        public string Id, Label, Kind;
        public StageRect Rect;
        public Dictionary<string, StageRect> RectWhenFlag = new Dictionary<string, StageRect>();
        public float? Walk;
        /// <summary>Flag → walk alternativo (null = não anda).</summary>
        public Dictionary<string, float?> WalkWhenFlag = new Dictionary<string, float?>();
        public Condition Show;
        public List<RuleDef> Tap = new List<RuleDef>();
        public Dictionary<string, List<RuleDef>> Use = new Dictionary<string, List<RuleDef>>();
        public PatchDef Patch;

        public bool IsNpc => Kind == "npc";
        public bool IsProp => Kind == "prop";

        public static HotspotDef Parse(Dictionary<string, object> d)
        {
            var h = new HotspotDef
            {
                Id = J.Str(d, "id"),
                Label = J.Str(d, "label"),
                Kind = J.Str(d, "kind"),
                Rect = J.Rect(J.Get(d, "rect")),
                Walk = J.NullableFloat(J.Get(d, "walk")),
                Show = Condition.Parse(J.Get(d, "show")),
                Tap = RuleDef.ParseList(J.Get(d, "tap")),
            };
            if (h.Label == null) h.Label = h.Id;

            foreach (var kv in J.Dict(J.Get(d, "rectWhenFlag")))
                h.RectWhenFlag[kv.Key] = J.Rect(kv.Value);
            foreach (var kv in J.Dict(J.Get(d, "walkWhenFlag")))
                h.WalkWhenFlag[kv.Key] = J.NullableFloat(kv.Value);
            foreach (var kv in J.Dict(J.Get(d, "use")))
                h.Use[kv.Key] = RuleDef.ParseList(kv.Value);

            var pd = J.Dict(J.Get(d, "patchWhenTaken"));
            if (pd.Count > 0)
                h.Patch = new PatchDef { Flag = J.Str(pd, "flag"), Image = J.Str(pd, "image"), Rect = J.Rect(J.Get(pd, "rect")) };
            return h;
        }
    }

    /// <summary>
    /// Recorte do cenário desenhado na frente dos personagens que estão atrás dele (ex.: mesa na frente da professora).
    /// Depth = base (% medido do topo) do personagem que fica atrás; o recorte cobre quem tem base até esse valor.
    /// </summary>
    public class ForegroundDef
    {
        public string Id, Image;
        public StageRect Rect;
        public float Depth = 100;
    }

    public class PatchDef
    {
        public string Flag, Image;
        public StageRect Rect;
    }

    /// <summary>Condição de regra: flags ligadas/desligadas e itens no inventário. Null = sempre verdadeira.</summary>
    public class Condition
    {
        public List<string> Flags = new List<string>();
        public List<string> NotFlags = new List<string>();
        public List<string> Has = new List<string>();
        public List<string> NotHas = new List<string>();

        public static Condition Parse(object o)
        {
            var d = J.Dict(o);
            if (d.Count == 0) return null;
            return new Condition
            {
                Flags = J.StrList(J.Get(d, "flags")),
                NotFlags = J.StrList(J.Get(d, "notFlags")),
                Has = J.StrList(J.Get(d, "has")),
                NotHas = J.StrList(J.Get(d, "notHas")),
            };
        }
    }

    public class RuleDef
    {
        public Condition If;
        public List<ActionDef> Do = new List<ActionDef>();

        public static List<RuleDef> ParseList(object o)
        {
            var list = new List<RuleDef>();
            foreach (var r in J.List(o))
            {
                var d = J.Dict(r);
                list.Add(new RuleDef { If = Condition.Parse(J.Get(d, "if")), Do = ActionDef.ParseList(J.Get(d, "do")) });
            }
            return list;
        }
    }

    /// <summary>
    /// Uma ação do roteiro, ex.: { "say": ["caio", "Oi"] }. Type = nome da ação, Value = valor bruto do JSON.
    /// Ações conhecidas: say, set, unset, give, walkTo, goScene, banner, wait, npcMove, lift, face, openPuzzle, sayMissing.
    /// </summary>
    public class ActionDef
    {
        public string Type;
        public object Value;
        /// <summary>Opcional: espera também este tempo (ms) em paralelo com a ação (ex.: walkTo + passo do Caio).</summary>
        public float? ParallelWaitMs;

        public string StringValue => Value as string;
        public List<object> ListValue => Value as List<object> ?? new List<object>();
        public Dictionary<string, object> DictValue => Value as Dictionary<string, object>;
        public float FloatValue => J.ToFloat(Value);
        public string Arg(int i) => i < ListValue.Count ? ListValue[i] as string : null;
        public float? FloatArg(int i) => i < ListValue.Count ? J.NullableFloat(ListValue[i]) : null;

        public static List<ActionDef> ParseList(object o)
        {
            var list = new List<ActionDef>();
            foreach (var a in J.List(o))
            {
                var d = J.Dict(a);
                var act = new ActionDef { ParallelWaitMs = J.NullableFloat(J.Get(d, "parallelWait")) };
                foreach (var kv in d)
                {
                    if (kv.Key == "parallelWait") continue;
                    act.Type = kv.Key;
                    act.Value = kv.Value;
                    break;
                }
                if (act.Type != null) list.Add(act);
            }
            return list;
        }

        public override string ToString() => Type;
    }

    public class ObjectiveDef
    {
        public Condition If;
        public string Target, Item, Text;
    }

    public class PuzzleDef
    {
        public string Id;
        public List<ProblemDef> Problems = new List<ProblemDef>();
        public string MsgCorrect = "Isso!", MsgAllCorrect = "Pronto!", MsgWrong = "Hm... não.";
        public bool KeepsAnswersWhenClosed = true;
        public List<ActionDef> OnShow = new List<ActionDef>();

        public static PuzzleDef Parse(string id, Dictionary<string, object> d)
        {
            var p = new PuzzleDef
            {
                Id = id,
                KeepsAnswersWhenClosed = !d.ContainsKey("keepsAnswersWhenClosed") || J.Bool(d, "keepsAnswersWhenClosed"),
                OnShow = ActionDef.ParseList(J.Get(d, "onShow")),
            };
            var m = J.Dict(J.Get(d, "messages"));
            p.MsgCorrect = J.Str(m, "correct") ?? p.MsgCorrect;
            p.MsgAllCorrect = J.Str(m, "allCorrect") ?? p.MsgAllCorrect;
            p.MsgWrong = J.Str(m, "wrong") ?? p.MsgWrong;
            foreach (var o in J.List(J.Get(d, "problems")))
            {
                var pd = J.Dict(o);
                var prob = new ProblemDef { Q = J.Str(pd, "q"), A = (int)J.Float(pd, "a", 0), Hint = J.Str(pd, "hint") ?? "" };
                foreach (var v in J.FloatList(J.Get(pd, "options"))) prob.Options.Add((int)v);
                p.Problems.Add(prob);
            }
            return p;
        }
    }

    public class ProblemDef
    {
        public string Q, Hint;
        public int A;
        public List<int> Options = new List<int>();
    }

    public class PanelDef
    {
        public string Image, Caption;
    }

    /// <summary>Atalhos para ler o resultado do MiniJson com segurança.</summary>
    static class J
    {
        static readonly Dictionary<string, object> EmptyDict = new Dictionary<string, object>();
        static readonly List<object> EmptyList = new List<object>();

        public static object Get(Dictionary<string, object> d, string key) =>
            d != null && d.TryGetValue(key, out var v) ? v : null;

        public static Dictionary<string, object> Dict(object o) => o as Dictionary<string, object> ?? EmptyDict;
        public static List<object> List(object o) => o as List<object> ?? EmptyList;
        public static string Str(Dictionary<string, object> d, string key) => Get(d, key) as string;
        public static bool Bool(Dictionary<string, object> d, string key) => Get(d, key) is bool b && b;

        public static float ToFloat(object o)
        {
            switch (o)
            {
                case double d: return (float)d;
                case float f: return f;
                case int i: return i;
                case long l: return l;
                case string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r): return r;
                default: return 0f;
            }
        }

        public static float? NullableFloat(object o) => o == null ? (float?)null : ToFloat(o);

        public static float Float(Dictionary<string, object> d, string key, float fallback)
        {
            var v = Get(d, key);
            return v == null ? fallback : ToFloat(v);
        }

        public static List<string> StrList(object o)
        {
            var list = new List<string>();
            foreach (var v in List(o)) if (v is string s) list.Add(s);
            return list;
        }

        public static List<float> FloatList(object o)
        {
            var list = new List<float>();
            foreach (var v in List(o)) list.Add(ToFloat(v));
            return list;
        }

        public static StageRect Rect(object o)
        {
            var f = FloatList(o);
            return f.Count >= 4 ? new StageRect(f[0], f[1], f[2], f[3]) : new StageRect();
        }
    }
}
