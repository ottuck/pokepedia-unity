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
        int held = -1;

        public SpriteSheet Sheet => sheet;

        public void Init(SpriteRenderer body, SpriteRenderer flash)
        {
            this.body = body; this.flash = flash;
        }

        public void Play(SpriteSheet newSheet)
        {
            sheet = newSheet;
            index = 0; timer = 0; held = -1;
            if (sheet != null) SetFrame(0);
        }

        /// <summary>재생을 멈추고 frame 에 고정한다. 입에서 쏘는 기술처럼 스프라이트 위의 한 점(animation.json 의 mouth)이 움직이면 안 될 때 쓴다.</summary>
        public void Hold(int frame)
        {
            if (sheet == null) return;
            held = Mathf.Clamp(frame, 0, sheet.Frames.Length - 1);
            SetFrame(held);
        }

        /// <summary>Hold 를 풀고 고정했던 프레임부터 다시 재생한다.</summary>
        public void Release()
        {
            if (held < 0) return;
            index = held; timer = 0; held = -1;
        }

        void Update()
        {
            if (sheet == null || sheet.Frames.Length < 2 || held >= 0) return;
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
