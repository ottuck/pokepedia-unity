using System.Collections.Generic;
using Pokepedia.Presentation.Audio.Synth;
using UnityEngine;

namespace Pokepedia.Presentation.Audio
{
    /// <summary>
    /// 소리 재생. 소리는 합성기(Synth)로 만들되, 실행 중이 아니라 에디터에서 미리 구운 클립
    /// (Assets/Resources/Audio, AudioBaker)을 이름으로 불러 쓴다. 음원 파일을 외부에서 가져오지 않는다 (PROJECT.md 8.3).
    /// WebGL 에서 실행 중 합성은 수 초씩 메인 스레드를 막아(M3-2 측정) 쓰지 않는다.
    /// </summary>
    public sealed class SoundBank
    {
        /// <summary>기술 사용음(속성 공격음) 길이 배수</summary>
        public const double MoveSoundLength = 0.8;

        public static string MoveKey(string typeName) => "move_" + typeName;
        public static string SfxKey(SfxKind kind) => "sfx_" + kind.ToString().ToLowerInvariant();

        readonly AudioSource source;
        // 기 모으기처럼 도중에 끊어야 하는 소리는 따로 낸다(PlayOneShot 은 멈출 수 없다)
        readonly AudioSource stoppable;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public SoundBank(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            stoppable = host.AddComponent<AudioSource>();
            stoppable.playOnAwake = false;
        }

        public void Play(SfxKind kind) => PlayKey(SfxKey(kind));
        public void Move(string typeName) => PlayKey(MoveKey(typeName));

        /// <summary>속성 공격음이 있는 속성인지. 없으면(노말 등) 맞을 때 타격음만 난다.</summary>
        public static bool HasMoveSound(string typeName) => TypeAttackSounds.IsSupported(typeName);

        /// <summary>공격음을 발사 순간보다 몇 초 먼저 틀어야 하는지 (<see cref="TypeAttackSounds.LeadSeconds"/>).</summary>
        public static float MoveLead(string typeName)
            => TypeAttackSounds.TryParse(typeName, out var e) ? (float)TypeAttackSounds.LeadSeconds(e, MoveSoundLength) : 0f;

        /// <summary>끊을 수 있는 채널에서 재생한다(이전 소리는 멈춘다).</summary>
        public void PlayStoppable(SfxKind kind)
        {
            var clip = Load(SfxKey(kind));
            if (clip == null) return;
            stoppable.Stop();
            stoppable.clip = clip;
            stoppable.Play();
        }

        public void StopStoppable() => stoppable.Stop();

        void PlayKey(string key)
        {
            var clip = Load(key);
            if (clip != null) source.PlayOneShot(clip);
        }

        AudioClip Load(string key)
        {
            if (!cache.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + key);
                if (clip == null) Debug.LogWarning($"[SoundBank] missing Resources/Audio/{key} — 메뉴 Pokepedia/Bake Audio (또는 Setup Project) 를 실행했는지 확인");
                cache[key] = clip;
            }
            return clip;
        }
    }
}
