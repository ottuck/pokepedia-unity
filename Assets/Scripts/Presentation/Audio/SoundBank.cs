using System.Collections.Generic;
using Pokepedia.Presentation.Audio.Synth;
using UnityEngine;

namespace Pokepedia.Presentation.Audio
{
    /// <summary>
    /// 합성기(Synth)가 만든 샘플을 AudioClip 으로 바꿔 캐시하고 재생한다. 음원 파일이 없다 (PROJECT.md 8.3).
    /// WebGL 은 AudioClip.Create 에 stream=false 만 지원하므로 전체 샘플을 한 번에 만든다.
    /// </summary>
    public sealed class SoundBank
    {
        const int SampleRate = 22050;

        readonly AudioSource source;
        readonly Dictionary<SfxKind, AudioClip> sfx = new Dictionary<SfxKind, AudioClip>();
        readonly Dictionary<string, AudioClip> cries = new Dictionary<string, AudioClip>();

        public SoundBank(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        public void Play(SfxKind kind)
        {
            if (!sfx.TryGetValue(kind, out var clip))
            {
                clip = ToClip("sfx_" + kind, BattleSfx.Render(kind, SampleRate));
                sfx[kind] = clip;
            }
            source.PlayOneShot(clip);
        }

        /// <summary>속성 기반 울음소리. types 는 소문자 타입명(species.json 표기).</summary>
        public void Cry(IReadOnlyList<string> types, double semitones, double lengthMul)
        {
            var key = string.Join("/", types) + "|" + semitones + "|" + lengthMul;
            if (!cries.TryGetValue(key, out var clip))
            {
                clip = ToClip("cry_" + key, TypeCries.Render(types, semitones, lengthMul, SampleRate));
                cries[key] = clip;
            }
            source.PlayOneShot(clip);
        }

        static AudioClip ToClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, Mathf.Max(1, samples.Length), 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
