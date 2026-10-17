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
        /// <summary>기술 전용 공격음(animation.json moveSfx)의 클립 이름</summary>
        public static string MoveSfxKey(string soundId) => "movesfx_" + soundId;
        public static string SfxKey(SfxKind kind) => "sfx_" + kind.ToString().ToLowerInvariant();

        public const string BattleMusicKey = "bgm_battle";

        /// <summary>
        /// 음악 WAV 앞에 붙이는 무음(샘플, 22050Hz 기준). WebGL 은 소리를 AAC 로 바꾸는데 인코더가 앞 1024 샘플을 건드려
        /// 루프 이음새가 튄다 → 무음을 붙이고 WAV smpl 청크의 루프 시작을 그 뒤로 둔다(Unity 6 매뉴얼 "Audio in Web", AudioBaker).
        /// </summary>
        public const int MusicPadSamples = 2048;
        const float MusicPadSeconds = MusicPadSamples / 22050f;
        /// <summary>음악 음량. 효과음이 위에 얹히도록 낮게 둔다(사용자 확인 후 조정).</summary>
        const float MusicVolume = 0.7f;

        readonly AudioSource source;
        // 기 모으기처럼 도중에 끊어야 하는 소리는 따로 낸다(PlayOneShot 은 멈출 수 없다)
        readonly AudioSource stoppable;
        readonly AudioSource music;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public SoundBank(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            stoppable = host.AddComponent<AudioSource>();
            stoppable.playOnAwake = false;
            music = host.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
        }

        /// <summary>음악을 처음부터 반복 재생한다(앞의 무음은 건너뛴다).</summary>
        public void PlayMusic(string key)
        {
            var clip = Load(key);
            if (clip == null) return;
            music.Stop();
            music.clip = clip;
            music.volume = MusicVolume;
            music.time = Mathf.Min(MusicPadSeconds, clip.length * 0.5f);
            music.Play();
        }

        public void StopMusic() => music.Stop();

        /// <summary>음악을 seconds 동안 줄였다가 멈춘다(승리·패배 징글 전에).</summary>
        public System.Collections.IEnumerator FadeOutMusic(float seconds)
        {
            if (!music.isPlaying) yield break;
            float from = music.volume;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                music.volume = from * (1 - t / seconds);
                yield return null;
            }
            music.Stop();
            music.volume = MusicVolume;
        }

        public void Play(SfxKind kind) => PlayKey(SfxKey(kind));
        public void Move(string typeName) => PlayKey(MoveKey(typeName));
        public void MoveSfx(string soundId) => PlayKey(MoveSfxKey(soundId));

        /// <summary>기술 전용 공격음을 발사 순간보다 몇 초 먼저 틀어야 하는지 (<see cref="TypeAttackSounds.MoveSoundLead"/>).</summary>
        public static float MoveSfxLead(string soundId) => (float)TypeAttackSounds.MoveSoundLead(soundId, MoveSoundLength);

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
