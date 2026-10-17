using System.Collections;
using System.Collections.Generic;
using Pokepedia.Content;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 기술 메뉴 (시안 C, 2026-10-17 사용자 결정): 기술 3개(기본기 + 속성 기술 2개, PROJECT.md 6.3)를 큰 타입 색 버튼으로 나란히 둔다.
    /// 이름·위력·명중이 버튼 안에 있어 미리보기 탭 없이 한 번에 고르고, 버튼 전체가 눌림 영역이라 손가락으로 누르기 쉽다.
    /// 한 번 누르면 바로 쓰므로 평소에는 세 버튼이 같은 모양이고, 누르는 순간 2px 눌렸다 튀어 오르며 흰 테두리가 번쩍인다(Press).
    /// 키보드 방향키로 고를 때만 지금 버튼을 흰 테두리로 띄워 보여 준다. 버튼 그림은 타입 색으로 코드에서 그린다(도트 텍스처, 점 필터).
    /// 입력은 ChoiceMenu(커서 없음, 좌우로 이동)가 맡는다.
    /// </summary>
    public sealed class MoveCards
    {
        public const int Count = 3;
        const int W = 76, H = 56, Lift = 2, Top = 158, Radius = 5;
        static readonly int[] Xs = { 3, 82, 161 };
        static readonly Color Back = Ui.Hex(0x2C3832);   // 버튼 사이로 메시지 창이 비치지 않게 까는 판 (배틀 화면 아래쪽 색)
        static readonly Dictionary<(Color, bool), Sprite> Sprites = new Dictionary<(Color, bool), Sprite>();

        sealed class Card
        {
            public RectTransform Rt;
            public Image Shadow, Bg, Sep;
            public TextMeshProUGUI PowerName, PowerValue, AccName, AccValue;
            public Color Primary;
        }

        readonly AnimationCatalog animation;
        readonly Card[] cards = new Card[Count];
        bool keyboardFocus;   // 방향키를 쓴 적이 있다: 지금 고른 버튼을 띄워 보여 준다
        int highlighted;

        public ChoiceMenu Menu { get; }
        /// <summary>버튼 영역 전체 (바깥 탭 취소 판정용)</summary>
        public RectTransform Root { get; }

        public MoveCards(RectTransform screen, TMP_FontAsset font, AnimationCatalog animation)
        {
            this.animation = animation;
            Root = Ui.Rect(screen, "MoveCards", 0, Top - Lift, PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight - (Top - Lift));
            Ui.Fill(Root, "Back", 0, 0, Root.sizeDelta.x, Root.sizeDelta.y, Back);

            var cells = new Vector3[Count];
            for (int i = 0; i < Count; i++)
            {
                var rt = Ui.Rect(Root, "Card" + i, Xs[i], Lift, W, H);
                var c = new Card { Rt = rt };
                c.Shadow = Ui.Fill(rt, "Shadow", 0, 2, W, H, new Color(0, 0, 0, 0.35f));
                c.Bg = Ui.Fill(rt, "Bg", 0, 0, W, H, Color.white);
                c.Sep = Ui.Fill(rt, "Sep", 8, 19, W - 16, 1, Color.white);
                c.PowerName = Ui.Label(rt, "PowerName", font, 6, 21, W - 12, 14);
                c.PowerValue = Ui.Label(rt, "PowerValue", font, 6, 21, W - 12, 14, TextAlignmentOptions.TopRight);
                c.AccName = Ui.Label(rt, "AccName", font, 6, 35, W - 12, 14);
                c.AccValue = Ui.Label(rt, "AccValue", font, 6, 35, W - 12, 14, TextAlignmentOptions.TopRight);
                cards[i] = c;
                // 이름 칸 = 버튼 전체 (눌림 영역). 위쪽 3px 띄워 가운데 정렬
                cells[i] = new Vector3(Xs[i], Lift, W);
            }

            Menu = new ChoiceMenu(Root.gameObject, Root, font, cells, Count, withCursor: false, itemHeight: H, align: TextAlignmentOptions.Top);
            // 이름 라벨을 각 버튼 안으로 옮겨 버튼과 함께 떠오르게 한다(위치는 그대로, 맨 위에 그려짐)
            for (int i = 0; i < Count; i++) Menu.Label(i).rectTransform.SetParent(cards[i].Rt, true);
            for (int i = 0; i < Count; i++) Menu.Label(i).margin = new Vector4(2, 3, 2, 0);
            Menu.Highlighted += i => { highlighted = i; Refresh(); };
            Menu.Navigated += () => { keyboardFocus = true; Refresh(); };   // 방향키 이동에서만 불린다
            Menu.CancelOutside = new[] { Root };
        }

        /// <summary>기술 메뉴를 열 때: 타입 색과 위력·명중 (이름은 Menu.Show 가 넣는다)</summary>
        public void SetMoves(IReadOnlyList<MoveInfo> moves, string powerLabel, string accuracyLabel)
        {
            for (int i = 0; i < Count; i++)
            {
                var c = cards[i];
                bool on = i < moves.Count;
                c.Rt.gameObject.SetActive(on);
                if (!on) continue;
                var m = moves[i].Move;
                c.Primary = Ui.Hex(animation.TypeFor(m.Type).PrimaryRgb);
                c.Sep.color = Color.Lerp(c.Primary, Color.black, 0.2f);
                c.PowerName.color = c.AccName.color = Color.Lerp(c.Primary, Color.black, 0.6f);
                c.PowerName.text = powerLabel;
                c.AccName.text = accuracyLabel;
                c.PowerValue.text = m.Power.ToString();
                c.AccValue.text = m.Accuracy.ToString();
            }
        }

        /// <summary>
        /// 이름이 버튼 안쪽 폭보다 길면(영어 "Thunderbolt") 글자 간격만 좁혀 테두리에 닿지 않게 한다.
        /// 글자 크기는 그대로 둔다(도트 글꼴이라 크기를 줄이면 흐려진다). Menu.Show 뒤에 부른다.
        /// </summary>
        public void FitNames()
        {
            const float inner = W - 10;
            for (int i = 0; i < Count; i++)
            {
                var label = Menu.Label(i);
                label.characterSpacing = 0;
                if (!label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text)) continue;
                float width = label.GetPreferredValues(label.text).x;
                int gaps = label.text.Length - 1;
                if (width > inner && gaps > 0)
                    label.characterSpacing = -(width - inner) / gaps / label.fontSize * 100f;   // 단위: em 의 1/100
            }
        }

        void Refresh()
        {
            for (int i = 0; i < Count; i++) Show(i, keyboardFocus && i == highlighted, raised: keyboardFocus && i == highlighted);
        }

        void Show(int i, bool bright, bool raised, int pressed = 0)
        {
            var c = cards[i];
            c.Bg.sprite = SpriteFor(c.Primary, bright);
            c.Shadow.sprite = SpriteFor(c.Primary, false);
            c.Shadow.enabled = raised;
            c.Rt.anchoredPosition = new Vector2(Xs[i], raised ? 0 : -Lift - pressed);
        }

        /// <summary>누른 버튼의 짧은 반응(약 0.2초): 2px 눌리며 흰 테두리 → 제자리로 튀어 올랐다가 원래 모양. 끝나면 화면이 기술을 쓴다.</summary>
        public IEnumerator Press(int i)
        {
            Show(i, bright: true, raised: false, pressed: 2);
            yield return Fx.VfxLayer.Wait(0.08f);
            Show(i, bright: true, raised: true);
            yield return Fx.VfxLayer.Wait(0.1f);
            Refresh();
        }

        static Sprite SpriteFor(Color primary, bool on)
        {
            if (Sprites.TryGetValue((primary, on), out var s)) return s;
            return Sprites[(primary, on)] = Draw(primary, on);
        }

        /// <summary>둥근 버튼: 고르면 바깥 1px 진한 선 + 흰 테두리 2px, 아니면 짙은 타입 색 테두리 2px. 안은 위가 옅고 아래가 진한 그라데이션.</summary>
        static Sprite Draw(Color primary, bool on)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color border = on ? Color.white : Color.Lerp(primary, Color.black, 0.35f);
            Color top = Color.Lerp(primary, Color.white, on ? 0.6f : 0.75f);
            Color bottom = Color.Lerp(primary, Color.white, on ? 0.05f : 0.35f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int dx = Mathf.Max(0, Mathf.Max(Radius - x, x - (W - 1 - Radius)));
                    int dy = Mathf.Max(0, Mathf.Max(Radius - y, y - (H - 1 - Radius)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c;
                    if (dist > Radius + 0.5f) c = Color.clear;
                    else
                    {
                        float inset = dx > 0 && dy > 0 ? Radius - dist : Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                        if (on && inset < 1) c = Ui.Ink;
                        else if (inset < (on ? 3 : 2)) c = border;
                        else c = Color.Lerp(bottom, top, y / (float)(H - 1));   // 텍스처 y=0 이 아래
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
