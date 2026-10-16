using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 한쪽 진영의 화면 요소 (PROJECT.md 5.5): 월드의 포켓몬 스프라이트(발 위치 기준) + HUD 의 정보 상자(이름·HP).
    /// 좌표는 화면 픽셀(좌상단 원점, y 아래로) — 월드 위치로 바꾸는 건 BattleWorld.ToLocal 이다.
    /// 피격 번쩍임은 본체 위에 겹친 두 번째 렌더러(Pokepedia/Sprite Flash)의 알파로 낸다.
    /// </summary>
    public sealed class BattleSideView
    {
        readonly SpriteRenderer body, flashRenderer;
        readonly Transform bodyTransform;
        bool visible;
        float flash;
        float alpha = 1;

        public SpriteAnimator Animator { get; }
        /// <summary>발 위치(화면 픽셀)</summary>
        public Vector2 Feet { get; }
        public GameObject InfoBox { get; }
        public TextMeshProUGUI Name { get; }
        public HpBar Hp { get; }

        public BattleSideView(BattleWorld world, string name, Vector2 feet, Material unlit, Material flashMaterial,
                              GameObject infoBox, TextMeshProUGUI nameLabel, HpBar hp)
        {
            Feet = feet; InfoBox = infoBox; Name = nameLabel; Hp = hp;

            body = world.NewRenderer(world.Root, name + "Sprite", null, BattleWorld.SortSprite);
            bodyTransform = body.transform;
            body.enabled = false;
            flashRenderer = world.NewRenderer(bodyTransform, "Flash", null, BattleWorld.SortFlash);
            if (flashMaterial != null) flashRenderer.sharedMaterial = flashMaterial;
            flashRenderer.enabled = false;
            Animator = body.gameObject.AddComponent<SpriteAnimator>();
            Animator.Init(body, flashRenderer);
        }

        public Vector2 Size => Animator.Sheet != null ? (Vector2)Animator.Sheet.Size : new Vector2(48, 48);

        /// <summary>몸 가운데(화면 픽셀)</summary>
        public Vector2 Center => Feet - new Vector2(0, Mathf.Round(Size.y / 2));

        /// <summary>시트를 재생하고 보이게 한다 (null 이면 숨긴다)</summary>
        public void Play(SpriteSheet sheet)
        {
            Animator.Play(sheet);
            visible = sheet != null;
            Refresh();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            Refresh();
        }

        /// <summary>발 위치에서 offset(화면 픽셀, y 아래로)만큼 옮긴다. 정수 픽셀로 맞춘다.</summary>
        public void SetOffset(Vector2 offset)
            => bodyTransform.localPosition = BattleWorld.ToLocal(new Vector2(Mathf.Round(Feet.x + offset.x), Mathf.Round(Feet.y + offset.y)));

        public void SetFlash(float amount)
        {
            flash = amount;
            Refresh();
        }

        public void SetAlpha(float a)
        {
            alpha = a;
            Refresh();
        }

        public void Reset()
        {
            flash = 0; alpha = 1; visible = false;
            SetOffset(Vector2.zero);
            Refresh();
            InfoBox.SetActive(false);
        }

        void Refresh()
        {
            bool on = visible && Animator.Sheet != null;
            body.enabled = on;
            body.color = new Color(1, 1, 1, alpha);
            flashRenderer.enabled = on && flash > 0.001f;
            flashRenderer.color = new Color(1, 1, 1, Mathf.Clamp01(flash) * alpha);
        }
    }
}
