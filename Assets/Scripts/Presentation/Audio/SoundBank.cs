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
        /// <summary>기술 사용음 = 속성 레시피를 조금 짧게 (울음소리와 같은 합성기)</summary>
        public const double MoveSoundLength = 0.8;

        public static string CryKey(int speciesId) => "cry_" + speciesId;
        public static string MoveKey(string typeName) => "move_" + typeName;
        public static string SfxKey(SfxKind kind) => "sfx_" + kind.ToString().ToLowerInvariant();

        readonly AudioSource source;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public SoundBank(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        public void Play(SfxKind kind) => PlayKey(SfxKey(kind));
        public void Cry(int speciesId) => PlayKey(CryKey(speciesId));
        public void Move(string typeName) => PlayKey(MoveKey(typeName));

        void PlayKey(string key)
        {
            if (!cache.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + key);
                if (clip == null) Debug.LogWarning($"[SoundBank] missing Resources/Audio/{key} — 메뉴 Pokepedia/Bake Audio (또는 Setup Project) 를 실행했는지 확인");
                cache[key] = clip;
            }
            if (clip != null) source.PlayOneShot(clip);
        }
    }
}
