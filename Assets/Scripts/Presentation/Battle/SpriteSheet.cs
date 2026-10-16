using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// BW 애니메이션 시트(PNG + JSON, Tools/sprites 출력)를 Sprite[] + 프레임 지연으로 읽는다.
    /// 시트는 Assets/Resources/Sprites 에 생성되며(커밋 안 함) 이름으로 Resources.Load 한다.
    /// Java 비교: [Serializable] + JsonUtility 는 Jackson POJO 매핑처럼 public 필드 이름으로 읽는다.
    /// </summary>
    public sealed class SpriteSheet
    {
        [Serializable] sealed class FrameDto { public int x, y, w, h, delayMs; }
        [Serializable] sealed class SizeDto { public int w, h; }
        [Serializable] sealed class SheetDto { public SizeDto canvas; public bool loop; public FrameDto[] frames; }

        public Sprite[] Frames { get; }
        public float[] Delays { get; }
        /// <summary>한 칸 크기(px). 모든 프레임이 같다. 발 위치는 바닥 중앙.</summary>
        public Vector2Int Size { get; }

        SpriteSheet(Sprite[] frames, float[] delays, Vector2Int size)
        {
            Frames = frames; Delays = delays; Size = size;
        }

        static readonly Dictionary<string, SpriteSheet> Cache = new Dictionary<string, SpriteSheet>();

        /// <summary>name 예: "front_25_sheet" (species.json 의 sprites 값). 없으면 null.</summary>
        public static SpriteSheet Load(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>("Sprites/" + name);
            var json = Resources.Load<TextAsset>("Sprites/" + name);
            if (tex == null || json == null)
            {
                Debug.LogWarning($"[SpriteSheet] missing Resources/Sprites/{name} — Tools/sprites/build_sprites.py 를 실행했는지 확인");
                return null;
            }
            var dto = JsonUtility.FromJson<SheetDto>(json.text);
            var frames = new Sprite[dto.frames.Length];
            var delays = new float[dto.frames.Length];
            var byCell = new Dictionary<long, Sprite>(); // dedupe 된 셀은 Sprite 하나를 재사용
            for (int i = 0; i < dto.frames.Length; i++)
            {
                var f = dto.frames[i];
                long key = ((long)f.x << 32) | (uint)f.y;
                if (!byCell.TryGetValue(key, out var s))
                {
                    // JSON 은 좌상단 원점, Unity 텍스처는 좌하단 원점
                    s = Sprite.Create(tex, new Rect(f.x, tex.height - f.y - f.h, f.w, f.h), new Vector2(0.5f, 0f), 1f, 0, SpriteMeshType.FullRect);
                    s.name = $"{name}_{i}";
                    byCell[key] = s;
                }
                frames[i] = s;
                delays[i] = Mathf.Max(0.02f, f.delayMs / 1000f);
            }
            var sheet = new SpriteSheet(frames, delays, new Vector2Int(dto.canvas.w, dto.canvas.h));
            Cache[name] = sheet;
            return sheet;
        }
    }
}
