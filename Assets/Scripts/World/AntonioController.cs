using RecreioEspacial.Core;
using RecreioEspacial.Data;
using UnityEngine;

namespace RecreioEspacial.World
{
    /// <summary>
    /// Antônio: anda numa área de chão em perspectiva (clique ou teclado) com aceleração e frenagem,
    /// e tem uma animação procedural (balanço, compressão, inclinação, respiração) até chegar o rig.
    /// Posições em % do palco: X = centro horizontal, Y = distância da base.
    /// </summary>
    public class AntonioController : MonoBehaviour
    {
        const float Accel = 140f;          // %/s²
        const float MaxSpeed = 30f;        // %/s (multiplicado pela escala de profundidade)
        const float YMetric = 0.5625f;     // distância vertical vale 9/16
        const float StepLength = 4.2f;     // distância de um passo (× escala)

        public float X { get; private set; } = 50f;
        public float Y { get; private set; } = 2f;
        /// <summary>Altura extra em % (22 = nas costas do Caio). Suavizada visualmente.</summary>
        public float Lift { get; set; }
        /// <summary>1 = olhando para a direita, -1 = esquerda.</summary>
        public int Dir { get; set; } = 1;

        public bool IsWalking => hasTarget || v > 0.5f;
        public float CurrentLift => curLift;

        FloorDef floor = new FloorDef();
        float antonioHeight = 50f;

        // Movimento
        bool hasTarget;
        float tx, ty;
        int moveId;
        Vector2 keyInput;
        Vector2 heading = Vector2.right;
        float v;

        // Animação
        float phase, lean, t, curLift;
        bool side;

        Transform body;
        SpriteRenderer sr, shadow;
        Sprite frontSprite, sideSprite;

        public static AntonioController Create(Transform parent, Sprite front, Sprite side)
        {
            var go = new GameObject("Antonio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AntonioController>();
            a.frontSprite = front;
            a.sideSprite = side;

            var sh = new GameObject("Sombra");
            sh.transform.SetParent(go.transform, false);
            a.shadow = sh.AddComponent<SpriteRenderer>();
            a.shadow.sprite = GameAssets.Shadow;

            var b = new GameObject("Corpo");
            b.transform.SetParent(go.transform, false);
            a.body = b.transform;
            a.sr = b.AddComponent<SpriteRenderer>();
            a.sr.sprite = front;
            return a;
        }

        public void EnterScene(SceneDef scene)
        {
            floor = scene.Floor ?? new FloorDef();
            antonioHeight = scene.AntonioHeight;
            X = scene.StartX;
            Y = floor.NearY;
            Lift = 0;
            curLift = 0;
            v = 0;
            Dir = 1;
            phase = 0;
            lean = 0;
            hasTarget = false;
            keyInput = Vector2.zero;
            UpdateVisual(0f);
        }

        // ---------- Área de chão ----------

        public float DepthT(float y) =>
            floor.FarY > floor.NearY ? Mathf.Clamp01((y - floor.NearY) / (floor.FarY - floor.NearY)) : 0f;

        public float ScaleAt(float y) => Mathf.Lerp(1f, floor.FarScale, DepthT(y));

        public Vector2 Clamp(float x, float y)
        {
            y = Mathf.Clamp(y, floor.NearY, Mathf.Max(floor.NearY, floor.FarY));
            float t01 = DepthT(y);
            float minX = Mathf.Lerp(floor.NearXMin, floor.FarXMin, t01);
            float maxX = Mathf.Lerp(floor.NearXMax, floor.FarXMax, t01);
            return new Vector2(Mathf.Clamp(x, minX, maxX), y);
        }

        /// <summary>Altura atual do Antônio em % do palco (com a escala de profundidade).</summary>
        public float HeightPct => antonioHeight * ScaleAt(Y);

        // ---------- Comandos ----------

        /// <summary>Começa a andar até (x, y). y null = mantém a profundidade. Devolve um id (0 = já está lá).</summary>
        public int MoveTo(float x, float? y = null)
        {
            var p = Clamp(x, y ?? Y);
            if (Mathf.Sqrt((p.x - X) * (p.x - X) + (p.y - Y) * YMetric * (p.y - Y) * YMetric) < 0.6f) return 0;
            tx = p.x;
            ty = p.y;
            hasTarget = true;
            return ++moveId;
        }

        public bool IsMovingTo(int id) => id != 0 && hasTarget && moveId == id;

        public void StopMove() => hasTarget = false;

        /// <summary>Entrada direta das setas/WASD (zero = solto).</summary>
        public void SetKeyboardInput(Vector2 dir) => keyInput = dir;

        /// <summary>Teleporta (usado ao trocar de cena / debug).</summary>
        public void Place(float x, float y)
        {
            var p = Clamp(x, y);
            X = p.x;
            Y = p.y;
            hasTarget = false;
            v = 0;
        }

        // ---------- Loop ----------

        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            Step(dt);
            UpdateVisual(dt);
        }

