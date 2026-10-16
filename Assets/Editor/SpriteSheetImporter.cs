using UnityEditor;
using UnityEngine;

namespace Pokepedia.EditorTools
{
    /// <summary>
    /// Tools/sprites 가 만든 시트(Assets/Resources/Sprites/*.png)의 임포트 설정을 고정한다.
    /// 도트가 번지지 않도록 Point 필터, 압축 없음, 밉맵 없음. (시트는 커밋하지 않으므로 .meta 대신 코드로 보장)
    /// </summary>
    public sealed class SpriteSheetImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Sprites/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.filterMode = FilterMode.Point;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
            t.isReadable = false;
        }
    }
}
