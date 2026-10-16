using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Pokepedia.EditorTools
{
    /// <summary>
    /// Galmuri11 TTF → TMP Static 폰트 에셋. 필요한 글리프만 굽는다(M0 결론).
    /// 글리프 출처: Assets/Content/Locale/*.json 의 모든 문자 + ASCII + 고정 기호.
    /// 문구를 바꾸면 메뉴 Pokepedia/Build Font Atlas 를 다시 실행한다. (누락 글자는 □ 로 보인다)
    /// </summary>
    public static class FontAtlasBuilder
    {
        const string TtfPath = "Assets/Content/Fonts/Galmuri11.ttf";
        const string LocaleDir = "Assets/Content/Locale";
        public const string OutPath = "Assets/Content/Fonts/Galmuri11-Static.asset";
        const string ExtraGlyphs = "♀♂×…·→▶▼○★♪、。「」！？～ー・'’\"";

        [MenuItem("Pokepedia/Build Font Atlas")]
        public static void Build()
        {
            var glyphs = CollectGlyphs();
            var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            // Galmuri11 은 12px 격자 도트 폰트 → 12pt 샘플링 + RASTER_HINTED(안티앨리어싱 없음)
            var fa = TMP_FontAsset.CreateFontAsset(font, 12, 2, GlyphRenderMode.RASTER_HINTED, 512, 512, AtlasPopulationMode.Dynamic, false);
            fa.name = "Galmuri11-Static";
            fa.TryAddCharacters(glyphs, out string missing);
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            fa.atlasTexture.filterMode = FilterMode.Point;

            AssetDatabase.DeleteAsset(OutPath);
            AssetDatabase.CreateAsset(fa, OutPath);
            fa.atlasTexture.name = fa.name + " Atlas";
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            // 빌드에 원본 TTF(약 5MB)가 끌려 들어가지 않도록 소스 폰트 참조를 끊는다
            var so = new SerializedObject(fa);
            so.FindProperty("m_SourceFontFile").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();

            var msg = $"[FontAtlas] glyphs={glyphs.Length} characters={fa.characterTable.Count} atlas={fa.atlasTexture.width}x{fa.atlasTexture.height}";
            if (!string.IsNullOrEmpty(missing)) Debug.LogError(msg + " MISSING='" + missing + "'");
            else Debug.Log(msg);
        }

        public static string CollectGlyphs()
        {
            var set = new SortedSet<char>();
            for (char c = ' '; c <= '~'; c++) set.Add(c);
            foreach (var c in ExtraGlyphs) set.Add(c);
            foreach (var path in Directory.GetFiles(LocaleDir, "*.json"))
                foreach (var c in File.ReadAllText(path, Encoding.UTF8))
                    if (!char.IsControl(c)) set.Add(c);
            return new string(set.ToArray());
        }
    }
}
