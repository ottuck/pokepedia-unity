using System.Collections;
using System.Collections.Generic;
using Pokepedia.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle.Fx
{
    /// <summary>
    /// 배틀 화면 이펙트 계층 (PROJECT.md 5.5). 이펙트는 월드(BattleWorld)에서 그려지고, 화면 전체 번쩍임만 HUD 맨 위 Image 로 그린다.
    /// 좌표는 HUD 와 같은 "화면 픽셀, 좌상단 원점, y 아래로" 를 받아 BattleWorld.ToLocal 로 바꾼다.
    /// - 파티클: Unity Particle System. 월드 1 단위 = 1 화면 픽셀(기준 해상도)이라 크기·속도 단위도 화면 픽셀이다.
    ///   텍스처 없이 정사각형 입자로 그려 도트 느낌을 낸다.
    /// - 도형: 번개 선분·구체·고리는 SpriteRenderer 로 그린다 (정수 픽셀 정렬이 쉽다).
    /// 월드 이펙트는 HUD(정보 상자·메시지) 뒤에 그려진다.
    /// Java 비교: 코루틴(IEnumerator + yield)은 프레임 단위로 나눠 실행되는 협력적 작업이다 (스레드가 아님).
    /// </summary>
    public sealed class VfxLayer
    {
        readonly BattleWorld world;
        readonly RectTransform hudScreen;
        readonly Transform fx;
        readonly Material particleMaterial;
        readonly Image flash;
        readonly List<ParticleSystem> pool = new List<ParticleSystem>();
        readonly Sprite circle, ring;
        int poolIndex;

        public VfxLayer(BattleWorld world, RectTransform hudScreen, Material particleMaterial)
        {
            this.world = world;
            this.hudScreen = hudScreen;
            this.particleMaterial = particleMaterial;
            fx = new GameObject("Fx").transform;
            fx.SetParent(world.Root, false);
            flash = Ui.Fill(hudScreen, "ScreenFlash", 0, 0, PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight, new Color(1, 1, 1, 0));
            circle = MakeSprite(16, (x, y) => Dist(x, y, 16) <= 1f);
            ring = MakeSprite(32, (x, y) => { var d = Dist(x, y, 32); return d <= 1f && d >= 0.8f; });
            // 타격 불꽃: 가로·세로로 길고 대각선은 짧은 별(마름모 4개를 겹친 모양). 바깥 별(색)과 안쪽 별(흰색) 두 겹으로 쓴다.
            starRim = MakeSprite(17, (x, y) => Star(x, y, 17, 1f));
            starCore = MakeSprite(17, (x, y) => Star(x, y, 17, 0.55f));
            for (int i = 0; i < 4; i++) pool.Add(CreateEmitter("Particles" + i));
        }

        /// <summary>화면 번쩍임을 HUD 맨 위로 (정보 상자·메시지보다 위)</summary>
        public void BringToFront() => flash.transform.SetAsLastSibling();

        Sprite starRim, starCore;

        static bool Star(int x, int y, int n, float scale)
        {
            float c = (n - 1) / 2f, dx = Mathf.Abs(x - c) / (c * scale), dy = Mathf.Abs(y - c) / (c * scale);
            bool cross = dx + dy * 4 <= 1 || dy + dx * 4 <= 1;                        // 가로·세로 긴 가시
            float u = (dx + dy) * 0.7071f, v = Mathf.Abs(dx - dy) * 0.7071f;
            bool diag = u * 1.6f + v * 5 <= 1;                                           // 대각선 짧은 가시
            return cross || diag || dx * dx + dy * dy <= 0.06f;
        }

        /// <summary>
        /// 타격 불꽃: 맞은 지점에 도트 별이 1배 → 2배로 "팍" 커졌다가 사라진다. 정수 배율로만 바꿔 도트가 고르게 유지된다.
        /// </summary>
        public IEnumerator ImpactStar(Vector2 at, Color core, Color rim, float seconds)
        {
            var back = world.NewRenderer(fx, "StarRim", starRim, BattleWorld.SortFx + 3);
            var front = world.NewRenderer(fx, "StarCore", starCore, BattleWorld.SortFx + 4);
            foreach (var r in new[] { back, front }) r.transform.localPosition = ToLocal(at);
            back.color = rim; front.color = core;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float sc = k < 0.25f ? 1 : 2;
                foreach (var r in new[] { back, front }) r.transform.localScale = new Vector3(sc, sc, 1);
                float a = k < 0.5f ? 1 : 1 - (k - 0.5f) / 0.5f;
                back.color = new Color(rim.r, rim.g, rim.b, a);
                front.color = new Color(core.r, core.g, core.b, a);
                yield return null;
            }
            Object.Destroy(back.gameObject); Object.Destroy(front.gameObject);
        }

        static float Dist(int x, int y, int n)
        {
            float r = n / 2f, dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static Sprite MakeSprite(int n, System.Func<int, int, bool> inside)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) tex.SetPixel(x, y, inside(x, y) ? Color.white : Color.clear);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
        }

        ParticleSystem CreateEmitter(string name)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(fx, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission; emission.enabled = false; // Emit() 으로만 낸다
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.sortingOrder = BattleWorld.SortFx;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        /// <summary>화면 픽셀 좌표(좌상단 원점) → 월드 로컬 좌표(가운데 원점, y 위로)</summary>
        public static Vector3 ToLocal(Vector2 px) => BattleWorld.ToLocal(px);

        ParticleSystem Next()
        {
            var ps = pool[poolIndex];
            poolIndex = (poolIndex + 1) % pool.Count;
            return ps;
        }

        /// <summary>한 점에서 사방으로 튀는 입자. gravity 는 픽셀/초² (아래로 +).</summary>
        public void Burst(Vector2 at, Color a, Color b, int count, float speedMin, float speedMax, float life, float size, float gravity = 0, float spreadRadius = 0)
        {
            var ps = Next();
            var main = ps.main;
            main.gravityModifier = 0;
            var force = ps.forceOverLifetime;
            force.enabled = gravity != 0;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = new ParticleSystem.MinMaxCurve(-gravity);
            force.x = new ParticleSystem.MinMaxCurve(0);
            for (int i = 0; i < count; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                var p = new ParticleSystem.EmitParams
                {
                    position = ToLocal(at) + (Vector3)(Random.insideUnitCircle * spreadRadius),
                    velocity = dir * Random.Range(speedMin, speedMax),
                    startLifetime = life * Random.Range(0.6f, 1f),
                    startSize = Mathf.Round(size * Random.Range(0.7f, 1.3f)),
                    startColor = Color.Lerp(a, b, Random.value),
                };
                ps.Emit(p, 1);
            }
        }

        /// <summary>from → to 방향으로 seconds 동안 입자를 흘려 보낸다 (물대포 등).</summary>
        public IEnumerator Stream(Vector2 from, Vector2 to, Color a, Color b, float seconds, int perFrame, float speed, float size, float jitter)
        {
            var ps = Next();
            var force = ps.forceOverLifetime; force.enabled = false;
            var dir = (to - from).normalized;
            var flight = (to - from).magnitude / speed;
            var local = ToLocal(from);
            var vel = new Vector3(dir.x, -dir.y, 0) * speed; // 화면 좌표는 y 아래, 월드는 y 위
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                for (int i = 0; i < perFrame; i++)
                {
                    var side = new Vector3(-vel.y, vel.x, 0).normalized * Random.Range(-jitter, jitter);
                    ps.Emit(new ParticleSystem.EmitParams
                    {
                        position = local + side,
                        velocity = vel * Random.Range(0.92f, 1.08f),
                        startLifetime = flight,
                        startSize = Mathf.Round(size * Random.Range(0.7f, 1.3f)),
                        startColor = Color.Lerp(a, b, Random.value),
                    }, 1);
                }
                yield return null;
            }
        }

        /// <summary>지그재그 번개 (위 → 아래). 선분을 만들고 깜빡인 뒤 지운다.</summary>
        public IEnumerator Bolt(Vector2 top, Vector2 bottom, Color core, Color glow, int flickers, float seconds)
        {
            var root = new GameObject("Bolt").transform;
            root.SetParent(fx, false);
            var points = new List<Vector2> { top };
            int segments = 7;
            for (int i = 1; i < segments; i++)
            {
                var p = Vector2.Lerp(top, bottom, i / (float)segments);
                p.x += Random.Range(-9f, 9f);
                points.Add(p);
            }
            points.Add(bottom);
            for (int i = 0; i < points.Count - 1; i++)
            {
                Segment(root, points[i], points[i + 1], 5, glow, BattleWorld.SortFx);
                Segment(root, points[i], points[i + 1], 2, core, BattleWorld.SortFx + 1);
            }
            float each = seconds / Mathf.Max(1, flickers * 2);
            for (int f = 0; f < flickers; f++)
            {
                root.gameObject.SetActive(true); yield return Wait(each);
                root.gameObject.SetActive(false); yield return Wait(each * 0.6f);
            }
            Object.Destroy(root.gameObject);
        }

        /// <summary>
        /// from → to 로 뻗는 지그재그 번개(몸에서 발사하는 전기). 가운데가 가장 크게 휘고 양 끝은 정확히 from·to 에 붙는다.
        /// forks 만큼 잔가지를 친다. 선분을 만들어 깜빡인 뒤 지운다.
        /// </summary>
        public IEnumerator Lightning(Vector2 from, Vector2 to, Color core, Color glow, int flickers, float seconds,
                                     float jitter = 9, int forks = 0, float glowWidth = 5)
        {
            var root = new GameObject("Lightning").transform;
            root.SetParent(fx, false);
            var dir = to - from;
            var perp = new Vector2(-dir.y, dir.x).normalized;
            int segments = Mathf.Clamp(Mathf.RoundToInt(dir.magnitude / 14f), 5, 12);
            var points = new List<Vector2> { from };
            for (int i = 1; i < segments; i++)
            {
                float t = i / (float)segments;
                points.Add(Vector2.Lerp(from, to, t) + perp * Random.Range(-jitter, jitter) * Mathf.Sin(t * Mathf.PI));
            }
            points.Add(to);
            for (int i = 0; i < points.Count - 1; i++)
            {
                Segment(root, points[i], points[i + 1], glowWidth, glow, BattleWorld.SortFx);
                Segment(root, points[i], points[i + 1], 2, core, BattleWorld.SortFx + 1);
            }
            for (int f = 0; f < forks; f++)
            {
                var start = points[Random.Range(1, points.Count - 1)];
                float angle = Random.Range(25f, 50f) * (Random.value < 0.5f ? 1 : -1);
                var fdir = (Vector2)(Quaternion.Euler(0, 0, angle) * dir.normalized);
                var a = start;
                for (int k = 0; k < 3; k++)
                {
                    var b = a + fdir * Random.Range(6f, 10f) + perp * Random.Range(-3f, 3f);
                    Segment(root, a, b, 3, glow, BattleWorld.SortFx);
                    Segment(root, a, b, 1, core, BattleWorld.SortFx + 1);
                    a = b;
                }
            }
            float each = seconds / Mathf.Max(1, flickers * 2);
            for (int f = 0; f < flickers; f++)
            {
                root.gameObject.SetActive(true); yield return Wait(each);
                root.gameObject.SetActive(false); yield return Wait(each * 0.6f);
            }
            Object.Destroy(root.gameObject);
        }

        /// <summary>
        /// 굵은 물줄기 빔: from 에서 to 까지 extendSec 동안 뻗어 holdSec 동안 출렁이며 유지하다가 가늘어지며 사라진다.
        /// 바깥(outer)·안쪽(inner)·흰 심(core) 세 겹이고, 두께는 정수 픽셀로 반올림한다.
        /// </summary>
        public IEnumerator Beam(Vector2 from, Vector2 to, Color outer, Color inner, float width, float extendSec, float holdSec)
        {
            var root = new GameObject("Beam").transform;
            root.SetParent(fx, false);
            root.localPosition = ToLocal(from);
            root.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(-(to.y - from.y), to.x - from.x) * Mathf.Rad2Deg);
            var o = world.NewRenderer(root, "Outer", BattleWorld.PixelLeft, BattleWorld.SortFx);
            var i = world.NewRenderer(root, "Inner", BattleWorld.PixelLeft, BattleWorld.SortFx + 1);
            var c = world.NewRenderer(root, "Core", BattleWorld.PixelLeft, BattleWorld.SortFx + 2);
            o.color = outer; i.color = inner; c.color = Color.white;
            float length = Vector2.Distance(from, to);

            void Shape(float len, float w)
            {
                o.transform.localScale = new Vector3(len, Mathf.Max(1, Mathf.Round(w)), 1);
                i.transform.localScale = new Vector3(len, Mathf.Max(1, Mathf.Round(w * 0.55f)), 1);
                c.transform.localScale = new Vector3(len, Mathf.Max(1, Mathf.Round(w * 0.2f)), 1);
            }

            for (float t = 0; t < extendSec; t += Time.deltaTime)
            {
                float k = t / extendSec;
                Shape(Mathf.Round(length * (1 - (1 - k) * (1 - k))), width * (0.6f + 0.4f * k));
                yield return null;
            }
            for (float t = 0; t < holdSec; t += Time.deltaTime)
            {
                Shape(length, width * (1 + 0.18f * Mathf.Sin(t * 45f)));
                yield return null;
            }
            for (float t = 0; t < 0.12f; t += Time.deltaTime)
            {
                Shape(length, width * (1 - t / 0.12f));
                yield return null;
            }
            Object.Destroy(root.gameObject);
        }

        /// <summary>
        /// 물줄기(하이드로펌프): 레이저처럼 곧은 막대가 아니라 물처럼 보이도록 길이 방향으로 잘게 나눈 조각마다 굵기가 시간·위치에 따라 출렁인다.
        /// 바깥(진한 물색, 반투명)·안쪽(밝은 물색)·흰 거품 줄(군데군데 끊기며 앞으로 흐름) 세 겹이고, 발사구에서 멀어질수록 퍼져 굵어진다.
        /// to 는 상대 몸의 "표면"이어야 한다(가운데를 주면 몸을 뚫은 것처럼 보인다). 맞는 동안 끝에서 물보라가 튀고, 끝날 때는 꼬리부터 끊겨 날아간다.
        /// 곧게 뻗어야 수압이 느껴진다(곡선으로 휘게 해 봤다가 사용자 피드백으로 되돌림, 2026-10-17).
        /// </summary>
        public IEnumerator WaterJet(Vector2 from, Vector2 to, Color deep, Color light, float width, float extendSec, float holdSec, bool splash = true)
        {
            const int N = 24;
            var root = new GameObject("WaterJet").transform;
            root.SetParent(fx, false);
            root.localPosition = ToLocal(from);
            Vector2 At(float u) => Vector2.Lerp(from, to, u);
            var dir = to - from;
            var outer = new SpriteRenderer[N];
            var inner = new SpriteRenderer[N];
            var core = new SpriteRenderer[N];
            var segLen = new float[N];
            var deepA = new Color(deep.r, deep.g, deep.b, 0.88f);
            for (int i = 0; i < N; i++)
            {
                outer[i] = world.NewRenderer(root, "O" + i, BattleWorld.PixelLeft, BattleWorld.SortFx);
                inner[i] = world.NewRenderer(root, "I" + i, BattleWorld.PixelLeft, BattleWorld.SortFx + 1);
                core[i] = world.NewRenderer(root, "C" + i, BattleWorld.PixelLeft, BattleWorld.SortFx + 2);
                outer[i].color = deepA; inner[i].color = light; core[i].color = Color.white;
                var a = At((float)i / N) - from;
                var d = At((float)(i + 1) / N) - from - a;
                segLen[i] = d.magnitude;
                foreach (var r in new[] { outer[i], inner[i], core[i] })
                {
                    r.transform.localPosition = new Vector3(Mathf.Round(a.x), -Mathf.Round(a.y), 0);
                    r.transform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);
                    r.enabled = false;
                }
            }

            void Shape(float t, int head, int tail, float thin)
            {
                for (int i = 0; i < N; i++)
                {
                    bool on = i >= tail && i < head;
                    outer[i].enabled = inner[i].enabled = on;
                    if (!on) { core[i].enabled = false; continue; }
                    float grow = 0.7f + 0.45f * i / N;                                      // 멀어질수록 퍼진다
                    float wob = 1 + 0.22f * Mathf.Sin(t * 38 + i * 0.9f) + 0.12f * Mathf.Sin(t * 23 - i * 1.7f);
                    float w = Mathf.Max(1, Mathf.Round(width * grow * wob * thin));
                    float wi = Mathf.Max(1, Mathf.Round(w * (0.5f + 0.1f * Mathf.Sin(t * 29 + i * 1.3f))));
                    float len = Mathf.Ceil(segLen[i]) + 1;
                    outer[i].transform.localScale = new Vector3(len, w, 1);
                    inner[i].transform.localScale = new Vector3(len, wi, 1);
                    // 거품 줄: 군데군데 끊기고 그 끊김이 앞으로 흘러간다
                    core[i].enabled = ((i + (int)(t * 34)) % 5) != 0;
                    core[i].transform.localScale = new Vector3(len, Mathf.Max(1, Mathf.Round(w * 0.16f)), 1);
                }
            }

            float time = 0;
            for (float t = 0; t < extendSec; t += Time.deltaTime, time += Time.deltaTime)
            {
                float k = t / extendSec;
                int head = Mathf.Clamp(Mathf.CeilToInt(N * (1 - (1 - k) * (1 - k))), 1, N);
                Shape(time, head, 0, 0.8f + 0.2f * k);
                yield return null;
            }
            for (float t = 0; t < holdSec; t += Time.deltaTime, time += Time.deltaTime)
            {
                Shape(time, N, 0, 1);
                // 물줄기 가장자리에서 떨어지는 물방울, 끝에서 튀는 물보라
                float u = Random.Range(0.15f, 0.95f);
                var tangent = At(Mathf.Min(1, u + 0.02f)) - At(u);
                var along = At(u) + new Vector2(-tangent.y, tangent.x).normalized * Random.Range(-width * 0.6f, width * 0.6f);
                Burst(along, light, Color.white, 1, 10, 40, 0.35f, 2, 160);
                if (splash) Spray(to, -dir, 75, light, Color.white, 5, 50, 140, 0.4f, 2, 300);
                yield return null;
            }
            // 끝: 전체 길이를 유지한 채 가늘어지며 옅어진다. 예전처럼 꼬리를 입에서 상대 쪽으로 쓸어 지우면
            // 프레임이 적은 휴대폰에서 상대 근처의 짧은 사선 조각만 한두 프레임 보였다(2026-10-17 실기기).
            for (float t = 0; t < 0.16f; t += Time.deltaTime, time += Time.deltaTime)
            {
                float k = t / 0.16f;
                Shape(time, N, 0, 1 - 0.75f * k);
                for (int i = 0; i < N; i++)
                {
                    outer[i].color = new Color(deepA.r, deepA.g, deepA.b, deepA.a * (1 - k));
                    inner[i].color = new Color(light.r, light.g, light.b, light.a * (1 - k));
                    core[i].color = new Color(1, 1, 1, 1 - k);
                }
                yield return null;
            }
            Object.Destroy(root.gameObject);
        }

        /// <summary>
        /// 한 방향(dir, 화면 좌표)을 중심으로 spreadDeg 각도 안에 퍼지는 입자 (물이 몸에 부딪혀 되튀는 물보라 등). gravity 는 픽셀/초² (아래로 +).
        /// </summary>
        public void Spray(Vector2 at, Vector2 dir, float spreadDeg, Color a, Color b, int count, float speedMin, float speedMax, float life, float size, float gravity)
        {
            var ps = Next();
            var main = ps.main;
            main.gravityModifier = 0;
            var force = ps.forceOverLifetime;
            force.enabled = gravity != 0;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = new ParticleSystem.MinMaxCurve(-gravity);
            force.x = new ParticleSystem.MinMaxCurve(0);
            float baseAngle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;                 // 화면 y 아래 → 월드 y 위
            for (int i = 0; i < count; i++)
            {
                float ang = (baseAngle + Random.Range(-spreadDeg, spreadDeg)) * Mathf.Deg2Rad;
                ps.Emit(new ParticleSystem.EmitParams
                {
                    position = ToLocal(at),
                    velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * Random.Range(speedMin, speedMax),
                    startLifetime = life * Random.Range(0.6f, 1f),
                    startSize = Mathf.Round(size * Random.Range(0.7f, 1.4f)),
                    startColor = Color.Lerp(a, b, Random.value),
                }, 1);
            }
        }

        void Segment(Transform parent, Vector2 a, Vector2 b, float thickness, Color color, int order)
        {
            var len = Vector2.Distance(a, b);
            var sr = world.NewRenderer(parent, "Seg", BattleWorld.PixelLeft, order);
            sr.color = color;
            var t = sr.transform;
            t.localPosition = ToLocal(a);
            t.localScale = new Vector3(len, thickness, 1);
            t.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg);
        }

        /// <summary>구체가 from → to 로 날아간다 (발사형 기술). 도착하면 사라진다.</summary>
        public IEnumerator Orb(Vector2 from, Vector2 to, Color color, int sizePx, float seconds, Color trail)
        {
            var sr = world.NewRenderer(fx, "Orb", circle, BattleWorld.SortFx);
            sr.color = color;
            sr.transform.localScale = new Vector3(sizePx / 16f, sizePx / 16f, 1);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                var p = Vector2.Lerp(from, to, t / seconds);
                p.y -= Mathf.Sin(t / seconds * Mathf.PI) * 14; // 살짝 포물선
                sr.transform.localPosition = ToLocal(new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)));
                Burst(p, trail, color, 1, 0, 6, 0.25f, 2);
                yield return null;
            }
            Object.Destroy(sr.gameObject);
        }

        /// <summary>대상 위치에서 퍼지는 고리 (효과형 기술)</summary>
        public IEnumerator Rings(Vector2 at, Color color, int count, float seconds)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = world.NewRenderer(fx, "Ring", ring, BattleWorld.SortFx);
                sr.transform.localPosition = ToLocal(at);
                float each = seconds / count;
                for (float t = 0; t < each; t += Time.deltaTime)
                {
                    float k = t / each;
                    float s = Mathf.Round(Mathf.Lerp(8, 64, k) / 2) * 2;
                    sr.transform.localScale = new Vector3(s / 32f, s / 32f, 1);
                    var c = color; c.a = 1 - k; sr.color = c;
                    yield return null;
                }
                Object.Destroy(sr.gameObject);
            }
        }

        // ------------------------------------------------------------------ 응원 부활 (성스러운 빛)
        Sprite feather, shaft;

        /// <summary>깃털 7×13 도트: 가운데 깃대, 양쪽 깃은 위가 넓고 아래로 좁아지며, 깃대 아래 2px 은 깃 없이 둔다.</summary>
        static Sprite MakeFeather()
        {
            const int w = 7, h = 13;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Abs(x - 3);
                    float k = (y - 2) / (float)(h - 3);                 // 0 = 깃 아래 끝, 1 = 깃 위 끝 (텍스처 y=0 이 아래)
                    int half = y < 2 ? 0 : Mathf.RoundToInt(3 * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 0.95f + 0.05f)));
                    bool on = dx == 0 || (dx <= half && y >= 2);
                    if (on && dx == half && dx > 0 && (y + dx) % 3 == 0) on = false;   // 가장자리를 군데군데 비워 깃 결이 보이게
                    tex.SetPixel(x, y, on ? (dx == 0 ? new Color(0.85f, 0.8f, 0.6f) : Color.white) : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>빛기둥 텍스처 32×64: 가운데가 진하고 가장자리로 갈수록 옅다(4단계 계단), 아래 끝은 옅어진다. 위 가운데 기준.</summary>
        static Sprite MakeShaft()
        {
            const int w = 32, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / w * 2 - 1);
                    float a = Mathf.Clamp01(1 - u * u) * Mathf.Clamp01(y / 10f) * 0.85f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Floor(a * 4) / 4f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 1f);
        }

        /// <summary>하늘에서 feet 로 내려오는 빛기둥. 화면 위 바깥에서 발밑까지, 폭이 숨 쉬듯 살짝 넓어졌다 좁아진다.</summary>
        public IEnumerator LightShaft(Vector2 feet, float width, Color color, float seconds)
        {
            if (shaft == null) shaft = MakeShaft();
            var r = world.NewRenderer(fx, "LightShaft", shaft, BattleWorld.SortFx + 1);
            const float top = -40;   // 카메라가 다가가도 위 끝이 보이지 않게 화면 위로 넉넉히
            r.transform.localPosition = ToLocal(new Vector2(Mathf.Round(feet.x), top));
            float height = feet.y + 6 - top;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float alpha = Mathf.Min(1, t / 0.25f) * Mathf.Min(1, (seconds - t) / 0.6f);
                float w = width * (0.6f + 0.4f * Mathf.Min(1, t / 0.35f)) * (1 + 0.06f * Mathf.Sin(t * 9));
                r.transform.localScale = new Vector3(Mathf.Round(w) / 32f, height / 64f, 1);
                r.color = new Color(color.r, color.g, color.b, color.a * alpha);
                yield return null;
            }
            Object.Destroy(r.gameObject);
        }

        /// <summary>화면 전체에 성스러운 깃털 count 개가 좌우로 흔들리며 천천히 내려온다 (위치는 정수 픽셀, 기울기는 15° 단위).</summary>
        public IEnumerator Feathers(int count, float seconds, Color tint)
        {
            if (feather == null) feather = MakeFeather();
            var rs = new SpriteRenderer[count];
            var x0 = new float[count]; var y0 = new float[count]; var speed = new float[count];
            var sway = new float[count]; var phase = new float[count]; var delay = new float[count];
            var baseColor = new Color[count];
            for (int i = 0; i < count; i++)
            {
                rs[i] = world.NewRenderer(fx, "Feather" + i, feather, BattleWorld.SortFx + 6);
                rs[i].enabled = false;
                x0[i] = Random.Range(6f, PixelCanvasScaler.BaseWidth - 6f);
                y0[i] = Random.Range(-50f, -8f);
                speed[i] = Random.Range(30f, 52f);
                sway[i] = Random.Range(5f, 12f);
                phase[i] = Random.Range(0f, Mathf.PI * 2);
                delay[i] = Random.Range(0f, seconds * 0.35f);
                baseColor[i] = Color.Lerp(Color.white, tint, Random.Range(0f, 0.45f));
            }
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    float tt = t - delay[i];
                    if (tt < 0) continue;
                    float s = Mathf.Sin(phase[i] + tt * 2.2f);
                    rs[i].enabled = true;
                    rs[i].transform.localPosition = ToLocal(new Vector2(Mathf.Round(x0[i] + s * sway[i]), Mathf.Round(y0[i] + speed[i] * tt)));
                    rs[i].transform.localEulerAngles = new Vector3(0, 0, Mathf.Round(s * 2) * 15);
                    var c = baseColor[i];
                    c.a = Mathf.Min(1, tt / 0.3f) * Mathf.Min(1, (seconds - t) / 0.6f);
                    rs[i].color = c;
                }
                yield return null;
            }
            foreach (var r in rs) Object.Destroy(r.gameObject);
        }

        // ------------------------------------------------------------------ 트레이너가 처리한다 (몬스터볼)
        Sprite pokeBall, dizzyStar;

        /// <summary>몬스터볼 11×11 도트: 검은 테두리, 위 빨강(왼쪽 위 하이라이트)·아래 흰색, 가운데 검은 띠와 흰 단추.</summary>
        static Sprite MakePokeBall()
        {
            const int n = 11;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var red = new Color(0.9f, 0.15f, 0.17f);
            var white = new Color(0.96f, 0.96f, 0.96f);
            var black = new Color(0.1f, 0.1f, 0.13f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - 5, dy = y - 5, d = Mathf.Sqrt(dx * dx + dy * dy);   // 텍스처 y=0 이 아래
                    Color c;
                    if (d > 5.4f) c = Color.clear;
                    else if (d > 4.4f) c = black;
                    else if (d <= 1.1f) c = white;                 // 단추
                    else if (d <= 2.2f || dy == 0) c = black;       // 단추 테두리·가운데 띠
                    else if (dy > 0) c = x == 3 && y == 8 ? new Color(1f, 0.7f, 0.68f) : red;
                    else c = white;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>
        /// 몬스터볼 던지기: from → to 로 포물선(가운데가 arcPx 만큼 높다)을 그리며 45°씩 돌아 날아간다. 도착하면 사라진다(튕겨 나가는 공은 BounceBall).
        /// </summary>
        public IEnumerator ThrowBall(Vector2 from, Vector2 to, float arcPx, float seconds)
        {
            if (pokeBall == null) pokeBall = MakePokeBall();
            var r = world.NewRenderer(fx, "PokeBall", pokeBall, BattleWorld.SortFx + 5);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                var p = Vector2.Lerp(from, to, k);
                p.y -= Mathf.Sin(k * Mathf.PI) * arcPx;
                r.transform.localPosition = ToLocal(new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)));
                r.transform.localEulerAngles = new Vector3(0, 0, -Mathf.Round(k * 10) * 45);   // 시계 방향으로 휙휙
                yield return null;
            }
            Object.Destroy(r.gameObject);
        }

        /// <summary>맞고 튕겨 나간 몬스터볼: at 에서 velocity(픽셀/초, 화면 좌표 y 아래)로 튀어 올랐다가 중력으로 떨어지며 돈다.</summary>
        public IEnumerator BounceBall(Vector2 at, Vector2 velocity, float gravity, float seconds)
        {
            if (pokeBall == null) pokeBall = MakePokeBall();
            var r = world.NewRenderer(fx, "PokeBallBounce", pokeBall, BattleWorld.SortFx + 5);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                var p = at + velocity * t + new Vector2(0, gravity * t * t / 2);
                r.transform.localPosition = ToLocal(new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)));
                r.transform.localEulerAngles = new Vector3(0, 0, Mathf.Round(t * 16) * 45);
                yield return null;
            }
            Object.Destroy(r.gameObject);
        }

        /// <summary>머리 위를 도는 별 3개 (맞고 어지러움). 뒤쪽(타원 위쪽)을 지날 때는 옅다.</summary>
        public IEnumerator Dizzy(Vector2 above, float radius, float seconds)
        {
            if (dizzyStar == null) dizzyStar = MakeSprite(5, (x, y) => x == 2 || y == 2 || (Mathf.Abs(x - 2) == 1 && Mathf.Abs(y - 2) == 1));
            var rs = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++) rs[i] = world.NewRenderer(fx, "DizzyStar" + i, dizzyStar, BattleWorld.SortFx + 6);
            var yellow = new Color(1f, 0.92f, 0.35f);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float fade = Mathf.Min(1, (seconds - t) / 0.3f);
                for (int i = 0; i < 3; i++)
                {
                    float a = t * 7 + i * Mathf.PI * 2 / 3;
                    var p = above + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.35f);
                    rs[i].transform.localPosition = ToLocal(new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)));
                    var c = yellow; c.a = (Mathf.Sin(a) > 0 ? 1 : 0.55f) * fade;
                    rs[i].color = c;
                }
                yield return null;
            }
            foreach (var r in rs) Object.Destroy(r.gameObject);
        }

        /// <summary>화면 전체 번쩍임 (급소 등). HUD 맨 위 Image 라서 글자까지 덮는다.</summary>
        public IEnumerator ScreenFlash(Color color, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                var c = color; c.a = color.a * (1 - t / seconds);
                flash.color = c;
                yield return null;
            }
            flash.color = new Color(1, 1, 1, 0);
        }

        /// <summary>화면 흔들림 (정수 픽셀). 월드 루트와 HUD 영역을 같은 만큼 움직인다.</summary>
        public IEnumerator Shake(int amplitude, float seconds)
        {
            var worldHome = world.Root.localPosition;
            var hudHome = hudScreen.anchoredPosition;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = 1 - t / seconds;
                var o = new Vector2(Mathf.Round(Random.Range(-1f, 1f) * amplitude * k), Mathf.Round(Random.Range(-1f, 1f) * amplitude * k));
                world.Root.localPosition = worldHome + (Vector3)o;
                hudScreen.anchoredPosition = hudHome + o;
                yield return null;
            }
            world.Root.localPosition = worldHome;
            hudScreen.anchoredPosition = hudHome;
        }

        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
