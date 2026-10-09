using System;
using System.Collections;
using System.Collections.Generic;
using RecreioEspacial.Data;
using RecreioEspacial.UI;
using RecreioEspacial.World;
using UnityEngine;

namespace RecreioEspacial.Core
{
    /// <summary>
    /// Maestro do jogo. Carrega o ep01.json, monta câmera/mundo/UI, recebe a entrada,
    /// interpreta as regras e ações do roteiro (como corrotinas em sequência), cuida das dicas,
    /// do caderno, das trocas de cena e da cutscene final.
    ///
    /// Ele se cria sozinho ao apertar Play em qualquer cena (ver AutoBoot). Para mudar as opções
    /// de teste no Inspector, crie um GameObject vazio numa cena e adicione este componente.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Teste")]
        [Tooltip("Cena inicial: title, sala, portao, quadra ou final")]
        public string startScene = "title";
        [Tooltip("Desenha o contorno de todos os hotspots (atalho F1 no Editor)")]
        public bool showHotspots;

        [Header("Dicas")]
        [Tooltip("Segundos sem entrada até a dica automática nível 1 (0 desliga)")]
        [Range(0, 120)] public int autoHintSeconds = 30;

        EpisodeData ep;
        readonly GameState state = new GameState();
        Camera cam, barsCam;
        SceneView view;
        AntonioController antonio;
        GameUI ui;
        SceneDef currentScene;

        // Balão
        string bubbleWho, bubbleText;
        bool bubbleActive, skipRequested;

        string banner;
        float fadeAlpha;
        bool lastInputKeyboard;

        // Caderno
        string puzzleId;
        string puzzleMessage = "";

        // ---------------------------------------------------------------------
        // Inicialização
        // ---------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
#if UNITY_2023_1_OR_NEWER
            bool exists = FindAnyObjectByType<GameController>() != null;
#else
            bool exists = FindObjectOfType<GameController>() != null;
#endif
            if (!exists) new GameObject("Recreio Espacial").AddComponent<GameController>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            // Mobile: só paisagem (o palco é 16:9).
            UnityEngine.Screen.autorotateToPortrait = false;
            UnityEngine.Screen.autorotateToPortraitUpsideDown = false;
            UnityEngine.Screen.autorotateToLandscapeLeft = true;
            UnityEngine.Screen.autorotateToLandscapeRight = true;
            UnityEngine.Screen.orientation = ScreenOrientation.AutoRotation;

            var json = Resources.Load<TextAsset>(GameAssets.EpisodeJson);
            if (json == null)
            {
                Debug.LogError($"Não achei Resources/{GameAssets.EpisodeJson}.json");
                enabled = false;
                return;
            }
            ep = EpisodeData.FromJson(json.text);

            SetupCameras();
            var world = new GameObject("Mundo").transform;
            view = SceneView.Create(world, ep, state);
            antonio = AntonioController.Create(world,
                GameAssets.Sprite(ep.Characters["antonio"].SpriteFront, new Vector2(0.5f, 0f)),
                GameAssets.Sprite(ep.Characters["antonio"].SpriteSide, new Vector2(0.5f, 0f)),
                ep.Characters["antonio"].RigSide,
                GameAssets.Sprite(ep.Characters["antonio"].SpriteFrontHolding, new Vector2(0.5f, 0f)),
                ep.Characters["antonio"].HoldingRigPart);
            antonio.gameObject.SetActive(false);

