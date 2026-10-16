using System.Collections;
using System.Collections.Generic;
using Pokepedia.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle.Fx
{
    /// <summary>
    /// 배틀 화면 위 이펙트 계층. 좌표는 Ui 와 같은 "화면 픽셀, 좌상단 원점, y 아래로" 를 쓴다.
    /// - 파티클: Unity Particle System. 캔버스(Screen Space - Camera) 안 자식이고 scalingMode = Hierarchy 라서 크기·속도 단위도 화면 픽셀이다.
    ///   텍스처 없이 정사각형 입자로 그려 도트 느낌을 낸다. 정렬 순서를 캔버스보다 높여 UI 위에 그린다.
    /// - 도형: 번개 선분·구체·고리는 uGUI Image 로 그린다 (픽셀 정렬이 쉽다).
    /// Java 비교: 코루틴(IEnumerator + yield)은 프레임 단위로 나눠 실행되는 협력적 작업이다 (스레드가 아님).
    /// </summary>
    public sealed class VfxLayer
    {
        public const int ParticleSortingOrder = 20;

        readonly RectTransform screen;
        readonly RectTransform overlay;
        readonly Material particleMaterial;
        readonly Image flash;
        readonly List<ParticleSystem> pool = new List<ParticleSystem>();
        readonly Sprite square, circle, ring;
        int poolIndex;

        public VfxLayer(RectTransform screen, Material particleMaterial, int canvasSortingOrder)
        {
            this.screen = screen;
            this.particleMaterial = particleMaterial;
            overlay = Ui.Rect(screen, "Fx", 0, 0, PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight);
            flash = Ui.Fill(screen, "ScreenFlash", 0, 0, PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight, new Color(1, 1, 1, 0));
            square = MakeSprite(4, (x, y) => true);
            circle = MakeSprite(16, (x, y) => Dist(x, y, 16) <= 1f);
            ring = MakeSprite(32, (x, y) => { var d = Dist(x, y, 32); return d <= 1f && d >= 0.8f; });
            for (int i = 0; i < 4; i++) pool.Add(CreateEmitter("Particles" + i, canvasSortingOrder));
        }

        /// <summary>오버레이 레이어를 맨 위로 (스프라이트·정보 상자보다 위)</summary>
        public void BringToFront()
        {
            overlay.SetAsLastSibling();
            flash.transform.SetAsLastSibling();
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

        ParticleSystem CreateEmitter(string name, int canvasSortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ParticleSystem));
            go.transform.SetParent(screen, false);
            go.layer = screen.gameObject.layer;
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; // 캔버스 배율을 따라간다 → 단위 = 화면 픽셀
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
            renderer.sortingOrder = canvasSortingOrder + ParticleSortingOrder;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        /// <summary>화면 픽셀 좌표(좌상단 원점) → 화면 RectTransform 의 로컬 좌표(가운데 원점, y 위로)</summary>
        public static Vector3 ToLocal(Vector2 px) => new Vector3(px.x - PixelCanvasScaler.BaseWidth / 2f, PixelCanvasScaler.BaseHeight / 2f - px.y, 0);

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
            var vel = new Vector3(dir.x, -dir.y, 0) * speed; // 화면 좌표는 y 아래, 로컬은 y 위
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

        /// <summary>지그재그 번개 (위 → 아래). 선분 Image 를 만들고 깜빡인 뒤 지운다.</summary>
        public IEnumerator Bolt(Vector2 top, Vector2 bottom, Color core, Color glow, int flickers, float seconds)
        {
            var root = new GameObject("Bolt", typeof(RectTransform));
            root.transform.SetParent(overlay, false);
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
                Segment(root.transform, points[i], points[i + 1], 5, glow);
                Segment(root.transform, points[i], points[i + 1], 2, core);
            }
            float each = seconds / Mathf.Max(1, flickers * 2);
            for (int f = 0; f < flickers; f++)
            {
                root.SetActive(true); yield return Wait(each);
                root.SetActive(false); yield return Wait(each * 0.6f);
            }
            Object.Destroy(root);
        }

        static void Segment(Transform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var len = Vector2.Distance(a, b);
            var img = Ui.Fill(parent, "Seg", a.x, a.y, len, thickness, color);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(a.x, -a.y);
            var angle = Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg;
            rt.localEulerAngles = new Vector3(0, 0, angle);
        }

        /// <summary>구체가 from → to 로 날아간다 (발사형 기술). 도착하면 사라진다.</summary>
        public IEnumerator Orb(Vector2 from, Vector2 to, Color color, int sizePx, float seconds, Color trail)
        {
            var img = Ui.Fill(overlay, "Orb", 0, 0, sizePx, sizePx, color);
            img.sprite = circle;
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                var p = Vector2.Lerp(from, to, t / seconds);
                p.y -= Mathf.Sin(t / seconds * Mathf.PI) * 14; // 살짝 포물선
                img.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.x), -Mathf.Round(p.y));
                Burst(p, trail, color, 1, 0, 6, 0.25f, 2);
                yield return null;
            }
            Object.Destroy(img.gameObject);
        }

        /// <summary>대상 위치에서 퍼지는 고리 (효과형 기술)</summary>
        public IEnumerator Rings(Vector2 at, Color color, int count, float seconds)
        {
            for (int i = 0; i < count; i++)
            {
                var img = Ui.Fill(overlay, "Ring", 0, 0, 8, 8, color);
                img.sprite = ring;
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchoredPosition = new Vector2(at.x, -at.y);
                float each = seconds / count;
                for (float t = 0; t < each; t += Time.deltaTime)
                {
                    float k = t / each;
                    float s = Mathf.Round(Mathf.Lerp(8, 64, k) / 2) * 2;
                    img.rectTransform.sizeDelta = new Vector2(s, s);
                    var c = color; c.a = 1 - k; img.color = c;
                    yield return null;
                }
                Object.Destroy(img.gameObject);
            }
        }

        /// <summary>화면 전체 번쩍임 (급소 등)</summary>
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

        /// <summary>화면 흔들림 (정수 픽셀)</summary>
        public IEnumerator Shake(int amplitude, float seconds)
        {
            var home = screen.anchoredPosition;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = 1 - t / seconds;
                screen.anchoredPosition = home + new Vector2(Mathf.Round(Random.Range(-1f, 1f) * amplitude * k), Mathf.Round(Random.Range(-1f, 1f) * amplitude * k));
                yield return null;
            }
            screen.anchoredPosition = home;
        }

        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
