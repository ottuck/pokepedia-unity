using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>배경 테마 색 (PROJECT.md 5.5). 지금은 비 오는 저녁 하나. Standard 용 공통 배경은 M5에서 테마만 추가한다.</summary>
    public sealed class BackdropTheme
    {
        public Color[] Sky;                 // 위 → 지평선
        public Color[] ClearSky;            // 승리 뒤 비가 그친 하늘 (노을, 위 → 지평선)
        public Color HorizonGlow;           // 구름 뒤 해가 비치는 지평선 쪽 빛
        public Color CloudDark, CloudMid, CloudLight;
        public Color RidgeFar, RidgeNear, RidgeLight, Mist;
        public Color ForestBack, ForestFront, ForestLight;
        public Color[] Ground;              // 먼 곳 → 가까운 곳
        public Color GrassTip, GrassShade, Wet, Puddle;
        public Color Fog;
        public float BackFogAlpha, FrontFogAlpha;
        public bool Rain;
        public Color Ambient;               // 전역 조명 색 (곱하기)
        public Color ClearAmbient;          // 승리 뒤 비가 그쳤을 때의 전역 조명
        public float VignetteAlpha;

        public static BackdropTheme Storm => new BackdropTheme
        {
            Sky = new[] { Rgb(0x1E2536), Rgb(0x262F44), Rgb(0x313C54), Rgb(0x3E4B64), Rgb(0x505E77), Rgb(0x67748A), Rgb(0x818D9F) },
            ClearSky = new[] { Rgb(0x4A72AE), Rgb(0x6C93C6), Rgb(0x9DB4D6), Rgb(0xD2C3C8), Rgb(0xF0C8A0), Rgb(0xF8D894), Rgb(0xFFEDC0) },
            HorizonGlow = Rgb(0xC6A88C),
            CloudDark = Rgb(0x1A2130), CloudMid = Rgb(0x2B3547), CloudLight = Rgb(0x5A677D),
            RidgeFar = Rgb(0x66788E), RidgeNear = Rgb(0x36455A), RidgeLight = Rgb(0x9AA9BA), Mist = Rgb(0x8A99AB),
            ForestBack = Rgb(0x2E4248), ForestFront = Rgb(0x1B2D2A), ForestLight = Rgb(0x3E5A4D),
            Ground = new[] { Rgb(0x6C8778), Rgb(0x5A7864), Rgb(0x4A6A51), Rgb(0x3D5D43), Rgb(0x324F38), Rgb(0x2A4430) },
            GrassTip = Rgb(0x86AC80), GrassShade = Rgb(0x1F3626), Wet = Rgb(0xA3B3C1), Puddle = Rgb(0x6F8094),
            Fog = Rgb(0xB9C7D6),
            BackFogAlpha = 0.24f, FrontFogAlpha = 0.09f,
            Rain = true,
            Ambient = new Color(0.8f, 0.85f, 0.96f),
            ClearAmbient = new Color(1f, 0.96f, 0.9f),
            VignetteAlpha = 0.5f,
        };

        static Color Rgb(int v) => new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
    }

    /// <summary>
    /// 배틀 배경 (PROJECT.md 5.5): 하늘·구름 2겹·먼 산 2겹·숲 2겹·원근 풀밭을 레이어로 나누고, 카메라가 움직일 때 레이어마다 다르게 움직인다(패럴랙스).
    /// 안개·비·빗방울 물결·번개 번쩍임·비네트를 얹고, 승리하면 비가 그치며 하늘이 밝아진다(SetWeather).
    /// 모든 그림은 코드로 그린 도트 텍스처다(파일 없음). 색 단계는 Bayer 4×4 디더링으로 섞어 게임보이·GBA 느낌을 낸다.
    /// 좌표는 HUD 와 같은 "화면 픽셀, 좌상단 원점, y 아래로". 레이어 폭은 기준 해상도보다 넓게 잡아 패럴랙스·흔들림이 가장자리를 드러내지 않게 한다.
    /// </summary>
    public sealed class BattleBackdrop
    {
        const int W = 304;          // 레이어 폭 (기준 240 + 좌우 32)
        const int Horizon = 68;     // 지평선 (화면 픽셀 y)
        const int BaseW = 240, BaseH = 216;
        const float RainRate = 170f;

        // 그리는 순서: 하늘 → 맑은 하늘(승리 뒤) → 먼 구름 → 해 번짐 → 먼 산 → 가까운 구름 → 가까운 산 → 골짜기 안개 → 숲(뒤) → 숲(앞) → 풀밭 → 물결
        //            → [그림자 12, 포켓몬 20] → 안개(앞) 25 → 이펙트 30 → 비 40 → 비네트 50 → 번개 55
        const int SortSky = 0, SortClearSky = 1, SortCloudFar = 2, SortSun = 3, SortRidgeFar = 4, SortCloudNear = 5, SortRidgeNear = 6, SortBackFog = 7;
        const int SortForestBack = 8, SortForestFront = 9, SortGround = 10, SortRipple = 11;
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
        readonly List<Layer> layers = new List<Layer>();
        readonly SpriteRenderer lightning, sun, cloudNear, cloudFar, clearSky, fogBack, fogFront;
        readonly SpriteRenderer[] ripples = new SpriteRenderer[12];
        readonly float[] rippleAge = new float[12];
        ParticleSystem rain;
        float pan;
        float nextRipple;
        float nextBolt = 5f;
        float boltT = -1;
        float weather = 1, weatherTarget = 1, weatherSpeed;

        public BackdropTheme Theme { get; }

        public BattleBackdrop(BattleWorld world, BackdropTheme theme)
        {
            this.world = world;
            Theme = theme;
            var root = world.Root;

            Add("Sky", SkySprite(theme.Sky, 0.55f), new Vector2(120, 0), SortSky, 0.04f);
            clearSky = Add("ClearSky", SkySprite(theme.ClearSky, 0.8f), new Vector2(120, 0), SortClearSky, 0.04f).Item2;
            cloudFar = Add("CloudsFar", CloudSprite(240, 30, 11, 0.42f, 6, 3, theme.CloudMid, theme.CloudLight, theme.CloudLight), new Vector2(120, 6),
                SortCloudFar, 0.08f, tile: 240, drift: 1.5f).Item2;
            sun = Add("SunGlow", SunSprite(), new Vector2(120, 0), SortSun, 0.06f, lit: false).Item2;
            Add("RidgeFar", RidgeSprite(52, 19, 18, 26, 0.02f, theme.RidgeFar, false), new Vector2(120, Horizon - 50), SortRidgeFar, 0.12f);
            cloudNear = Add("CloudsNear", CloudSprite(240, 40, 23, 0.5f, 5, 2, theme.CloudDark, theme.CloudMid, theme.CloudLight), new Vector2(120, -6),
                SortCloudNear, 0.16f, tile: 240, drift: 4f).Item2;
            Add("RidgeNear", RidgeSprite(40, 37, 12, 18, 0.03f, theme.RidgeNear, true), new Vector2(120, Horizon - 38), SortRidgeNear, 0.22f);

            var fogTile = FogSprite();
            fogBack = Add("FogBack", fogTile, new Vector2(120, Horizon - 18), SortBackFog, 0.3f, tile: 120, drift: 5f, lit: false).Item2;
            Add("ForestBack", ForestSprite(18, 41, 7, 12, 5, 9, theme.ForestBack, theme.ForestLight, 0.4f), new Vector2(120, Horizon - 13), SortForestBack, 0.38f);
            Add("ForestFront", ForestSprite(24, 53, 9, 17, 6, 13, theme.ForestFront, theme.ForestLight, 0f), new Vector2(120, Horizon - 18), SortForestFront, 0.55f);
            Add("Ground", GroundSprite(), new Vector2(120, Horizon), SortGround, 1f);
            fogFront = Add("FogFront", fogTile, new Vector2(120, 116), SortFrontFog, 1f, tile: 120, drift: -9f, lit: false).Item2;

            var ring = RingSprite(16);
            for (int i = 0; i < ripples.Length; i++)
            {
                var sr = world.NewRenderer(root, "Ripple" + i, ring, SortRipple);
                sr.enabled = false;
                ripples[i] = sr;
            }

            if (theme.Rain) CreateRain();

            world.NewRenderer(root, "Vignette", VignetteSprite(), SortVignette).transform.localPosition = Vector3.zero;
            lightning = world.Quad(root, "Lightning", Vector2.zero, BaseW, BaseH, new Color(0.85f, 0.9f, 1f, 0f), SortLightning);
            ApplyWeather();
        }

        // ------------------------------------------------------------------ 시간·카메라·날씨
        /// <summary>카메라를 옆으로 움직인 양(픽셀). 월드 루트는 BattleWorld 가 움직이고, 여기서는 먼 레이어를 덜 따라가게 보정한다.</summary>
        public void SetPan(float value)
        {
            pan = value;
            ApplyLayers();
        }

        /// <summary>날씨: 1 = 비 오는 저녁(기본), 0 = 비가 그치고 하늘이 밝아짐. seconds 가 0 이면 바로 바꾼다.</summary>
        public void SetWeather(float target, float seconds)
        {
            weatherTarget = Mathf.Clamp01(target);
            if (seconds <= 0) { weather = weatherTarget; weatherSpeed = 0; ApplyWeather(); }
            else weatherSpeed = Mathf.Abs(weatherTarget - weather) / seconds;
        }

        public void Tick(float dt)
        {
            foreach (var l in layers)
            {
                if (l.Drift == 0) continue;
                l.Offset += l.Drift * dt;
                l.Offset -= Mathf.Floor(l.Offset / l.Tile) * l.Tile;
            }
            ApplyLayers();
            if (weather != weatherTarget)
            {
                weather = Mathf.MoveTowards(weather, weatherTarget, weatherSpeed * dt);
                ApplyWeather();
            }
            TickRipples(dt);
            TickLightning(dt);
        }

        void ApplyLayers()
        {
            foreach (var l in layers)
            {
                float x = Mathf.Round(pan * (1 - l.Factor));
                if (l.Drift != 0) x -= Mathf.Round(l.Offset);
                var p = l.T.localPosition;
                l.T.localPosition = new Vector3(x, p.y, p.z);
            }
        }

        void ApplyWeather()
        {
            float w = weather;
            if (rain != null)
            {
                var emission = rain.emission;
                emission.rateOverTime = RainRate * w;
            }
            fogBack.color = WithAlpha(Theme.Fog, Theme.BackFogAlpha * (0.45f + 0.55f * w));
            fogFront.color = WithAlpha(Theme.Fog, Theme.FrontFogAlpha * w);
            cloudNear.color = WithAlpha(Color.white, 0.1f + 0.9f * w);
            cloudFar.color = WithAlpha(Color.white, 0.25f + 0.75f * w);
            clearSky.color = WithAlpha(Color.white, 1 - w);
            sun.color = WithAlpha(Color.white, 0.12f + 0.8f * (1 - w));
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        void TickRipples(float dt)
        {
            if (!Theme.Rain) return;
            nextRipple -= dt;
            if (nextRipple <= 0 && weather > 0.2f)
            {
                nextRipple = UnityEngine.Random.Range(0.05f, 0.14f) / weather;
                for (int i = 0; i < ripples.Length; i++)
                {
                    if (ripples[i].enabled) continue;
                    float y = UnityEngine.Random.Range(Horizon + 10, 150);
                    float x = UnityEngine.Random.Range(6, BaseW - 6);
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
                var p = ripples[i].transform.localPosition;
                float depth = Mathf.InverseLerp(BaseH / 2f - Horizon, -BaseH / 2f, p.y);    // 멀수록 작게
                float s = Mathf.Lerp(0.35f, 0.9f, k) * Mathf.Lerp(0.5f, 1.2f, depth);
                ripples[i].transform.localScale = new Vector3(s, s * 0.38f, 1);
                ripples[i].color = new Color(0.85f, 0.92f, 1f, 0.42f * (1 - k));
            }
        }

        void TickLightning(float dt)
        {
            if (!Theme.Rain) return;
            if (boltT < 0)
            {
                if (weather < 0.6f) return;
                nextBolt -= dt;
                if (nextBolt <= 0) { boltT = 0; nextBolt = UnityEngine.Random.Range(7f, 14f); }
                return;
            }
            boltT += dt;
            float a;
            if (boltT < 0.05f) a = 0.28f;
            else if (boltT < 0.11f) a = 0.04f;
            else if (boltT < 0.17f) a = 0.2f;
            else a = Mathf.Max(0, 0.2f * (1 - (boltT - 0.17f) / 0.35f));
            lightning.color = new Color(0.85f, 0.9f, 1f, a);
            if (boltT > 0.55f) { boltT = -1; lightning.color = new Color(0.85f, 0.9f, 1f, 0f); }
        }

        // ------------------------------------------------------------------ 레이어 만들기
        Tuple<Layer, SpriteRenderer> Add(string name, Sprite sprite, Vector2 anchorPx, int order, float factor,
                                         float tile = 0, float drift = 0, bool lit = true)
        {
            var sr = world.NewRenderer(world.Root, name, sprite, order, lit);
            sr.transform.localPosition = BattleWorld.ToLocal(anchorPx);
            if (tile > 0)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(W + tile, sprite.rect.height);   // 흘러도 한쪽이 비지 않게 타일 한 장만큼 더 넓게
            }
            var layer = new Layer { T = sr.transform, Factor = factor, Drift = drift, Tile = tile };
            layers.Add(layer);
            return Tuple.Create(layer, sr);
        }

        // ------------------------------------------------------------------ 도트 그리기 도구
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>Bayer 4×4 문턱값 (0~1)</summary>
        static float Th(int x, int y) => (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

        /// <summary>팔레트 위의 연속값 v(0 ~ 길이-1)를 디더링으로 이웃 두 색 중 하나로 고른다</summary>
        static Color Ramp(Color[] pal, float v, int x, int y)
        {
            v = Mathf.Clamp(v, 0, pal.Length - 1);
            int i = Mathf.Min((int)v, pal.Length - 2);
            return v - i > Th(x, y) ? pal[i + 1] : pal[i];
        }

        /// <summary>a → b 를 steps 단계로 나누고 디더링으로 섞는다 (t: 0~1)</summary>
        static Color Mix(Color a, Color b, float t, int x, int y, int steps = 4)
        {
            float s = Mathf.Clamp01(t) * steps;
            int i = (int)s;
            if (s - i > Th(x, y)) i++;
            return Color.Lerp(a, b, Mathf.Min(i, steps) / (float)steps);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1103515245u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;
        static float Smooth(float t) => t * t * (3 - 2 * t);

        /// <summary>값 노이즈. periodX 가 0 보다 크면 가로로 그 칸 수마다 반복된다(타일링).</summary>
        static float Noise(float x, float y, int seed, int periodX = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            int xa = periodX > 0 ? Mod(x0, periodX) : x0, xb = periodX > 0 ? Mod(x0 + 1, periodX) : x0 + 1;
            float a = Mathf.Lerp(Hash(xa, y0, seed), Hash(xb, y0, seed), fx);
            float b = Mathf.Lerp(Hash(xa, y0 + 1, seed), Hash(xb, y0 + 1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float x, float y, int seed, int octaves, int periodX = 0)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise(x, y, seed + o * 97, periodX);
                norm += amp;
                x *= 2; y *= 2; amp *= 0.5f;
                if (periodX > 0) periodX *= 2;
            }
            return sum / norm;
        }

        sealed class PixelCanvas
        {
            public readonly int W, H;
            public readonly Color32[] Px;
            public PixelCanvas(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }
            /// <summary>위에서부터 y 번째 줄(0 = 맨 위) 기준으로 찍는다 (텍스처는 아래가 0)</summary>
            public void Set(int x, int y, Color c) { if (x >= 0 && x < W && y >= 0 && y < H) Px[(H - 1 - y) * W + x] = c; }
            public Color Get(int x, int y) => x >= 0 && x < W && y >= 0 && y < H ? (Color)Px[(H - 1 - y) * W + x] : Color.clear;

            public Sprite ToSprite(Vector2 pivot, bool repeat = false)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp };
                tex.SetPixels32(Px);
                tex.Apply(false, true);
                return Sprite.Create(tex, new Rect(0, 0, W, H), pivot, 1f, 0, SpriteMeshType.FullRect);
            }
        }

        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

        // ------------------------------------------------------------------ 레이어 그림
        /// <summary>하늘: 7색 그라디언트를 디더링으로 잇고, 낮은 주파수 노이즈로 띠를 깨며, 지평선 왼쪽에 구름 뒤 해의 은은한 빛을 얹는다.</summary>
        Sprite SkySprite(Color[] pal, float glow)
        {
            int h = Horizon + 10;
            var c = new PixelCanvas(W, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)(h - 1) * (pal.Length - 1) + (Fbm(x * 0.02f, y * 0.06f, 3, 3) - 0.5f) * 0.9f;
                    var col = Ramp(pal, v, x, y);
                    float dx = (x - W * 0.3f) / 120f, dy = (y - (h - 4)) / 30f;
                    float g = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    if (g > 0) col = Mix(col, Theme.HorizonGlow, g * g * glow, x, y, 5);
                    c.Set(x, y, col);
                }
            return c.ToSprite(TopCenter);
        }

        /// <summary>해 번짐: 비가 그치면 강해지는 따뜻한 빛(조명을 받지 않는 반투명 레이어)</summary>
        Sprite SunSprite()
        {
            int h = Horizon + 10;
            var c = new PixelCanvas(W, h);
            Color warm = Color.Lerp(Theme.HorizonGlow, Color.white, 0.3f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - W * 0.3f) / 150f, dy = (y - (h - 2)) / 52f;
                    float g = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Round(g * g * 6) / 6f;            // 6단계 띠 (도트 느낌)
                    if (a > 0 && a * 0.999f + 0.001f > Th(x, y) * 0.35f) c.Set(x, y, WithAlpha(warm, a * 0.8f));
                }
            return c.ToSprite(TopCenter);
        }

        /// <summary>
        /// 구름: 가로로 이어지는 fBm 노이즈를 밀도로 쓰고, 위쪽 가장자리는 밝게(하늘빛), 아래쪽은 중간색, 속은 어둡게 칠한다.
        /// coverage 가 클수록 하늘을 많이 덮는다. 세로로는 가운데가 두껍게 한다.
        /// </summary>
        Sprite CloudSprite(int w, int h, int seed, float coverage, int cellsX, int cellsY, Color body, Color under, Color rim)
        {
            var density = new float[w, h];
            for (int y = 0; y < h; y++)
            {
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * (y + 0.5f) / h), 0.6f);
                for (int x = 0; x < w; x++)
                    density[x, y] = Fbm(x / (float)w * cellsX, y / (float)h * cellsY, seed, 4, cellsX) * env - (1 - coverage);
            }
            var c = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = density[x, y];
                    if (d <= 0) continue;
                    if (d < 0.035f && Th(x, y) > 0.5f) continue;        // 가장자리를 디더링으로 성기게
                    bool topEdge = y < 2 || density[x, y - 2] <= 0;
                    bool bottomEdge = y >= h - 2 || density[x, y + 2] <= 0;
                    var col = topEdge ? rim : bottomEdge ? under : body;
                    if (!topEdge && d > 0.12f && Th(x, y) < 0.2f) col = Color.Lerp(col, under, 0.5f);
                    c.Set(x, y, WithAlpha(col, d < 0.05f ? 0.75f : 0.95f));
                }
            return c.ToSprite(TopCenter, repeat: true);
        }

        /// <summary>
        /// 산등성이: 1차원 fBm 으로 봉우리 높이를 만들고, 왼쪽 위에서 빛이 온다고 보고 오른쪽으로 올라가는 비탈(왼쪽을 향한 면)의
        /// 능선 가까이를 밝게 칠한다. 아래로 갈수록 골짜기 안개 색으로 디더링하며 흐려진다(대기 원근). rocky 면 바위 결을 더한다.
        /// </summary>
        Sprite RidgeSprite(int h, int seed, float baseH, float amp, float freq, Color body, bool rocky)
        {
            var top = new int[W];
            for (int x = 0; x < W; x++)
            {
                float n = Fbm(x * freq, 0.5f, seed, 5);
                float peak = Mathf.Pow(n, 1.6f) * 1.5f;
                top[x] = Mathf.Clamp(h - Mathf.RoundToInt(baseH + amp * peak), 0, h - 1);
            }
            var c = new PixelCanvas(W, h);
            Color lit = Color.Lerp(body, Theme.RidgeLight, 0.75f), shade = Color.Lerp(body, Color.black, 0.18f);
            for (int x = 0; x < W; x++)
            {
                int tl = top[Mathf.Max(0, x - 1)], tr = top[Mathf.Min(W - 1, x + 1)];
                int rise = tl - tr;                                      // > 0: 오른쪽으로 올라감 = 왼쪽을 향한 면
                for (int y = top[x]; y < h; y++)
                {
                    int depth = y - top[x];
                    var col = body;
                    if (rise > 0 && depth < 2 + Mathf.Min(rise, 3)) col = depth == 0 ? lit : Mix(body, lit, 0.6f, x, y, 2);
                    else if (rise < 0 && depth < 2) col = shade;
                    if (rocky && depth > 2 && Hash(x / 2, y / 3, seed) > 0.84f) col = Color.Lerp(col, shade, 0.6f);
                    float fade = Mathf.Clamp01((y - h * 0.4f) / (h * 0.6f));
                    col = Mix(col, Theme.Mist, fade * 0.85f, x, y, 4);
                    c.Set(x, y, col);
                }
            }
            return c.ToSprite(TopCenter);
        }

        /// <summary>
        /// 침엽수 숲 한 줄: 나무마다 층층이 넓어지는 가지(3~4층)를 그리고, 왼쪽 가장자리에 밝은 테두리, 오른쪽 절반은 조금 어둡게 칠한다.
        /// haze 만큼 골짜기 안개 색과 섞어 뒤쪽 줄을 흐리게 한다.
        /// </summary>
        Sprite ForestSprite(int h, int seed, int minH, int maxH, int minGap, int maxGap, Color body, Color light, float haze)
        {
            var c = new PixelCanvas(W, h);
            var rnd = new System.Random(seed);
            Color dark = Color.Lerp(body, Color.black, 0.22f);
            Color rimC = Color.Lerp(body, light, 0.8f);
            if (haze > 0) { body = Color.Lerp(body, Theme.Mist, haze); dark = Color.Lerp(dark, Theme.Mist, haze); rimC = Color.Lerp(rimC, Theme.Mist, haze); }
            for (int y = h - 5; y < h; y++)                              // 숲 바닥
                for (int x = 0; x < W; x++) c.Set(x, y, Th(x, y) < 0.15f ? dark : body);
            for (int cx = rnd.Next(0, 6); cx < W + 6; cx += rnd.Next(minGap, maxGap + 1))
            {
                int th = rnd.Next(minH, maxH + 1);
                float hw = th * (0.28f + (float)rnd.NextDouble() * 0.1f);
                int tiers = th > 22 ? 4 : 3;
                int baseY = h - 2 - rnd.Next(0, 3);
                for (int dy = 0; dy <= th; dy++)
                {
                    float tf = dy / (float)th * tiers;
                    int k = Mathf.Min((int)tf, tiers - 1);
                    float f = tf - k;
                    float halfW = hw * (k + 0.35f + f * 0.85f) / (tiers + 0.2f) * (0.45f + 0.55f * f);
                    int y = baseY - th + dy;
                    int x0 = Mathf.RoundToInt(cx - halfW), x1 = Mathf.RoundToInt(cx + halfW);
                    for (int x = x0; x <= x1; x++)
                    {
                        var col = x <= x0 && f > 0.25f ? rimC : x > cx ? dark : body;
                        if (dy == 0) col = rimC;
                        c.Set(x, y, col);
                    }
                }
            }
            return c.ToSprite(TopCenter);
        }

        /// <summary>
        /// 풀밭: 먼 곳 → 가까운 곳 6색 그라디언트를 디더링으로 잇고, 원근으로 늘어나는 큰 얼룩(밝고 어두운 풀 무리), 지평선 쪽 젖은 반짝임,
        /// 무리 지어 난 풀포기(가까울수록 크게)를 그린다. 웅덩이는 회색 막대처럼 보여 넣지 않았다(빗방울 물결만).
        /// </summary>
        Sprite GroundSprite()
        {
            int h = BaseH - Horizon;
            var c = new PixelCanvas(W, h);
            var pal = Theme.Ground;
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                float z = 1f / (y + 6f);                                 // 원근: 지평선 쪽일수록 멀다
                for (int x = 0; x < W; x++)
                {
                    float wx = (x - W / 2f) * z * 20f, wz = z * 400f;
                    float patch = Fbm(wx * 0.5f + 40, wz * 0.5f, 17, 3) - 0.5f;
                    var col = Ramp(pal, t * (pal.Length - 1) + patch * 1.6f, x, y);
                    float sheen = Mathf.Pow(1 - Mathf.Clamp01(t / 0.45f), 2) * 0.5f;
                    if (sheen > 0)
                    {
                        float n = Noise(x * 0.11f, y * 0.7f, 91);
                        if (n > 0.55f) col = Mix(col, Theme.Wet, sheen * (n - 0.55f) / 0.45f * 2.2f, x, y, 3);
                    }
                    if (y < 4) col = Mix(col, Theme.Mist, (4 - y) / 4f * 0.6f, x, y, 3);
                    c.Set(x, y, col);
                }
            }

            // 풀포기: 지평선 가까이는 그리지 않고(지글거림), 가까울수록 3갈래 잎이 길어진다
            var rnd = new System.Random(77);
            for (float yy = 12; yy < h; yy += 2.2f + yy * 0.07f)
            {
                int y = (int)yy;
                float t = y / (float)h;
                float spacing = 7f + y * 0.22f;
                for (float xx = (float)rnd.NextDouble() * spacing; xx < W; xx += spacing * (0.6f + (float)rnd.NextDouble() * 0.9f))
                {
                    int x = (int)xx;
                    if (Fbm(x * 0.05f, y * 0.12f, 61, 2) < 0.42f) continue;      // 풀이 무리 지어 난다 (빈 곳이 있어야 덜 지글거린다)
                    int len = Mathf.Max(2, Mathf.RoundToInt((1.2f + t * 4.5f) * (0.7f + (float)rnd.NextDouble() * 0.6f)));
                    Blade(c, x, y, len, 0);
                    Blade(c, x - 1, y, len - 1, -1);
                    if (len > 3) Blade(c, x + 1, y, len - 1, 1);
                }
            }
            return c.ToSprite(TopCenter);
        }

        void Blade(PixelCanvas c, int x0, int y0, int len, int lean)
        {
            for (int i = 0; i < len; i++)
            {
                int x = x0 + lean * i / 3, y = y0 - i;
                var tip = Color.Lerp(c.Get(x, y), Theme.GrassTip, 0.55f);
                var col = i == len - 1 ? tip : i < len / 2 ? Theme.GrassShade : Color.Lerp(Theme.GrassShade, Theme.GrassTip, 0.35f);
                c.Set(x, y, col);
            }
        }


        Sprite FogSprite()
        {
            const int w = 120, h = 32, cellsX = 12, cellsY = 4;
            var c = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = Fbm(x / (float)w * cellsX, y / (float)h * cellsY, 3, 2, cellsX);
                    float edge = Mathf.Sin(Mathf.PI * (y + 0.5f) / h);
                    float a = Mathf.Clamp01(v * 1.6f - 0.4f) * edge;
                    a = Mathf.Round(a * 4) / 4f;                         // 4단계 (도트 느낌)
                    if (a > 0) c.Set(x, y, new Color(1, 1, 1, a));
                }
            return c.ToSprite(TopCenter, repeat: true);
        }

        static Sprite RingSprite(int n)
        {
            var c = new PixelCanvas(n, n);
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 1f && d >= 0.72f) c.Set(x, y, Color.white);
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        Sprite VignetteSprite()
        {
            var c = new PixelCanvas(BaseW, BaseH);
            for (int y = 0; y < BaseH; y++)
                for (int x = 0; x < BaseW; x++)
                {
                    float dx = (x + 0.5f - BaseW / 2f) / (BaseW / 2f), dy = (y + 0.5f - BaseH / 2f) / (BaseH / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float s = Smooth(Mathf.Clamp01((r - 0.55f) / 0.6f));
                    c.Set(x, y, new Color(0.02f, 0.04f, 0.07f, s * Theme.VignetteAlpha));
                }
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        void CreateRain()
        {
            var go = new GameObject("Rain", typeof(ParticleSystem));
            go.transform.SetParent(world.Root, false);
            go.transform.localPosition = new Vector3(0, BaseH / 2f + 30, 0);
            rain = go.GetComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 1.05f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = new Color(0.78f, 0.87f, 1f, 0.55f);
            main.maxParticles = 400;
            var emission = rain.emission;
            emission.rateOverTime = RainRate;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(W + 60, 1, 1);
            var vel = rain.velocityOverLifetime;
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
            rain.Play();
        }
    }

    /// <summary>배경 시간(구름·안개가 흐르는 것, 물결, 번개, 날씨 변화)을 한 프레임씩 진행시킨다.</summary>
    public sealed class BackdropTicker : MonoBehaviour
    {
        public Action<float> OnTick;
        void Update() => OnTick?.Invoke(Time.deltaTime);
    }
}
