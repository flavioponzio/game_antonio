using System;
using System.Collections.Generic;
using RecreioEspacial.Core;
using RecreioEspacial.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace RecreioEspacial.UI
{
    /// <summary>
    /// Toda a interface do jogo, montada por código: título, HUD (cena, faixa de tutorial, dica),
    /// rótulo de hover, balão de fala, inventário, caderno das contas, cutscene final, tela de fim e fade.
    /// Não contém regras do jogo: só mostra o que o GameController manda e avisa os cliques por eventos.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        // ---------- Eventos (o GameController escuta) ----------
        public event Action PlayClicked, ContinueClicked, ReplayClicked, HintClicked, BubbleClicked;
        public event Action<int> ItemClicked;
        public event Action<int, int> PuzzleOptionClicked;
        public event Action PuzzleHintClicked, PuzzleCloseClicked, PuzzleShowClicked;

        static float Cq(float v) => UiKit.Cq(v);

        Canvas canvas;
        RectTransform root, playLayer, titleLayer, cutsceneLayer, endLayer, puzzleLayer;
        Image fade;

        // HUD
        Image sceneTag, banner, hover, bubble;
        Text sceneTagText, bannerText, hoverText, bubbleName, bubbleText;
        UiKit.ButtonParts hintButton;

        // Inventário
        RectTransform inventoryRow;
        readonly List<Slot> slots = new List<Slot>();

        // Título / fim
        UiKit.ButtonParts continueButton;
        Text endTimeText;

        // Cutscene
        readonly List<Image> panels = new List<Image>();
        Image captionBar;
        Text captionText;

        // Puzzle
        RectTransform puzzlePanel;

        readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        class Slot
        {
            public Image Frame;
            public Outline Border;
            public Image Icon;
            public Text Label, Number;
            public UiPulse Pulse;
        }

        // =====================================================================
        // Construção
        // =====================================================================

        public static GameUI Create(Camera cam)
        {
            EnsureEventSystem();

            var go = new GameObject("UI", typeof(RectTransform));
            go.layer = 5;
            var ui = go.AddComponent<GameUI>();
            ui.canvas = go.AddComponent<Canvas>();
            // Screen Space - Camera: o canvas acompanha o retângulo 16:9 da câmera (com tarjas pretas).
            ui.canvas.renderMode = RenderMode.ScreenSpaceCamera;
            ui.canvas.worldCamera = cam;
            ui.canvas.planeDistance = 5f;
            ui.canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            ui.root = (RectTransform)go.transform;
            ui.Build();
            return ui;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyEventSystem() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        static EventSystem FindAnyEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<EventSystem>();
#else
            return FindObjectOfType<EventSystem>();
#endif
        }

        void Build()
        {
            playLayer = Layer("Jogo");
            BuildHud();
            BuildInventory();
            BuildBubble();

            puzzleLayer = Layer("Caderno");
            var dim = UiKit.Panel("Escurecer", puzzleLayer, Tokens.Ink.WithAlpha(0.55f), true);
            UiKit.Stretch(dim.rectTransform);

            cutsceneLayer = Layer("Final");
            BuildCutscene();

            titleLayer = Layer("Titulo");
            BuildTitle();

            endLayer = Layer("Fim");
            BuildEnd();

            fade = UiKit.Panel("Fade", root, Color.black);
            UiKit.Stretch(fade.rectTransform);
            fade.color = new Color(0, 0, 0, 0);

            ShowScreen(UiScreen.Title);
        }

        RectTransform Layer(string name)
        {
            var rt = UiKit.Rect(name, root);
            UiKit.Stretch(rt);
            return rt;
        }

        void BuildHud()
        {
            // Título da cena (canto superior esquerdo)
            sceneTag = UiKit.Panel("Cena", playLayer, Tokens.Ink);
            UiKit.Anchor(sceneTag.rectTransform, new Vector2(0, 1), new Vector2(Cq(1.2f), -Cq(1.2f)), Vector2.zero);
            sceneTagText = UiKit.Label("Texto", sceneTag.transform, "", GameAssets.Weight.Bold, Cq(1.1f), Tokens.Bg);

            // Faixa de tutorial / dica (topo, centro)
            banner = UiKit.Panel("Faixa", playLayer, Tokens.Bg);
            UiKit.Border(banner, Tokens.Ink);
            UiKit.Anchor(banner.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -Cq(1.2f)), Vector2.zero);
            bannerText = UiKit.Label("Texto", banner.transform, "", GameAssets.Weight.Bold, Cq(1.35f), Tokens.Ink);
            banner.gameObject.AddComponent<PopIn>().Duration = 0.2f;
            banner.gameObject.SetActive(false);

            // Botão de dica (canto superior direito)
            hintButton = UiKit.Button("Dica", playLayer, "Dica", UiKit.ButtonStyle.Secondary, Cq(1.2f), () => HintClicked?.Invoke());
            UiKit.Anchor(hintButton.Rect, new Vector2(1, 1), new Vector2(-Cq(1.2f), -Cq(1.2f)), Vector2.zero);
            UiKit.FitButton(hintButton, Cq(0.9f), Cq(0.5f));

            // Rótulo de hover
            hover = UiKit.Panel("Hover", playLayer, Tokens.Ink);
            UiKit.Anchor(hover.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -Cq(5f)), Vector2.zero);
            hoverText = UiKit.Label("Texto", hover.transform, "", GameAssets.Weight.Bold, Cq(1.2f), Tokens.Bg);
            hover.gameObject.SetActive(false);
        }

        void BuildBubble()
        {
            bubble = UiKit.Panel("Balao", playLayer, Color.white, true);
            UiKit.Border(bubble, Tokens.Ink, Cq(0.2f) * 0.65f);
            var btn = bubble.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => BubbleClicked?.Invoke());
            bubbleName = UiKit.Label("Nome", bubble.transform, "", GameAssets.Weight.Bold, Cq(1f), Tokens.AccentDark);
            bubbleText = UiKit.Label("Fala", bubble.transform, "", GameAssets.Weight.Bold, Cq(1.7f), Tokens.Ink);
            bubbleText.lineSpacing = 1.05f;
            bubble.gameObject.AddComponent<PopIn>().Duration = 0.15f;
            bubble.gameObject.SetActive(false);
        }

        void BuildInventory()
        {
            inventoryRow = UiKit.Rect("Inventario", playLayer);
            UiKit.Anchor(inventoryRow, Vector2.zero, new Vector2(Cq(1.2f), Cq(1.2f)), new Vector2(Cq(60f), Cq(8.5f)));
        }

        Slot MakeSlot(int index)
        {
            float size = Cq(8.5f), pad = Cq(0.5f);
            var frame = UiKit.Panel("Item " + (index + 1), inventoryRow, Tokens.Bg, true);
            UiKit.Anchor(frame.rectTransform, new Vector2(0, 0), new Vector2(index * (size + Cq(0.8f)) + size / 2f, size / 2f), new Vector2(size, size));
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f); // pulso escala a partir do centro
            var border = UiKit.Border(frame, Tokens.Ink, Cq(0.25f) * 0.65f);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                ItemClicked?.Invoke(index);
            });

            float labelH = Cq(1.3f);
            var icon = UiKit.Panel("Icone", frame.transform, Color.white);
            icon.preserveAspect = true;
            var irt = icon.rectTransform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(pad, pad + labelH + Cq(0.3f));
            irt.offsetMax = new Vector2(-pad, -pad);

            var label = UiKit.Label("Nome", frame.transform, "", GameAssets.Weight.Bold, Cq(0.95f), Tokens.Ink);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 0);
            lrt.pivot = new Vector2(0, 0);
            lrt.offsetMin = new Vector2(pad, pad);
            lrt.offsetMax = new Vector2(-pad, pad + labelH);

            // Número da tecla (1–4) para quem joga no teclado
            var number = UiKit.Label("Tecla", frame.transform, (index + 1).ToString(), GameAssets.Weight.ExtraBold, Cq(0.9f), Tokens.Neutral700, TextAnchor.UpperRight);
            var nrt = number.rectTransform;
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(1, 1);
            nrt.anchoredPosition = new Vector2(-pad * 0.6f, -pad * 0.4f);
            nrt.sizeDelta = new Vector2(Cq(2f), Cq(1.4f));

            var pulse = frame.gameObject.AddComponent<UiPulse>();
            pulse.enabled = false;
            return new Slot { Frame = frame, Border = border, Icon = icon, Label = label, Number = number, Pulse = pulse };
        }

        void BuildTitle()
        {
            var bg = UiKit.Panel("Fundo", titleLayer, Tokens.Bg, true);
            UiKit.Stretch(bg.rectTransform);
            float pad = Cq(4f);

            var line = UiKit.Panel("Linha", titleLayer, Tokens.Ink);
            var lr = line.rectTransform;
            lr.anchorMin = new Vector2(0, 1);
            lr.anchorMax = new Vector2(1, 1);
            lr.pivot = new Vector2(0.5f, 1);
            lr.offsetMin = new Vector2(pad, -pad - Cq(0.25f));
            lr.offsetMax = new Vector2(-pad, -pad);

            var left = UiKit.Label("Versao", titleLayer, "PROTÓTIPO UNITY · V0.1", GameAssets.Weight.Bold, Cq(1.2f), Tokens.Ink);
            UiKit.Anchor(left.rectTransform, new Vector2(0, 1), new Vector2(pad, -pad - Cq(1.2f)), new Vector2(Cq(40), Cq(2)));
            var right = UiKit.Label("Plataforma", titleLayer, "PC + MOBILE", GameAssets.Weight.Bold, Cq(1.2f), Tokens.AccentDark, TextAnchor.UpperRight);
            UiKit.Anchor(right.rectTransform, new Vector2(1, 1), new Vector2(-pad, -pad - Cq(1.2f)), new Vector2(Cq(40), Cq(2)));

            var kicker = UiKit.Label("Serie", titleLayer, "Recreio Espacial · EP 01", GameAssets.Weight.ExtraBold, Cq(2.4f), Tokens.AccentDark);
            UiKit.Anchor(kicker.rectTransform, new Vector2(0, 0.5f), new Vector2(pad, Cq(7.5f)), new Vector2(Cq(80), Cq(3.2f)));
            var title = UiKit.Label("Titulo", titleLayer, "A última aula", GameAssets.Weight.ExtraBold, Cq(11f), Tokens.Ink);
            title.lineSpacing = 0.85f;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Anchor(title.rectTransform, new Vector2(0, 0.5f), new Vector2(pad, -Cq(5f)), new Vector2(Cq(90), Cq(12f)));

            string howTo = "Toque no cenário para andar e interagir. Toque num item do inventário e depois onde quer usar. " +
                           "Toque no balão para pular a fala.\nNo teclado: setas/WASD andam, E ou Espaço interage, 1–4 escolhem item, H pede dica.";
            var how = UiKit.Label("ComoJogar", titleLayer, howTo, GameAssets.Weight.Regular, Cq(1.5f), Tokens.Ink);
            how.lineSpacing = 1.2f;
            UiKit.Anchor(how.rectTransform, new Vector2(0, 0), new Vector2(pad, pad), new Vector2(Cq(46f), Cq(10f)));
            how.alignment = TextAnchor.LowerLeft;

            var play = UiKit.Button("Jogar", titleLayer, "Jogar", UiKit.ButtonStyle.Primary, Cq(2.4f), () => PlayClicked?.Invoke());
            UiKit.Anchor(play.Rect, new Vector2(1, 0), new Vector2(-pad, pad), Vector2.zero);
            var playSize = UiKit.FitButton(play, Cq(2f), Cq(1.4f), Cq(5f));

            continueButton = UiKit.Button("Continuar", titleLayer, "Continuar", UiKit.ButtonStyle.Secondary, Cq(2.0f), () => ContinueClicked?.Invoke());
            UiKit.Anchor(continueButton.Rect, new Vector2(1, 0), new Vector2(-pad - playSize.x - Cq(1.2f), pad), Vector2.zero);
            UiKit.FitButton(continueButton, Cq(1.6f), Cq(1.6f), Cq(3f));
        }

        void BuildEnd()
        {
            var bg = UiKit.Panel("Fundo", endLayer, Color.black, true);
            UiKit.Stretch(bg.rectTransform);
            float pad = Cq(6f);
            var t = UiKit.Label("Continua", endLayer, "Continua…", GameAssets.Weight.ExtraBold, Cq(8f), Tokens.Bg, TextAnchor.LowerLeft);
            UiKit.Anchor(t.rectTransform, new Vector2(0, 0.5f), new Vector2(pad, Cq(6f)), new Vector2(Cq(80), Cq(10f)));
            endTimeText = UiKit.Label("Tempo", endLayer, "", GameAssets.Weight.Regular, Cq(1.6f), Tokens.Neutral400);
            UiKit.Anchor(endTimeText.rectTransform, new Vector2(0, 0.5f), new Vector2(pad, -Cq(0.5f)), new Vector2(Cq(80), Cq(2.4f)));
            var again = UiKit.Button("JogarDeNovo", endLayer, "Jogar de novo", UiKit.ButtonStyle.Primary, Cq(1.8f), () => ReplayClicked?.Invoke());
            UiKit.Anchor(again.Rect, new Vector2(0, 0.5f), new Vector2(pad, -Cq(9f)), Vector2.zero);
            UiKit.FitButton(again, Cq(1.6f), Cq(1.2f), Cq(4f));
        }

        void BuildCutscene()
        {
            var bg = UiKit.Panel("Fundo", cutsceneLayer, Color.black, true);
            UiKit.Stretch(bg.rectTransform);
            for (int i = 0; i < 4; i++)
            {
                var p = UiKit.Panel("Painel " + (i + 1), cutsceneLayer, new Color(1, 1, 1, 0));
                var rt = p.rectTransform;
                rt.anchorMin = new Vector2(i / 4f, 0);
                rt.anchorMax = new Vector2((i + 1) / 4f, 1);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                panels.Add(p);
            }
            captionBar = UiKit.Panel("Legenda", cutsceneLayer, Tokens.Ink.WithAlpha(0.85f));
            var cr = captionBar.rectTransform;
            cr.anchorMin = new Vector2(0, 0);
            cr.anchorMax = new Vector2(1, 0);
            cr.pivot = new Vector2(0.5f, 0);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = new Vector2(0, Cq(2f) * 1.3f + Cq(1.6f) * 2f);
            captionText = UiKit.Label("Texto", captionBar.transform, "", GameAssets.Weight.Bold, Cq(2f), Tokens.Bg, TextAnchor.MiddleLeft);
            UiKit.Stretch(captionText.rectTransform);
            captionText.rectTransform.offsetMin = new Vector2(Cq(2f), Cq(1.6f));
            captionText.rectTransform.offsetMax = new Vector2(-Cq(2f), -Cq(1.6f));
        }

        // =====================================================================
        // Telas
        // =====================================================================

        public enum UiScreen { Title, Play, Cutscene, End }

        public void ShowScreen(UiScreen s)
        {
            titleLayer.gameObject.SetActive(s == UiScreen.Title);
            playLayer.gameObject.SetActive(s == UiScreen.Play);
            cutsceneLayer.gameObject.SetActive(s == UiScreen.Cutscene);
            endLayer.gameObject.SetActive(s == UiScreen.End);
            if (s != UiScreen.Play) ClosePuzzle();
        }

        public void SetContinueAvailable(bool available) => continueButton.Rect.gameObject.SetActive(available);

        public void SetFade(float alpha) => fade.color = new Color(0, 0, 0, Mathf.Clamp01(alpha));

        public void SetEndTime(float seconds)
        {
            int s = Mathf.RoundToInt(seconds);
            endTimeText.text = $"Fim do episódio 01 · tempo de jogo: {s / 60} min {s % 60} s";
        }

        // =====================================================================
        // HUD
        // =====================================================================

        public void SetSceneTitle(string title)
        {
            sceneTagText.text = (title ?? "").ToUpperInvariant();
            FitBox(sceneTag, sceneTagText, Cq(60f), Cq(0.9f), Cq(0.5f));
        }

        public void SetBanner(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (show && (!banner.gameObject.activeSelf || bannerText.text != text))
            {
                bannerText.text = text;
                FitBox(banner, bannerText, Cq(46f), Cq(1f), Cq(0.6f));
                if (banner.gameObject.activeSelf) banner.GetComponent<PopIn>().Play();
            }
            banner.gameObject.SetActive(show);
        }

        public void SetHintLevel(int level)
        {
            string label = level > 0 ? $"Dica ({level}/3)" : "Dica";
            if (hintButton.Label.text == label) return;
            hintButton.Label.text = label;
            UiKit.FitButton(hintButton, Cq(0.9f), Cq(0.5f));
        }

        public void SetHover(string label)
        {
            bool show = !string.IsNullOrEmpty(label);
            if (show && hoverText.text != label)
            {
                hoverText.text = label;
                FitBox(hover, hoverText, Cq(40f), Cq(0.8f), Cq(0.4f));
            }
            hover.gameObject.SetActive(show);
        }

        /// <summary>
        /// Mostra o balão (ou só reposiciona, se a fala é a mesma).
        /// x = centro em %, bottom = base do balão em % (medida da base do palco).
        /// </summary>
        public void ShowBubble(string name, string text, float xPct, float bottomPct)
        {
            string upperName = (name ?? "").ToUpperInvariant();
            text = text ?? "";
            bool same = bubble.gameObject.activeSelf && bubbleName.text == upperName && bubbleText.text == text;
            var rt = bubble.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(xPct / 100f, bottomPct / 100f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            if (same) return;

            float padX = Cq(1.2f), padY = Cq(0.9f), gap = Cq(0.3f);
            bubbleName.text = upperName;
            bubbleText.text = text;
            var nameSize = UiKit.Measure(bubbleName, Cq(34f) - padX * 2f);
            var textSize = UiKit.Measure(bubbleText, Cq(34f) - padX * 2f);
            float inner = Mathf.Max(nameSize.x, textSize.x);
            float width = Mathf.Clamp(inner + padX * 2f, Cq(10f), Cq(34f));
            inner = width - padX * 2f;
            nameSize = UiKit.Measure(bubbleName, inner);
            textSize = UiKit.Measure(bubbleText, inner);
            float height = padY * 2f + nameSize.y + gap + textSize.y;

            PlaceTopLeft(bubbleName.rectTransform, new Vector2(padX, -padY), new Vector2(inner, nameSize.y));
            PlaceTopLeft(bubbleText.rectTransform, new Vector2(padX, -padY - nameSize.y - gap), new Vector2(inner, textSize.y));
            rt.sizeDelta = new Vector2(width, height);

            if (bubble.gameObject.activeSelf) bubble.GetComponent<PopIn>().Play();
            else bubble.gameObject.SetActive(true);
        }

        public void HideBubble()
        {
            bubble.gameObject.SetActive(false);
            bubbleText.text = "";
        }

        static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void FitBox(Image box, Text text, float maxTextWidth, float padX, float padY)
        {
            var size = UiKit.Measure(text, maxTextWidth);
            PlaceTopLeft(text.rectTransform, new Vector2(padX, -padY), size);
            box.rectTransform.sizeDelta = new Vector2(size.x + padX * 2f, size.y + padY * 2f);
        }

        // =====================================================================
        // Inventário
        // =====================================================================

        public void SetInventory(IList<string> items, EpisodeData ep, string selected, string pulseItem)
        {
            while (slots.Count < items.Count) slots.Add(MakeSlot(slots.Count));
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                bool used = i < items.Count;
                s.Frame.gameObject.SetActive(used);
                if (!used) continue;
                string id = items[i];
                ep.Items.TryGetValue(id, out var def);
                var icon = def != null ? GameAssets.Sprite(def.Icon) : null;
                if (s.Icon.sprite != icon) s.Icon.sprite = icon;
                s.Icon.color = icon != null ? Color.white : new Color(1, 1, 1, 0);
                s.Label.text = def != null ? def.Label : id;
                bool sel = id == selected;
                s.Frame.color = sel ? Tokens.AccentLight : Tokens.Bg;
                bool pulse = id == pulseItem;
                s.Border.effectColor = sel || pulse ? Tokens.Accent : Tokens.Ink;
                s.Pulse.enabled = pulse;
            }
        }

        // =====================================================================
        // Cutscene
        // =====================================================================

        public void SetupCutscene(IList<PanelDef> defs)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                var sprite = i < defs.Count ? GameAssets.Sprite(defs[i].Image) : null;
                panels[i].sprite = sprite;
                panels[i].color = new Color(1, 1, 1, 0);
            }
            captionText.text = "";
            captionBar.gameObject.SetActive(false);
        }

        public void SetPanelAlpha(int index, float alpha)
        {
            if (index < 0 || index >= panels.Count) return;
            panels[index].color = new Color(1, 1, 1, Mathf.Clamp01(alpha));
        }

        public void SetCaption(string text)
        {
            captionText.text = text ?? "";
            captionBar.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // =====================================================================
        // Caderno (puzzle de multiplicação)
        // =====================================================================

        public bool PuzzleOpen => puzzleLayer != null && puzzleLayer.gameObject.activeSelf;

        public void ClosePuzzle()
        {
            if (puzzleLayer != null) puzzleLayer.gameObject.SetActive(false);
        }

        /// <summary>Abre (ou redesenha) o caderno com as respostas e a mensagem atuais.</summary>
        public void ShowPuzzle(PuzzleDef def, int?[] answers, string message)
        {
            bool firstOpen = !puzzleLayer.gameObject.activeSelf;
            puzzleLayer.gameObject.SetActive(true);
            if (puzzlePanel != null) Destroy(puzzlePanel.gameObject);

            float width = Cq(52f), pad = Cq(2.4f), gapY = Cq(1.4f), rule = Cq(0.2f);
            var panelImg = UiKit.Panel("Caderno", puzzleLayer, Tokens.Paper, true);
            UiKit.Border(panelImg, Tokens.Ink, Cq(0.25f) * 0.65f);
            puzzlePanel = panelImg.rectTransform;
            puzzlePanel.anchorMin = puzzlePanel.anchorMax = puzzlePanel.pivot = new Vector2(0.5f, 0.5f);

            var linesRoot = UiKit.Rect("Pauta", puzzlePanel);
            UiKit.Stretch(linesRoot);
            linesRoot.gameObject.AddComponent<RectMask2D>();

            var content = UiKit.Rect("Conteudo", puzzlePanel);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = new Vector2(pad, -pad);
            float innerW = width - pad * 2f;
            content.sizeDelta = new Vector2(innerW, 10f);
            float y = 0f;

            // Cabeçalho
            var head = UiKit.Label("Titulo", content, "Tarefa de matemática", GameAssets.Weight.ExtraBold, Cq(2.4f), Tokens.Ink);
            PlaceTopLeft(head.rectTransform, new Vector2(0, y), new Vector2(innerW * 0.7f, Cq(3f)));
            var who = UiKit.Label("Aluno", content, "Antônio · 3º ano", GameAssets.Weight.Bold, Cq(1.1f), Tokens.Neutral700, TextAnchor.LowerRight);
            PlaceTopLeft(who.rectTransform, new Vector2(innerW * 0.6f, y), new Vector2(innerW * 0.4f, Cq(2.6f)));
            y -= Cq(3f) + gapY;

            // Linhas das contas
            float rowH = Cq(4.4f);
            for (int i = 0; i < def.Problems.Count; i++)
            {
                var p = def.Problems[i];
                Rule(content, y, innerW, rule);
                y -= rule + Cq(1f);
                var q = UiKit.Label("Conta", content, p.Q + " =", GameAssets.Weight.ExtraBold, Cq(3.2f), Tokens.Ink, TextAnchor.MiddleLeft);
                q.horizontalOverflow = HorizontalWrapMode.Overflow;
                PlaceTopLeft(q.rectTransform, new Vector2(0, y), new Vector2(Cq(14f), rowH));
                var a = UiKit.Label("Resposta", content, answers[i]?.ToString() ?? "", GameAssets.Weight.ExtraBold, Cq(3.2f), Tokens.AccentDark, TextAnchor.MiddleLeft);
                PlaceTopLeft(a.rectTransform, new Vector2(Cq(15f), y), new Vector2(Cq(7f), rowH));

                float x = Cq(23f);
                for (int k = 0; k < p.Options.Count; k++)
                {
                    int row = i, value = p.Options[k];
                    bool chosen = answers[i] == value;
                    var b = UiKit.Button("Opcao " + value, content, value.ToString(),
                        UiKit.ButtonStyle.Secondary, Cq(2f), () => PuzzleOptionClicked?.Invoke(row, value));
                    if (chosen)
                    {
                        var c = b.Button.colors;
                        c.normalColor = c.highlightedColor = c.pressedColor = c.selectedColor = Tokens.Ink;
                        b.Button.colors = c;
                        b.Label.color = Tokens.Bg;
                    }
                    var size = UiKit.FitButton(b, Cq(1f), 0f);
                    size.x = Mathf.Max(size.x, Cq(5f));
                    PlaceTopLeft(b.Rect, new Vector2(x, y), new Vector2(size.x, rowH));
                    b.Label.rectTransform.offsetMin = new Vector2(Cq(1f), 0);
                    b.Label.rectTransform.offsetMax = new Vector2(-Cq(0.5f), 0);
                    x += size.x + Cq(0.8f);
                }
                y -= rowH + gapY;
            }

            // Mensagem
            var msg = UiKit.Label("Mensagem", content, message ?? "", GameAssets.Weight.Bold, Cq(1.6f), Tokens.Ink);
            float msgH = Mathf.Max(Cq(2.4f), UiKit.Measure(msg, innerW).y);
            PlaceTopLeft(msg.rectTransform, new Vector2(0, y), new Vector2(innerW, msgH));
            y -= msgH + gapY;

            // Botões
            Rule(content, y, innerW, rule);
            y -= rule + Cq(1.2f);
            float bx = 0f, bh = 0f;
            bool allDone = true;
            foreach (var ans in answers) if (ans == null) allDone = false;
            if (allDone) bx += PlaceButton(UiKit.Button("Mostrar", content, "Mostrar para a professora", UiKit.ButtonStyle.Primary, Cq(1.5f),
                () => PuzzleShowClicked?.Invoke()), bx, y, ref bh);
            bx += PlaceButton(UiKit.Button("PedirDica", content, "Pedir dica", UiKit.ButtonStyle.Ghost, Cq(1.5f),
                () => PuzzleHintClicked?.Invoke(), GameAssets.Weight.Bold), bx, y, ref bh);
            PlaceButton(UiKit.Button("Fechar", content, "Fechar caderno", UiKit.ButtonStyle.Ghost, Cq(1.5f),
                () => PuzzleCloseClicked?.Invoke(), GameAssets.Weight.Bold), bx, y, ref bh);
            y -= bh;

            float height = -y + pad * 2f;
            puzzlePanel.sizeDelta = new Vector2(width, height);
            puzzlePanel.anchoredPosition = Vector2.zero;

            // Pauta azul do caderno
            for (float ly = Cq(3.1f); ly < height; ly += Cq(3.25f))
            {
                var l = UiKit.Panel("Linha", linesRoot, Tokens.PaperLine);
                var lrt = l.rectTransform;
                lrt.anchorMin = new Vector2(0, 1);
                lrt.anchorMax = new Vector2(1, 1);
                lrt.pivot = new Vector2(0.5f, 1);
                lrt.offsetMin = new Vector2(0, -ly - Cq(0.15f));
                lrt.offsetMax = new Vector2(0, -ly);
            }

            if (firstOpen) panelImg.gameObject.AddComponent<PopIn>().Duration = 0.2f;
        }

        static void Rule(RectTransform parent, float y, float width, float thickness)
        {
            var r = UiKit.Panel("Regua", parent, Tokens.Ink);
            PlaceTopLeft(r.rectTransform, new Vector2(0, y), new Vector2(width, thickness));
        }

        static float PlaceButton(UiKit.ButtonParts b, float x, float y, ref float maxH)
        {
            var size = UiKit.FitButton(b, Cq(1.6f), Cq(1f));
            PlaceTopLeft(b.Rect, new Vector2(x, y), size);
            maxH = Mathf.Max(maxH, size.y);
            return size.x + Cq(1f);
        }

        // =====================================================================
        // Entrada
        // =====================================================================

        /// <summary>True se o ponto de tela está sobre algum elemento clicável da UI.</summary>
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screenPos };
            raycastResults.Clear();
            es.RaycastAll(ped, raycastResults);
            return raycastResults.Count > 0;
        }
    }
}
