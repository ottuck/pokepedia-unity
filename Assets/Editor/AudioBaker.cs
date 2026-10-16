using System;
using System.IO;
using System.Linq;
using Pokepedia.Content;
using Pokepedia.Presentation.Audio;
using Pokepedia.Presentation.Audio.Synth;
using UnityEditor;
using UnityEngine;

namespace Pokepedia.EditorTools
{
    /// <summary>
    /// 합성기(Synth)로 소리를 미리 만들어 Assets/Resources/Audio/*.wav 로 굽는다 (커밋하지 않음, Setup 이 재생성).
    /// 실행 중 합성은 WebGL 에서 수 초씩 메인 스레드를 막아(M3-2 확인) 에디터에서 한 번만 계산한다.
    /// 레시피·결과는 런타임 합성과 같다 (결정적 합성기).
    /// </summary>
    public static class AudioBaker
    {
        const string OutDir = "Assets/Resources/Audio";
        const int SampleRate = 22050;

        [MenuItem("Pokepedia/Bake Audio")]
        public static void Bake()
        {
            Directory.CreateDirectory(OutDir);
            var catalog = ContentLoader.Load(Read("species.json"), Read("moves.json"), Read("roster.json"));
            var anim = AnimationCatalog.Parse(Read("animation.json"));
            int count = 0;

            foreach (var s in catalog.Species)
            {
                var types = s.Species.Types.Select(ContentLoader.ToJsonName).Where(TypeCries.IsSupported).ToArray();
                if (types.Length == 0) continue;
                var cry = anim.ProfileFor(s.AnimProfile).Cry;
                Write(SoundBank.CryKey(s.Species.Id), TypeCries.Render(types, cry.Semitones, cry.Length, SampleRate));
                count++;
            }
            var supported = ((Pokepedia.Domain.PokemonType[])Enum.GetValues(typeof(Pokepedia.Domain.PokemonType)))
                .Select(ContentLoader.ToJsonName).Where(TypeCries.IsSupported);
            foreach (var type in supported)
            {
                Write(SoundBank.MoveKey(type), TypeCries.Render(new[] { type }, 0, SoundBank.MoveSoundLength, SampleRate));
                count++;
            }
            foreach (SfxKind kind in Enum.GetValues(typeof(SfxKind)))
            {
                Write(SoundBank.SfxKey(kind), BattleSfx.Render(kind, SampleRate));
                count++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[AudioBaker] {count} clips -> {OutDir}");
        }

        static string Read(string name) => File.ReadAllText(Path.Combine("Assets/Content/Data", name));

        /// <summary>16bit PCM mono WAV</summary>
        static void Write(string key, float[] samples)
        {
            var path = Path.Combine(OutDir, key + ".wav");
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(SampleRate); w.Write(SampleRate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataBytes);
                foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }
        }
    }

    /// <summary>구운 소리의 임포트 설정: 모노, 메모리에 압축 보관(iOS 무음 모드에서도 재생 — M0 조사, WebKit #262781)</summary>
    public sealed class BakedAudioImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var a = (AudioImporter)assetImporter;
            a.forceToMono = true;
            var s = a.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            a.defaultSampleSettings = s;
        }
    }
}
