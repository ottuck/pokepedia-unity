using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>배경 테마 색 (PROJECT.md 5.5). 지금은 비 오는 저녁 하나. Standard 용 공통 배경은 M5에서 테마만 추가한다.</summary>
    public sealed class BackdropTheme
    {
        public Color[] SkyBands;           // 위 → 아래
        public Color CloudDark, CloudLight;
        public Color Hill, Trees, GroundFar, GroundNear, Fog;
        public float BackFogAlpha, FrontFogAlpha;
        public bool Rain;
        public Color Ambient;              // 전역 조명 색 (곱하기)
        public float VignetteAlpha;

        public static BackdropTheme Storm => new BackdropTheme
        {
            SkyBands = new[]
            {
                Rgb(0x2B3548), Rgb(0x354258), Rgb(0x42536B), Rgb(0x526580), Rgb(0x66798F), Rgb(0x7D91A6),
            },
            CloudDark = Rgb(0x222C3C), CloudLight = Rgb(0x3A475B),
            Hill = Rgb(0x435566), Trees = Rgb(0x263A33),
            GroundFar = Rgb(0x607C63), GroundNear = Rgb(0x3F5C42),
            Fog = Rgb(0xB9C7D6),
            BackFogAlpha = 0.22f, FrontFogAlpha = 0.10f,
            Rain = true,
            Ambient = new Color(0.86f, 0.91f, 1f),
            VignetteAlpha = 0.55f,
        };

        static Color Rgb(int v) => new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
    }

    /// <summary>
    /// 배틀 배경 (PROJECT.md 5.5): 하늘·구름·먼 언덕·나무·원근 바닥을 레이어로 나누고, 카메라가 움직일 때 레이어마다 다르게 움직인다(패럴랙스).
    /// 안개·비·빗방울 물결·번개 번쩍임·비네트를 얹는다. 모든 그림은 코드로 그린 도트 텍스처라 파일이 없다(용량도 거의 0).
    /// 좌표는 HUD 와 같은 "화면 픽셀, 좌상단 원점, y 아래로". 레이어 폭은 기준 해상도보다 넓게 잡아 패럴랙스·흔들림이 가장자리를 드러내지 않게 한다.
    /// </summary>
    public sealed class BattleBackdrop
    {
        const int W = 304;          // 레이어 폭 (기준 240 + 좌우 32)
        const int Horizon = 62;     // 지평선 (화면 픽셀 y)
        const int BaseH = 216;

        // 그리는 순서: 하늘 → 구름 → 언덕 → 나무 → 바닥 → 안개(뒤) → 물결 → [발판 10, 그림자 11, 포켓몬 20] → 안개(앞) → 이펙트 30 → 비 → 비네트 → 번개
        const int SortSky = 1, SortClouds = 2, SortHills = 3, SortTrees = 4, SortGround = 5, SortBackFog = 6, SortRipple = 7;
        const int SortFrontFog = 25, SortRain = 40, SortVignette = 50, SortLightning = 55;

        sealed class Layer
        {
            public Transform T;
            public float Factor;   // 카메라가 움직일 때 화면에서 따라 움직이는 비율 (0 = 고정, 1 = 포켓몬과 같이)
            public float Drift;    // 초당 흐르는 픽셀 (0 이면 안 흐름)
            public float Tile;     // 흐르는 레이어의 타일 폭
            public float Offset;
        }

        readonly BattleWorld world;
        readonly BackdropTheme theme;
        readonly List<Layer> layers = new List<Layer>();
        readonly SpriteRenderer lightning;
        readonly SpriteRenderer[] ripples = new SpriteRenderer[10];
        readonly float[] rippleAge = new float[10];
        readonly Sprite ringSprite;
        float pan;
        float nextRipple;
        float nextBolt = 5f;
        float boltT = -1;

        public BattleBackdrop(BattleWorld world, BackdropTheme theme)
        {
            this.world = world;
            this.theme = theme;
            var root = world.Root;

            Add("Sky", SkySprite(), new Vector2(120, 0), SortSky, 0.05f, new Vector2(W, 1));
            Add("Clouds", CloudSprite(240, 36), new Vector2(120, 6), SortClouds, 0.15f, Vector2.one, tile: 240, drift: 3f, tiledWidth: W);
            Add("Hills", HillSprite(), new Vector2(120, Horizon - 40), SortHills, 0.3f, Vector2.one);
            Add("Trees", TreeSprite(), new Vector2(120, Horizon - 22), SortTrees, 0.55f, Vector2.one);
            Add("Ground", GroundSprite(), new Vector2(120, Horizon), SortGround, 1f, Vector2.one);

            var fogTile = FogSprite();
            var back = Add("FogBack", fogTile, new Vector2(120, Horizon - 12), SortBackFog, 0.7f, Vector2.one, tile: 120, drift: 5f, tiledWidth: W, lit: false);
            back.Item2.color = new Color(theme.Fog.r, theme.Fog.g, theme.Fog.b, theme.BackFogAlpha);
            var front = Add("FogFront", fogTile, new Vector2(120, 118), SortFrontFog, 1f, Vector2.one, tile: 120, drift: -9f, tiledWidth: W, lit: false);
            front.Item2.color = new Color(theme.Fog.r, theme.Fog.g, theme.Fog.b, theme.FrontFogAlpha);

            ringSprite = RingSprite(16);
            for (int i = 0; i < ripples.Length; i++)
            {
                var sr = world.NewRenderer(root, "Ripple" + i, ringSprite, SortRipple);
                sr.enabled = false;
                ripples[i] = sr;
            }

            if (theme.Rain) CreateRain();

            var vignette = world.NewRenderer(world.Root, "Vignette", VignetteSprite(), SortVignette);
            vignette.transform.localPosition = Vector3.zero;
            lightning = world.Quad(root, "Lightning", Vector2.zero, PixelW, BaseH, new Color(0.85f, 0.9f, 1f, 0f), SortLightning);
        }

        const int PixelW = 240;

        // ------------------------------------------------------------------ 시간·카메라
        /// <summary>카메라를 옆으로 움직인 양(픽셀). 월드 루트는 BattleWorld 가 움직이고, 여기서는 먼 레이어를 덜 따라가게 보정한다.</summary>
        public void SetPan(float value)
        {
            pan = value;
            Apply();
        }

        public void Tick(float dt)
        {
            foreach (var l in layers)
            {
                if (l.Drift == 0) continue;
                l.Offset += l.Drift * dt;
                l.Offset -= Mathf.Floor(l.Offset / l.Tile) * l.Tile;
            }
            Apply();
            TickRipples(dt);
            TickLightning(dt);
        }

        void Apply()
        {
            foreach (var l in layers)
            {
                float x = Mathf.Round(pan * (1 - l.Factor));
                if (l.Drift != 0) x -= Mathf.Round(l.Offset);
                var p = l.T.localPosition;
                l.T.localPosition = new Vector3(x, p.y, p.z);
            }
        }

        void TickRipples(float dt)
        {
            if (!theme.Rain) return;
            nextRipple -= dt;
            if (nextRipple <= 0)
            {
                nextRipple = UnityEngine.Random.Range(0.06f, 0.16f);
                for (int i = 0; i < ripples.Length; i++)
                {
                    if (ripples[i].enabled) continue;
                    float y = UnityEngine.Random.Range(Horizon + 12, 148);
                    float x = UnityEngine.Random.Range(6, 234);
                    ripples[i].transform.localPosition = BattleWorld.ToLocal(new Vector2(Mathf.Round(x), Mathf.Round(y)));
                    rippleAge[i] = 0;
                    ripples[i].enabled = true;
                    break;
                }
            }
            for (int i = 0; i < ripples.Length; i++)
            {
                if (!ripples[i].enabled) continue;
                rippleAge[i] += dt;
                float k = rippleAge[i] / 0.45f;
                if (k >= 1) { ripples[i].enabled = false; continue; }
                float s = Mathf.Lerp(0.35f, 0.9f, k);
                ripples[i].transform.localScale = new Vector3(s, s * 0.4f, 1);   // 원근: 가로로 납작하게
                ripples[i].color = new Color(0.85f, 0.92f, 1f, 0.45f * (1 - k));
            }
        }

        void TickLightning(float dt)
        {
            if (!theme.Rain) return;
            if (boltT < 0)
            {
                nextBolt -= dt;
                if (nextBolt <= 0) { boltT = 0; nextBolt = UnityEngine.Random.Range(7f, 14f); }
                return;
            }
            boltT += dt;
            float a;
            if (boltT < 0.05f) a = 0.30f;
            else if (boltT < 0.11f) a = 0.04f;
            else if (boltT < 0.17f) a = 0.22f;
            else a = Mathf.Max(0, 0.22f * (1 - (boltT - 0.17f) / 0.35f));
            lightning.color = new Color(0.85f, 0.9f, 1f, a);
            if (boltT > 0.55f) { boltT = -1; lightning.color = new Color(0.85f, 0.9f, 1f, 0f); }
        }

        // ------------------------------------------------------------------ 레이어 만들기
        System.Tuple<Layer, SpriteRenderer> Add(string name, Sprite sprite, Vector2 anchorPx, int order, float factor, Vector2 scale,
                                                float tile = 0, float drift = 0, float tiledWidth = 0, bool lit = true)
        {
            var sr = world.NewRenderer(world.Root, name, sprite, order, lit);
            sr.transform.localPosition = BattleWorld.ToLocal(anchorPx);
            if (tiledWidth > 0)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(tiledWidth + tile, sprite.rect.height);   // 흘러도 한쪽이 비지 않게 타일 한 장만큼 더 넓게
            }
            else sr.transform.localScale = new Vector3(scale.x, scale.y, 1);
            var layer = new Layer { T = sr.transform, Factor = factor, Drift = drift, Tile = tile };
            layers.Add(layer);
            return System.Tuple.Create(layer, sr);
        }

        // ------------------------------------------------------------------ 그리기 (모두 코드로, 도트 텍스처)
        static Sprite ToSprite(Color32[] px, int w, int h, Vector2 pivot, bool repeat = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 1f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>위에서부터 y 번째 줄(0 = 맨 위)을 텍스처 배열 인덱스로 바꾼다 (텍스처는 아래가 0)</summary>
        static int I(int x, int y, int w, int h) => (h - 1 - y) * w + x;

        Sprite SkySprite()
        {
            int h = Horizon + 10, n = theme.SkyBands.Length;
            var px = new Color32[h];
            for (int y = 0; y < h; y++) px[I(0, y, 1, h)] = theme.SkyBands[Mathf.Min(n - 1, y * n / h)];
            return ToSprite(px, 1, h, new Vector2(0.5f, 1f));
        }

        Sprite CloudSprite(int w, int h)
        {
            var px = new Color32[w * h];
            var rnd = new System.Random(11);
            for (int i = 0; i < 9; i++)
            {
                int cx = rnd.Next(w), cy = 8 + rnd.Next(h - 16), rx = 22 + rnd.Next(26), ry = 5 + rnd.Next(6);
                Color32 col = i % 3 == 0 ? theme.CloudLight : theme.CloudDark;
                for (int shift = -w; shift <= w; shift += w)
                    for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(h - 1, cy + ry); y++)
                        for (int x = cx + shift - rx; x <= cx + shift + rx; x++)
                        {
                            float dx = (x - (cx + shift)) / (float)rx, dy = (y - cy) / (float)ry, d = dx * dx + dy * dy;
                            if (d > 1) continue;
                            int xx = ((x % w) + w) % w;
                            var c = col; c.a = (byte)(d > 0.7f ? 140 : 215);
                            if (c.a > px[I(xx, y, w, h)].a) px[I(xx, y, w, h)] = c;
                        }
            }
            return ToSprite(px, w, h, new Vector2(0.5f, 1f), repeat: true);
        }

        Sprite HillSprite()
        {
            int h = 44;
            var px = new Color32[W * h];
            var rnd = new System.Random(5);
            for (int x = 0; x < W; x++)
            {
                int top = h - Mathf.RoundToInt(14 + 10 * Mathf.Sin(x * 0.035f) + 7 * Mathf.Sin(x * 0.083f + 1.3f) + rnd.Next(0, 2));
                for (int y = Mathf.Max(0, top); y < h; y++) px[I(x, y, W, h)] = theme.Hill;
            }
            return ToSprite(px, W, h, new Vector2(0.5f, 1f));
        }

        Sprite TreeSprite()
        {
            int h = 40;
            var px = new Color32[W * h];
            var tops = new int[W];
            for (int x = 0; x < W; x++) tops[x] = h - 8;                       // 낮은 숲 바닥
            var rnd = new System.Random(9);
            for (int xi = 4; xi < W; xi += 8 + rnd.Next(0, 7))
            {
                int hw = 5 + rnd.Next(0, 4), th = 18 + rnd.Next(0, 16);
                for (int x = Mathf.Max(0, xi - hw); x <= Mathf.Min(W - 1, xi + hw); x++)
                {
                    int top = h - th + Mathf.RoundToInt(Mathf.Abs(x - xi) * (th / (float)hw) * 0.85f);
                    if (top < tops[x]) tops[x] = top;
                }
            }
            for (int x = 0; x < W; x++)
            {
                var c = (Color32)theme.Trees;
                if (x % 3 == 0) c = Color32.Lerp(c, new Color32(0, 0, 0, 255), 0.12f);
                for (int y = Mathf.Max(0, tops[x]); y < h; y++) px[I(x, y, W, h)] = c;
            }
            return ToSprite(px, W, h, new Vector2(0.5f, 1f));
        }

        Sprite GroundSprite()
        {
            int h = BaseH - Horizon;
            var px = new Color32[W * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                var baseC = Color.Lerp(theme.GroundFar, theme.GroundNear, t);
                float denom = y + 46f;   // 소실점은 지평선 46px 위 (원근 줄무늬가 모이는 곳)
                for (int x = 0; x < W; x++)
                {
                    var c = baseC;
                    int stripe = Mathf.FloorToInt((x - W / 2f) / denom * 9f) & 1;
                    if (stripe == 1) c *= 0.93f;
                    if (y < 10) c = Color.Lerp(c, theme.Fog, (10 - y) / 10f * 0.55f);   // 지평선 안개
                    c.a = 1;
                    px[I(x, y, W, h)] = c;
                }
            }
            return ToSprite(px, W, h, new Vector2(0.5f, 1f));
        }

        Sprite FogSprite()
        {
            const int w = 120, h = 32, cellsX = 12, cellsY = 4;
            var rnd = new System.Random(3);
            var grid = new float[cellsX, cellsY];
            for (int cx = 0; cx < cellsX; cx++) for (int cy = 0; cy < cellsY; cy++) grid[cx, cy] = (float)rnd.NextDouble();
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)w * cellsX, fy = y / (float)h * cellsY;
                    int x0 = Mathf.FloorToInt(fx) % cellsX, x1 = (x0 + 1) % cellsX, y0 = Mathf.FloorToInt(fy) % cellsY, y1 = (y0 + 1) % cellsY;
                    float tx = Smooth(fx - Mathf.Floor(fx)), ty = Smooth(fy - Mathf.Floor(fy));
                    float v = Mathf.Lerp(Mathf.Lerp(grid[x0, y0], grid[x1, y0], tx), Mathf.Lerp(grid[x0, y1], grid[x1, y1], tx), ty);
                    float edge = Mathf.Sin(Mathf.PI * (y + 0.5f) / h);                 // 위아래로 옅어진다
                    float a = Mathf.Clamp01((v * 1.5f - 0.35f)) * edge;
                    px[I(x, y, w, h)] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
                }
            return ToSprite(px, w, h, new Vector2(0.5f, 1f), repeat: true);
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);

        static Sprite RingSprite(int n)
        {
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r, d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * n + x] = d <= 1f && d >= 0.72f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            return ToSprite(px, n, n, new Vector2(0.5f, 0.5f));
        }

        Sprite VignetteSprite()
        {
            int w = PixelW, h = BaseH;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float s = Smooth(Mathf.Clamp01((r - 0.55f) / 0.6f));
                    px[y * w + x] = new Color32(6, 10, 18, (byte)Mathf.RoundToInt(s * theme.VignetteAlpha * 255));
                }
            return ToSprite(px, w, h, new Vector2(0.5f, 0.5f));
        }

        void CreateRain()
        {
            var go = new GameObject("Rain", typeof(ParticleSystem));
            go.transform.SetParent(world.Root, false);
            go.transform.localPosition = new Vector3(0, BaseH / 2f + 30, 0);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 1.05f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = new Color(0.78f, 0.87f, 1f, 0.55f);
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 170f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(W + 60, 1, 1);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-34f);
            vel.y = new ParticleSystem.MinMaxCurve(-250f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = world.Unlit;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 5f;
            renderer.sortingOrder = SortRain;
            ps.Play();
        }
    }

    /// <summary>배경 시간(구름·안개가 흐르는 것, 물결, 번개)을 한 프레임씩 진행시킨다.</summary>
    public sealed class BackdropTicker : MonoBehaviour
    {
        public Action<float> OnTick;
        void Update() => OnTick?.Invoke(Time.deltaTime);
    }
}