        void Step(float dt)
        {
            t += dt;
            float scale = ScaleAt(Y);
            float vmax = MaxSpeed * scale;
            bool keyboard = keyInput.sqrMagnitude > 0.01f;
            float moved = 0f, dxMoved = 0f, dyMetricMoved = 0f;

            if (keyboard)
            {
                hasTarget = false;
                heading = keyInput.normalized;
                v = Mathf.Min(vmax, v + Accel * dt);
            }

            if (hasTarget)
            {
                float dx = tx - X, dy = (ty - Y) * YMetric;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Freia para parar exatamente no alvo (v² / 2a)
                if (d <= v * v / (2f * Accel) + 0.05f) v = Mathf.Max(6f, v - Accel * dt);
                else v = Mathf.Min(vmax, v + Accel * dt);
                float step = Mathf.Min(d, v * dt);
                if (d > 0.001f)
                {
                    X += dx / d * step;
                    Y += dy / d * step / YMetric;
                    dxMoved = dx;
                    dyMetricMoved = dy;
                }
                moved = step;
                if (d - step < 0.05f)
                {
                    X = tx;
                    Y = ty;
                    v = 0;
                    hasTarget = false;
                }
            }
            else if (v > 0f)
            {
                // Teclado: anda na direção pedida; ao soltar, desliza freando.
                if (!keyboard) v = Mathf.Max(0f, v - Accel * dt);
                float step = v * dt;
                var before = new Vector2(X, Y);
                var p = Clamp(X + heading.x * step, Y + heading.y * step / YMetric);
                X = p.x;
                Y = p.y;
                dxMoved = X - before.x;
                dyMetricMoved = (Y - before.y) * YMetric;
                moved = Mathf.Sqrt(dxMoved * dxMoved + dyMetricMoved * dyMetricMoved);
                if (moved < step * 0.2f && !keyboard) v = 0f; // bateu na borda
            }

            if (moved > 0f)
            {
                phase += moved / (StepLength * ScaleAt(Y)) * Mathf.PI;
                if (Mathf.Abs(dxMoved) > 0.2f || keyboard && Mathf.Abs(dxMoved) > 0.0001f) Dir = dxMoved < 0 ? -1 : 1;
                side = Mathf.Abs(dxMoved) > Mathf.Abs(dyMetricMoved) * 0.6f;
            }
            else
            {
                // Parado: termina o passo suavemente
                float target = Mathf.Round(phase / Mathf.PI) * Mathf.PI;
                phase += (target - phase) * Mathf.Min(1f, dt * 12f);
            }
        }

        void UpdateVisual(float dt)
        {
            if (sr == null) return;
            curLift += (Lift - curLift) * Mathf.Min(1f, dt * 7f);

            float s = ScaleAt(Y);
            float walkAmt = Mathf.Min(1f, v / 14f);
            float sn = Mathf.Abs(Mathf.Sin(phase));
            bool moving = hasTarget || walkAmt > 0.05f;
            bool useSide = moving && side && sideSprite != null;
            var spr = useSide ? sideSprite : frontSprite;
            if (sr.sprite != spr) sr.sprite = spr;

            float bob = sn * 2.4f * walkAmt;                       // % da altura
            float contact = (1f - sn) * walkAmt;
            float breathe = (1f - walkAmt) * 0.011f * Mathf.Sin(t * 2.3f);
            float sy = 1f - 0.05f * contact + breathe;
            float sx = 1f + 0.035f * contact - breathe * 0.4f;
            float leanTarget = useSide ? Dir * 5f * walkAmt : 0f;
            lean += (leanTarget - lean) * Mathf.Min(1f, dt * 10f);
            float fx = useSide ? Dir : 1f;

            float heightUnits = Stage.H(antonioHeight * s);
            float spriteUnits = spr != null ? spr.rect.height / spr.pixelsPerUnit : 1f;
            float k = heightUnits / spriteUnits;

            transform.localPosition = Stage.Point(X, Y + curLift);
            body.localPosition = new Vector3(0f, bob / 100f * heightUnits, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, -lean); // CSS rotate(+) = horário
            body.localScale = new Vector3(k * sx * fx, k * sy, 1f);

            int order = Stage.SortingOrderForBottom(Y) + 1;
            sr.sortingOrder = order;

            if (shadow != null)
            {
                float widthUnits = spr != null ? heightUnits * spr.rect.width / spr.rect.height : heightUnits * 0.5f;
                float sc = 1f - bob * 0.06f;
                shadow.transform.localPosition = new Vector3(0f, -curLift / 100f * Stage.Height + heightUnits * 0.013f, 0f);
                shadow.transform.localScale = new Vector3(widthUnits * 0.78f * sc, heightUnits * 0.05f * sc, 1f);
                var c = shadow.color;
                c.a = curLift > 3f ? 0f : 1f - bob * 0.12f;
                shadow.color = c;
                shadow.sortingOrder = order - 1;
            }
        }
    }
}
