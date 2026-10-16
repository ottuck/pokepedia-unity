using System.Collections;
using System.Collections.Generic;
using Pokepedia.Presentation.UI;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 배틀 화면의 "월드" 쪽 (PROJECT.md 5.5): 배경·발판·포켓몬 스프라이트·이펙트를 SpriteRenderer 와 카메라로 그린다.
    /// HP 바·메뉴·메시지는 별도 uGUI 캔버스(HUD, BattleScreen)가 맡고 항상 월드 위에 그려진다.
    ///
    /// 좌표계: 월드 1 단위 = 기준 해상도 1 픽셀(240×216), 원점 = 화면 가운데, y 위로.
    /// 이 클래스 밖의 연출 코드는 HUD 와 같은 "화면 픽셀, 좌상단 원점, y 아래로" 를 쓰고 ToLocal 로 바꾼다.
    /// 카메라는 PixelCamera 가 정수 배율로 맞추므로 1 월드 단위 = 정수 개의 화면 픽셀이다.
    ///
    /// 구조: Container → Frame(화면 밖 가림, 고정) + Pan(카메라 이동) → Root(화면 흔들림) → 배경·발판·포켓몬·이펙트.
    /// 조명을 받는 것(배경·발판·포켓몬)은 Sprite-Lit, 받지 않는 것(이펙트·그림자·안개·비·비네트)은 Sprite-Unlit 머티리얼을 쓴다.
    /// </summary>
    public sealed class BattleWorld
    {
        // 그리는 순서 (SpriteRenderer.sortingOrder). 숫자가 클수록 위. 배경 레이어는 BattleBackdrop 이 1~7 을 쓴다.
        public const int SortBackground = -10;
        public const int SortPlatform = 10;
        public const int SortShadow = 11;
        public const int SortSprite = 20;
        public const int SortFlash = 21;
        public const int SortFx = 30;
        public const int SortFrame = 100;

        // 기준 해상도 밖을 가리는 막대 색 = 카메라 배경색 (ProjectSetup.CreateMainScene)
        static readonly Color FrameColor = new Color(0.06f, 0.22f, 0.06f);

        static Sprite pixel, pixelLeft;
        static readonly Dictionary<long, Sprite> Ellipses = new Dictionary<long, Sprite>();
        static readonly Dictionary<long, Sprite> Shadows = new Dictionary<long, Sprite>();
        /// <summary>1×1 흰 점(가운데 기준). 크기는 localScale 로 준다.</summary>
        public static Sprite Pixel => pixel != null ? pixel : (pixel = MakePixel(new Vector2(0.5f, 0.5f)));
        /// <summary>1×1 흰 점(왼쪽 가운데 기준). 선분을 그릴 때 쓴다.</summary>
        public static Sprite PixelLeft => pixelLeft != null ? pixelLeft : (pixelLeft = MakePixel(new Vector2(0f, 0.5f)));

        readonly Material lit;
        readonly Transform pan;
        float panX;
        int panToken;

        /// <summary>조명을 받지 않는 스프라이트 머티리얼 (이펙트·그림자·안개 등)</summary>
        public Material Unlit { get; }
        /// <summary>월드 전체를 켜고 끄는 부모 (배틀 화면이 열릴 때만 켠다)</summary>
        public GameObject Container { get; }
        /// <summary>배경·발판·포켓몬·이펙트의 부모. 화면 흔들림이 이 루트를 움직인다(화면 밖 가림 막대는 제외).</summary>
        public Transform Root { get; }
        /// <summary>배경 레이어(패럴랙스). BattleScreen 이 만든 뒤 넣는다.</summary>
        public BattleBackdrop Backdrop { get; set; }

        public BattleWorld(Transform parent, Camera camera, Material unlit, Material lit)
        {
            Unlit = unlit;
            this.lit = lit;
            if (camera != null && camera.GetComponent<PixelCamera>() == null) camera.gameObject.AddComponent<PixelCamera>();

            Container = new GameObject("BattleWorld");
            Container.transform.SetParent(parent, false);
            pan = new GameObject("Pan").transform;
            pan.SetParent(Container.transform, false);
            Root = new GameObject("Root").transform;
            Root.SetParent(pan, false);

            // 가장 아래 바탕(레이어 사이로 비칠 일은 없지만 안전망)
            Quad(Root, "Background", Vector2.zero, PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight, Ui.Background, SortBackground);

            // 기준 해상도 밖(창이 더 클 때 보이는 영역)을 가린다. 흔들려도 움직이지 않게 Root 밖에 둔다.
            var frame = new GameObject("Frame").transform;
            frame.SetParent(Container.transform, false);
            const float big = 4096;
            float hw = PixelCanvasScaler.BaseWidth / 2f, hh = PixelCanvasScaler.BaseHeight / 2f;
            Quad(frame, "Left", new Vector2(-(hw + big / 2), 0), big, big * 2 + PixelCanvasScaler.BaseHeight, FrameColor, SortFrame);
            Quad(frame, "Right", new Vector2(hw + big / 2, 0), big, big * 2 + PixelCanvasScaler.BaseHeight, FrameColor, SortFrame);
            Quad(frame, "Top", new Vector2(0, hh + big / 2), PixelCanvasScaler.BaseWidth, big, FrameColor, SortFrame);
            Quad(frame, "Bottom", new Vector2(0, -(hh + big / 2)), PixelCanvasScaler.BaseWidth, big, FrameColor, SortFrame);
        }

        /// <summary>화면 픽셀 좌표(좌상단 원점, y 아래로) → 월드 로컬 좌표(가운데 원점, y 위로)</summary>
        public static Vector3 ToLocal(Vector2 px)
            => new Vector3(px.x - PixelCanvasScaler.BaseWidth / 2f, PixelCanvasScaler.BaseHeight / 2f - px.y, 0);

        // ------------------------------------------------------------------ 카메라 이동 (패럴랙스)
        /// <summary>카메라를 옆으로 옮긴 양(픽셀, 정수로 반올림). 가까운 것은 그만큼, 먼 배경은 덜 따라가 보인다.</summary>
        public void SetPan(float value)
        {
            panX = value;
            float x = Mathf.Round(value);
            pan.localPosition = new Vector3(-x, 0, 0);
            if (Backdrop != null) Backdrop.SetPan(x);
        }

        /// <summary>카메라를 부드럽게 target 까지 옮긴다. 새 이동이 시작되면 이전 이동은 멈춘다.</summary>
        public IEnumerator PanTo(float target, float seconds)
        {
            int token = ++panToken;
            float from = panX;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (token != panToken) yield break;
                float k = t / seconds;
                SetPan(Mathf.Lerp(from, target, k * k * (3 - 2 * k)));
                yield return null;
            }
            if (token == panToken) SetPan(target);
        }

        // ------------------------------------------------------------------ 도형 만들기
        /// <summary>단색 사각형. localScale 이 곧 픽셀 크기다.</summary>
        public SpriteRenderer Quad(Transform parent, string name, Vector2 localCenter, float w, float h, Color color, int order)
        {
            var sr = NewRenderer(parent, name, Pixel, order);
            sr.color = color;
            sr.transform.localPosition = localCenter;
            sr.transform.localScale = new Vector3(w, h, 1);
            return sr;
        }

        /// <summary>도트 느낌의 타원 발판(조명을 받는다). center 는 화면 픽셀 좌표.</summary>
        public SpriteRenderer Platform(string name, Vector2 center, int w, int h)
        {
            var sr = NewRenderer(Root, name, Ellipse(w, h), SortPlatform, true);
            sr.color = Ui.Platform;
            sr.transform.localPosition = ToLocal(center);
            return sr;
        }

        /// <summary>월드 스프라이트를 만든다. lit 이 true 면 조명을 받는 머티리얼을 쓴다.</summary>
        public SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite, int order, bool useLight = false)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            var material = useLight && lit != null ? lit : Unlit;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        /// <summary>w×h 타원(가운데 기준). 같은 크기는 하나를 같이 쓴다.</summary>
        public static Sprite Ellipse(int w, int h)
        {
            long key = ((long)w << 32) | (uint)h;
            if (Ellipses.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float rx = w / 2f, ry = h / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= 1f ? Color.white : Color.clear);
                }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            Ellipses[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 바닥 그림자: 가운데가 진하고 가장자리로 갈수록 옅어지는 타원(3단계, 경계는 Bayer 디더링). 흰색이라 SpriteRenderer.color 로 색·진하기를 준다.
        /// </summary>
        public static Sprite SoftShadow(int w, int h)
        {
            long key = ((long)w << 32) | (uint)h;
            if (Shadows.TryGetValue(key, out var cached) && cached != null) return cached;
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float rx = w / 2f, ry = h / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float th = (bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
                    float a = d > 1f ? 0f : d > 0.82f ? (th < 0.5f ? 0.35f : 0f) : d > 0.6f ? 0.6f : 1f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            Shadows[key] = sprite;
            return sprite;
        }

        static Sprite MakePixel(Vector2 pivot)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), pivot, 1f);
        }
    }
}
