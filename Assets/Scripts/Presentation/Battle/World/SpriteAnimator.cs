using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// SpriteRenderer 위에서 SpriteSheet 를 프레임 지연대로 반복 재생한다 (M0 결론: Animator 대신 코드 프레임 플레이어).
    /// 스프라이트 pivot 이 바닥 중앙(0.5, 0)이라 포켓몬 발 위치가 고정된다. 피격 번쩍임용 렌더러에도 같은 프레임을 넣는다.
    /// </summary>
    public sealed class SpriteAnimator : MonoBehaviour
    {
        SpriteRenderer body, flash;
        SpriteSheet sheet;
        int index;
        float timer;

        public SpriteSheet Sheet => sheet;

        public void Init(SpriteRenderer body, SpriteRenderer flash)
        {
            this.body = body; this.flash = flash;
        }

        public void Play(SpriteSheet newSheet)
        {
            sheet = newSheet;
            index = 0; timer = 0;
            if (sheet != null) SetFrame(0);
        }

        void Update()
        {
            if (sheet == null || sheet.Frames.Length < 2) return;
            timer += Time.deltaTime;
            while (timer >= sheet.Delays[index])
            {
                timer -= sheet.Delays[index];
                index = (index + 1) % sheet.Frames.Length;
            }
            SetFrame(index);
        }

        void SetFrame(int i)
        {
            body.sprite = sheet.Frames[i];
            flash.sprite = sheet.Frames[i];
        }
    }
}
