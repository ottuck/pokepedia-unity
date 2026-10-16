using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 한쪽 진영의 화면 요소: 포켓몬 스프라이트(발 위치 기준) + 정보 상자(이름·HP).
    /// 좌표는 화면 픽셀(좌상단 원점, y 아래로).
    /// </summary>
    public sealed class BattleSideView
    {
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public RectTransform SpriteRect { get; }
        public UiSpriteAnimator Animator { get; }
        public Image Image { get; }
        /// <summary>발 위치(화면 픽셀)</summary>
        public Vector2 Feet { get; }
        public GameObject InfoBox { get; }
        public TextMeshProUGUI Name { get; }
        public HpBar Hp { get; }

        readonly Material flash;

        public BattleSideView(RectTransform spriteRect, Image image, UiSpriteAnimator animator, Vector2 feet,
                              GameObject infoBox, TextMeshProUGUI name, HpBar hp, Material flashMaterial)
        {
            SpriteRect = spriteRect; Image = image; Animator = animator; Feet = feet;
            InfoBox = infoBox; Name = name; Hp = hp;
            if (flashMaterial != null)
            {
                flash = new Material(flashMaterial); // 양쪽이 따로 번쩍이도록 인스턴스를 만든다
                image.material = flash;
            }
        }

        public Vector2 Size => Animator.Sheet != null ? (Vector2)Animator.Sheet.Size : new Vector2(48, 48);

        /// <summary>몸 가운데(화면 픽셀)</summary>
        public Vector2 Center => Feet - new Vector2(0, Mathf.Round(Size.y / 2));

        /// <summary>발 위치에서 offset(화면 픽셀, y 아래로)만큼 옮긴다. 정수 픽셀로 맞춘다.</summary>
        public void SetOffset(Vector2 offset)
            => SpriteRect.anchoredPosition = new Vector2(Mathf.Round(Feet.x + offset.x), -Mathf.Round(Feet.y + offset.y));

        public void SetFlash(float amount)
        {
            if (flash != null) flash.SetFloat(FlashId, amount);
        }

        public void SetAlpha(float a)
        {
            var c = Image.color; c.a = a; Image.color = c;
        }

        public void Reset()
        {
            SetOffset(Vector2.zero);
            SetFlash(0);
            SetAlpha(1);
            Image.enabled = false;
            InfoBox.SetActive(false);
        }
    }
}