            ui = GameUI.Create(cam);
            ui.PlayClicked += OnPlay;
            ui.ContinueClicked += OnContinue;
            ui.ReplayClicked += ShowTitle;
            ui.HintClicked += OnHint;
            ui.BubbleClicked += () => { state.IdleSeconds = 0; skipRequested = bubbleActive; };
            ui.ItemClicked += OnItemClicked;
            ui.PuzzleOptionClicked += OnPuzzleOption;
            ui.PuzzleHintClicked += OnPuzzleHint;
            ui.PuzzleCloseClicked += ClosePuzzle;
            ui.PuzzleShowClicked += OnPuzzleShow;
        }

        void Start()
        {
            if (!enabled) return;
            if (string.IsNullOrEmpty(startScene) || startScene == "title") ShowTitle();
            else StartAt(startScene);
        }

        void SetupCameras()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = Stage.Height / 2f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Tokens.Ink;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;

            // Câmera de fundo só para pintar as tarjas fora do 16:9.
            var bars = new GameObject("Tarjas");
            barsCam = bars.AddComponent<Camera>();
            barsCam.clearFlags = CameraClearFlags.SolidColor;
            barsCam.backgroundColor = Tokens.Ink;
            barsCam.cullingMask = 0;
            barsCam.depth = cam.depth - 10;
            barsCam.orthographic = true;
            UpdateLetterbox();
        }

        void UpdateLetterbox()
        {
            float screenAspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
            if (screenAspect > Stage.Aspect)
            {
                float w = Stage.Aspect / screenAspect;
                cam.rect = new Rect((1f - w) / 2f, 0f, w, 1f);
            }
            else
            {
                float h = screenAspect / Stage.Aspect;
                cam.rect = new Rect(0f, (1f - h) / 2f, 1f, h);
            }
        }

        // ---------------------------------------------------------------------
        // Fluxo de telas
        // ---------------------------------------------------------------------

        bool InPlayScene => currentScene != null && !currentScene.IsCutscene && state.Scene == currentScene.Id;

        void ShowTitle()
        {
            StopAllCoroutines();
            state.Busy = false;
            ResetBubble();
            ClosePuzzle();
            state.ResetProgress();
            state.Scene = "title";
            currentScene = null;
            view.Clear();
            antonio.gameObject.SetActive(false);
            fadeAlpha = 0;
            ui.SetFade(0);
            ui.SetContinueAvailable(GameState.HasSave);
            ui.ShowScreen(GameUI.UiScreen.Title);
        }

        void OnPlay()
        {
            if (state.Busy) return;
            state.ResetProgress();
            GameState.DeleteSave();
            RunSequence(GoScene("sala"));
        }

        void OnContinue()
        {
            if (state.Busy) return;
            if (!state.Load() || !ep.Scenes.ContainsKey(state.Scene)) { OnPlay(); return; }
            RunSequence(GoScene(state.Scene));
        }

        /// <summary>Começa direto numa cena com o estado de teste do JSON (debugStartStates).</summary>
        void StartAt(string sceneId)
        {
            StopAllCoroutines();
            state.Busy = false;
            ResetBubble();
            ClosePuzzle();
            state.ResetProgress();
            string key = sceneId == "final" ? "quadra" : sceneId;
            if (ep.DebugStartStates.TryGetValue(key, out var dbg))
            {
                foreach (var f in dbg.Flags) state.SetFlag(f);
                foreach (var i in dbg.Inventory) state.Give(i);
            }
            if (!ep.Scenes.ContainsKey(sceneId))
            {
                Debug.LogWarning($"Cena '{sceneId}' não existe no JSON. Indo para o título.");
                ShowTitle();
                return;
            }
            RunSequence(GoScene(sceneId));
        }

        IEnumerator GoScene(string id)
        {
            if (!ep.Scenes.TryGetValue(id, out var scene))
            {
                Debug.LogError($"goScene: cena '{id}' não existe.");
                yield break;
            }
            yield return FadeTo(1f, 0.5f);
            yield return Wait(0.05f);
            SetupScene(scene);
            yield return Wait(0.15f);
            yield return FadeTo(0f, 0.45f);
            state.IdleSeconds = 0;
            if (scene.IsCutscene) yield return RunCutscene(scene);
            else yield return RunActions(scene.Intro);
        }

        void SetupScene(SceneDef scene)
        {
            currentScene = scene;
            state.Scene = scene.Id;
            state.HintLevel = 0;
            state.SelectedItem = null;
            banner = null;
            ResetBubble();
            ClosePuzzle();

            // Largura da cena: cenário largo (câmera acompanha) ou do tamanho da tela.
            float sceneWidth = Stage.ScreenWidth;
            if (scene.Pan)
            {
                var bg = GameAssets.Sprite(scene.Background);
                if (bg != null) sceneWidth = Stage.Height * bg.rect.width / bg.rect.height;
            }
            Stage.SetSceneWidth(scene.IsCutscene ? Stage.ScreenWidth : sceneWidth);
            Stage.CameraX = 0f;

            if (scene.IsCutscene)
            {
                view.Clear();
                antonio.gameObject.SetActive(false);
                ui.SetupCutscene(scene.Panels);
                ui.ShowScreen(GameUI.UiScreen.Cutscene);
                return;
            }

            view.Build(scene);
            antonio.gameObject.SetActive(true);
            antonio.EnterScene(scene);
            if (scene.Sit != null) antonio.Sit(scene.Sit.X, scene.Sit.SeatY, scene.Sit.Dir);
            Stage.CameraX = CameraTarget();
            UpdateCamera(0f);
            ui.ShowScreen(GameUI.UiScreen.Play);
            ui.SetSceneTitle(scene.Title);
            // Ponto de salvamento: "Continuar" recomeça esta cena com o progresso até aqui.
            state.Save();
        }

        IEnumerator RunCutscene(SceneDef scene)
        {
            for (int i = 0; i < scene.Panels.Count; i++)
            {
                ui.SetCaption(scene.Panels[i].Caption);
                StartCoroutine(FadePanel(i, scene.CrossfadeSeconds));
                yield return Wait(scene.PanelSeconds);
            }
            yield return Wait(0.8f);
            yield return FadeTo(1f, 0.6f);
            state.Scene = "end";
            currentScene = null;
            GameState.DeleteSave();
            ui.SetEndTime(state.PlaySeconds);
            ui.ShowScreen(GameUI.UiScreen.End);
            yield return FadeTo(0f, 0.5f);
        }

        IEnumerator FadePanel(int index, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                ui.SetPanelAlpha(index, k * k * (3f - 2f * k));
                yield return null;
            }
            ui.SetPanelAlpha(index, 1f);
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            float start = fadeAlpha;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                fadeAlpha = Mathf.Lerp(start, target, t / seconds);
                ui.SetFade(fadeAlpha);
                yield return null;
            }
            fadeAlpha = target;
            ui.SetFade(fadeAlpha);
        }

        // ---------------------------------------------------------------------
        // Sequências (corrotinas): enquanto rodam, o jogo está "busy"
        // ---------------------------------------------------------------------

        bool RunSequence(IEnumerator routine)
        {
            if (state.Busy) return false;
            StartCoroutine(SequenceRoutine(routine));
            return true;
        }

        IEnumerator SequenceRoutine(IEnumerator routine)
        {
            state.Busy = true;
            try
            {
                // Executa corrotinas aninhadas na mão: um erro numa ação é registrado sem travar o jogo.
                var stack = new Stack<IEnumerator>();
                stack.Push(routine);
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    bool moved;
                    try { moved = top.MoveNext(); }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        stack.Pop();
                        continue;
                    }
                    if (!moved) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return top.Current;
                }
            }
            finally
            {
                state.Busy = false;
            }
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }

        // ---------------------------------------------------------------------
        // Regras e ações do roteiro
        // ---------------------------------------------------------------------

        RuleDef FirstMatch(List<RuleDef> rules)
        {
            if (rules == null) return null;
            foreach (var r in rules) if (state.Check(r.If)) return r;
            return null;
        }

        IEnumerator RunActions(List<ActionDef> actions)
        {
            if (actions == null) yield break;
            foreach (var a in actions) yield return RunAction(a);
        }

        IEnumerator RunAction(ActionDef a)
        {
            switch (a.Type)
            {
                case "say":
                    yield return Say(a.Arg(0), a.Arg(1));
                    break;

                case "set":
                    if (a.StringValue != null) state.SetFlag(a.StringValue);
                    else if (a.DictValue != null)
                        foreach (var kv in a.DictValue) state.SetFlag(kv.Key, FlagString(kv.Value));
                    else foreach (var o in a.ListValue) state.SetFlag(o as string);
                    break;

                case "unset":
                    if (a.StringValue != null) state.UnsetFlag(a.StringValue);
                    else foreach (var o in a.ListValue) state.UnsetFlag(o as string);
                    break;

                case "give":
                    state.Give(a.StringValue);
                    break;

                case "walkTo":
                {
                    float x = a.FloatArg(0) ?? antonio.X;
                    float startTime = Time.time;
                    yield return WalkTo(x, a.FloatArg(1));
                    if (a.ParallelWaitMs.HasValue)
                        while (Time.time - startTime < a.ParallelWaitMs.Value / 1000f) yield return null;
                    break;
                }

                case "goScene":
                    yield return GoScene(a.StringValue);
                    break;

                case "banner":
                    banner = a.StringValue;
                    break;

                case "wait":
                    yield return Wait(a.FloatValue / 1000f);
                    break;

                case "npcMove":
                {
                    var npc = view.Npc(a.Arg(0));
                    if (npc != null) npc.StartMove((a.FloatArg(1) ?? 1000f) / 1000f);
                    break;
                }

                case "lift":
                    antonio.Lift = a.FloatValue;
                    break;

                case "face":
                    antonio.Dir = a.FloatValue < 0 ? -1 : 1;
                    break;

                case "openPuzzle":
                    OpenPuzzle(a.StringValue);
                    break;

                case "sayMissing":
                    yield return Say("antonio", MissingText(a.ListValue));
                    break;

                default:
                    Debug.LogWarning($"Ação desconhecida no roteiro: '{a.Type}'");
                    break;
            }
        }

        static string FlagString(object v)
        {
            switch (v)
            {
                case null: return null;
                case bool b: return b ? "true" : null;
                case string s: return s;
                default: return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        string MissingText(List<object> ids)
        {
            var missing = new List<string>();
            foreach (var o in ids)
            {
                if (!(o is string id) || state.Flag(id) || state.Has(id)) continue;
                if (ep.Items.TryGetValue(id, out var item))
                    missing.Add(((item.Article != null ? item.Article + " " : "") + item.Label).ToLowerInvariant());
                else missing.Add(id);
            }
            if (missing.Count == 0) return "Já tenho tudo.";
            string list = missing.Count == 1
                ? missing[0]
                : string.Join(", ", missing.GetRange(0, missing.Count - 1)) + " e " + missing[missing.Count - 1];
            return "Ainda falta " + list + ".";
        }

        IEnumerator Say(string who, string text)
        {
            text = text ?? "";
            float duration = Mathf.Max(1.6f, text.Length * 0.06f);
            bubbleWho = who;
            bubbleText = text;
            bubbleActive = true;
            skipRequested = false;
            UpdateBubble();
            for (float t = 0; t < duration && !skipRequested; t += Time.deltaTime) yield return null;
            ResetBubble();
            yield return Wait(0.15f);
        }

        void ResetBubble()
        {
            bubbleActive = false;
            skipRequested = false;
            bubbleWho = null;
            if (ui != null) ui.HideBubble();
        }

        IEnumerator WalkTo(float x, float? y)
        {
            if (antonio.Seated)
            {
                antonio.StandUp();
                yield return Wait(0.25f);
            }
            int id = antonio.MoveTo(x, y);
            while (antonio.IsMovingTo(id)) yield return null;
        }

        // ---------------------------------------------------------------------
        // Interação com hotspots, chão e inventário
        // ---------------------------------------------------------------------

        bool Restricted => currentScene?.Restriction != null && state.Flag(currentScene.Restriction.Flag);

        void OnHotspotClicked(HotspotDef h)
        {
            state.IdleSeconds = 0;
            if (state.Busy)
            {
                skipRequested = bubbleActive;
                return;
            }
            RunSequence(Interact(h, state.SelectedItem));
        }

        IEnumerator Interact(HotspotDef h, string item)
        {
            if (Restricted && !currentScene.Restriction.Allow.Contains(h.Id))
            {
                state.SelectedItem = null;
                yield return Say("antonio", currentScene.Restriction.Say);
                yield break;
            }
            banner = null;

            float? walk = view.WalkOf(h);
            bool fromChair = antonio.Seated && currentScene.Sit != null && currentScene.Sit.Reach.Contains(h.Id);
            if (walk.HasValue && !fromChair) yield return WalkTo(walk.Value, null);
            else antonio.StopMove();

            float cx = view.RectOf(h).CenterX;
            if (!antonio.Seated && Mathf.Abs(cx - antonio.X) > 2f) antonio.Dir = cx < antonio.X ? -1 : 1;

            if (item != null)
            {
                state.SelectedItem = null;
                RuleDef rule = null;
                if (h.Use.TryGetValue(item, out var rules)) rule = FirstMatch(rules);
                if (rule != null) yield return RunActions(rule.Do);
                else if (ep.FailLines.Count > 0)
                    yield return Say("antonio", ep.FailLines[UnityEngine.Random.Range(0, ep.FailLines.Count)]);
            }
            else
            {
                var rule = FirstMatch(h.Tap);
                if (rule != null) yield return RunActions(rule.Do);
            }
            state.HintLevel = 0;
        }

        void OnGroundClicked(float x, float yFromBottom)
        {
            if (state.Busy || ui.PuzzleOpen) return;
            if (Restricted && currentScene.Restriction.NoWalk) return;
            state.SelectedItem = null;
            antonio.StandUp();
            antonio.MoveTo(x, yFromBottom);
        }

        void OnItemClicked(int index)
        {
            state.IdleSeconds = 0;
            if (state.Busy || !InPlayScene || index < 0 || index >= state.Inventory.Count) return;
            string id = state.Inventory[index];
            state.SelectedItem = state.SelectedItem == id ? null : id;
            banner = null;
        }

        /// <summary>Hotspot que o teclado alcança: o de ponto de parada (walk) mais perto do Antônio.</summary>
        HotspotDef ReachableHotspot()
        {
            if (!InPlayScene) return null;
            HotspotDef best = null;
            float bestScore = float.MaxValue;
            const float reach = 9f;
            foreach (var h in currentScene.Hotspots)
            {
                if (!view.IsVisible(h)) continue;
                if (Restricted)
                {
                    if (!currentScene.Restriction.Allow.Contains(h.Id)) continue;
                }
                else if (!view.WalkOf(h).HasValue) continue; // ex.: céu — só com mouse/toque

                var r = view.RectOf(h);
                float target = view.WalkOf(h) ?? r.CenterX;
                float d = Mathf.Abs(target - antonio.X) * Stage.WidthFactor; // em % da tela
                if (!Restricted && d > reach) continue;
                float score = d + r.W * r.H * 0.0005f; // empate: o objeto menor (mais específico)
                if (score < bestScore)
                {
                    best = h;
                    bestScore = score;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------------
        // Dicas
        // ---------------------------------------------------------------------

        ObjectiveDef CurrentObjective()
        {
            if (!InPlayScene) return null;
            foreach (var o in currentScene.Objectives) if (state.Check(o.If)) return o;
            return null;
        }

        void OnHint()
        {
            state.IdleSeconds = 0;
            var o = CurrentObjective();
            if (o == null) return;
            int level = Mathf.Min(3, state.HintLevel + 1);
            state.HintLevel = level;
            if (level == 1 && !state.Busy) RunSequence(Say("antonio", "Hm?"));
            if (level == 2) banner = FirstSentence(o.Text);
            if (level == 3) banner = o.Text;
        }

        static string FirstSentence(string text)
        {
            int i = text.IndexOf('.');
            return i < 0 ? text : text.Substring(0, i + 1);
        }

        // ---------------------------------------------------------------------
        // Caderno (puzzle das contas)
        // ---------------------------------------------------------------------

        PuzzleDef CurrentPuzzle =>
            puzzleId != null && currentScene != null && currentScene.Puzzles.TryGetValue(puzzleId, out var p) ? p : null;

        void OpenPuzzle(string id)
        {
            puzzleId = id;
            var def = CurrentPuzzle;
            if (def == null)
            {
                Debug.LogWarning($"openPuzzle: puzzle '{id}' não existe na cena.");
                puzzleId = null;
                return;
            }
            var answers = state.AnswersFor(id, def.Problems.Count);
            if (!def.KeepsAnswersWhenClosed) Array.Clear(answers, 0, answers.Length);
            puzzleMessage = "";
            ui.ShowPuzzle(def, answers, puzzleMessage);
        }

        void OnPuzzleOption(int row, int value)
        {
            state.IdleSeconds = 0;
            var def = CurrentPuzzle;
            if (def == null || row < 0 || row >= def.Problems.Count) return;
            var answers = state.AnswersFor(def.Id, def.Problems.Count);
            if (value == def.Problems[row].A)
            {
                answers[row] = value;
                bool all = Array.TrueForAll(answers, x => x != null);
                puzzleMessage = all ? def.MsgAllCorrect : def.MsgCorrect;
            }
            else puzzleMessage = def.MsgWrong;
            ui.ShowPuzzle(def, answers, puzzleMessage);
        }

        void OnPuzzleHint()
        {
            state.IdleSeconds = 0;
            var def = CurrentPuzzle;
            if (def == null) return;
            var answers = state.AnswersFor(def.Id, def.Problems.Count);
            int i = Array.FindIndex(answers, x => x == null);
            if (i < 0) return;
            puzzleMessage = def.Problems[i].Hint;
            ui.ShowPuzzle(def, answers, puzzleMessage);
        }

        void ClosePuzzle()
        {
            puzzleId = null;
            if (ui != null) ui.ClosePuzzle();
        }

        void OnPuzzleShow()
        {
            var def = CurrentPuzzle;
            ClosePuzzle();
            if (def == null) return;
            RunSequence(ShowPuzzleRoutine(def));
        }

        IEnumerator ShowPuzzleRoutine(PuzzleDef def)
        {
            yield return RunActions(def.OnShow);
            state.HintLevel = 0;
        }

        // ---------------------------------------------------------------------
        // Loop: entrada e atualização da UI
        // ---------------------------------------------------------------------

        void Update()
        {
            UpdateLetterbox();
            HandleDebugKeys();
            if (state.Scene != "title" && state.Scene != "end") state.PlaySeconds += Time.unscaledDeltaTime;

            if (GameInput.PointerPressed(out var screenPos))
            {
                state.IdleSeconds = 0;
                lastInputKeyboard = false;
                if (!ui.IsPointerOverUI(screenPos)) OnWorldPointer(screenPos);
            }
            if (GameInput.AnyKeyDown()) state.IdleSeconds = 0;

            if (InPlayScene) HandleKeyboard();
            else antonio.SetKeyboardInput(Vector2.zero);

            // Dica automática
            if (InPlayScene && !state.Busy && !ui.PuzzleOpen)
            {
                state.IdleSeconds += Time.deltaTime;
                if (autoHintSeconds > 0 && state.IdleSeconds >= autoHintSeconds && state.HintLevel == 0) state.HintLevel = 1;
            }
        }

        void OnWorldPointer(Vector2 screenPos)
        {
            if (bubbleActive) { skipRequested = true; return; }
            if (!InPlayScene || state.Busy || ui.PuzzleOpen) return;
            var p = Stage.ScreenToStage(cam, screenPos);
            if (p.x < 0 || p.x > 100 || p.y < 0 || p.y > 100) return;
            var h = view.HitTest(p.x, 100f - p.y);
            if (h != null) OnHotspotClicked(h);
            else OnGroundClicked(p.x, p.y);
        }

        void HandleKeyboard()
        {
            bool canMove = !state.Busy && !ui.PuzzleOpen && !(Restricted && currentScene.Restriction.NoWalk);
            var axis = canMove ? GameInput.MoveAxis() : Vector2.zero;
            if (axis.sqrMagnitude > 0f)
            {
                antonio.StandUp();
                lastInputKeyboard = true;
                state.IdleSeconds = 0;
            }
            antonio.SetKeyboardInput(axis);

            if (GameInput.InteractPressed)
            {
                lastInputKeyboard = true;
                if (bubbleActive) skipRequested = true;
                else if (!state.Busy && !ui.PuzzleOpen)
                {
                    var h = ReachableHotspot();
                    if (h != null) OnHotspotClicked(h);
                }
            }
            if (GameInput.HintPressed && !ui.PuzzleOpen) OnHint();
            if (GameInput.CancelPressed)
            {
                if (ui.PuzzleOpen) ClosePuzzle();
                else state.SelectedItem = null;
            }
            int item = GameInput.ItemKeyPressed();
            if (item >= 0 && !ui.PuzzleOpen)
            {
                lastInputKeyboard = true;
                OnItemClicked(item);
            }
        }

        void HandleDebugKeys()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (GameInput.DebugKeyDown(1)) showHotspots = !showHotspots;
            if (GameInput.DebugKeyDown(5)) StartAt("sala");
            if (GameInput.DebugKeyDown(6)) StartAt("portao");
            if (GameInput.DebugKeyDown(7)) StartAt("quadra");
            if (GameInput.DebugKeyDown(8)) StartAt("final");
        }

        float CameraTarget() =>
            Mathf.Clamp(Stage.X(antonio.X), -Stage.MaxCameraX, Stage.MaxCameraX);

        /// <summary>Câmera segue o Antônio suavemente em cenários largos.</summary>
        void UpdateCamera(float dt)
        {
            if (!InPlayScene) Stage.CameraX = 0f;
            else if (dt <= 0f) Stage.CameraX = CameraTarget();
            else Stage.CameraX += (CameraTarget() - Stage.CameraX) * Mathf.Min(1f, dt * 3.5f);
            cam.transform.position = new Vector3(Stage.CameraX, 0f, -10f);
        }

        void LateUpdate()
        {
            if (ui == null || view == null) return;
            UpdateCamera(Time.deltaTime);
            view.ShowDebugOutlines = showHotspots;
            var antDef = ep.Characters["antonio"];
            antonio.Holding = antDef.HoldingItem == null || state.Has(antDef.HoldingItem);

            if (!InPlayScene) return;

            // Pulso de dica: alvo do objetivo (com dica ≥ 1) ou a flag "pulse" do roteiro.
            var objective = CurrentObjective();
            string pulseId = state.HintLevel >= 1 && objective != null ? objective.Target : state.FlagValue("pulse");
            view.SetPulse(pulseId);

            string pulseItem = state.HintLevel >= 3 && objective != null ? objective.Item : null;
            ui.SetInventory(state.Inventory, ep, state.SelectedItem, pulseItem);
            ui.SetHintLevel(state.HintLevel);
            ui.SetBanner(banner);

            // Destaque do teclado / rótulo de hover
            string hoverLabel = null;
            HotspotDef keyTarget = null;
            if (lastInputKeyboard && !state.Busy && !ui.PuzzleOpen)
            {
                keyTarget = ReachableHotspot();
                if (keyTarget != null) hoverLabel = "E · " + keyTarget.Label;
            }
            else if (!ui.PuzzleOpen && GameInput.HoverPosition(out var mouse) && !ui.IsPointerOverUI(mouse))
            {
                var p = Stage.ScreenToStage(cam, mouse);
                var h = view.HitTest(p.x, 100f - p.y);
                if (h != null) hoverLabel = h.Label;
            }
            view.SetHighlight(keyTarget?.Id);
            ui.SetHover(bubbleActive ? null : hoverLabel);

            foreach (var npc in view.Npcs)
            {
                npc.AntonioX = antonio.X;
                npc.Talking = bubbleActive && bubbleWho == npc.Id;
            }
            if (bubbleActive) UpdateBubble();
        }

        /// <summary>Posiciona o balão acima de quem fala (Antônio, NPC, ou no alto se for só voz).</summary>
        void UpdateBubble()
        {
            if (!bubbleActive || !InPlayScene) return;
            float x = 50f, b = 80f;
            if (bubbleWho == "antonio")
            {
                x = antonio.X;
                b = antonio.TopPct + 1f;
            }
            else
            {
                var h = view.FindVisible(bubbleWho);
                if (h != null)
                {
                    var r = view.RectOf(h);
                    x = r.CenterX;
                    b = r.TopFromBottom + 1f;
                }
            }
            ui.ShowBubble(ep.NameOf(bubbleWho), bubbleText, Mathf.Clamp(Stage.ToScreenPct(x), 17f, 83f), Mathf.Min(76f, b));
        }
    }
}
