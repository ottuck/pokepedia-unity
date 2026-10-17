using System;
using System.Collections.Generic;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// ▶ 커서 메뉴. 방향키로 움직이고 Z 로 고르고 X 로 취소한다. 항목을 클릭·터치해도 고른다(PreviewOnTouch·CancelOutside 는 휴대폰용).
    /// columns 로 격자(기술 2×2)도 표현한다. MonoBehaviour 가 아니라 화면의 Update 가 Tick() 을 부른다.
    /// </summary>
    public sealed class ChoiceMenu
    {
        readonly GameObject root;
        readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> cursors = new List<TextMeshProUGUI>();
        readonly int columns;
        readonly bool showCursor;
        int index;

        public event Action<int> Chosen;
        public event Action Cancelled;
        /// <summary>커서가 옮겨 갈 때 (기술 정보 표시용, Show 직후 포함)</summary>
        public event Action<int> Highlighted;
        /// <summary>사용자가 방향키로 커서를 옮겼을 때 (효과음용)</summary>
        public event Action Navigated;

        public bool Visible => root.activeSelf;
        public int Count { get; private set; }

        /// <summary>
        /// 터치로 고를 때 첫 탭은 커서만 옮기고(기술 정보 미리 보기), 커서가 있는 항목을 누르면 고른다.
        /// 마우스는 바로 고른다(가리키기 대신 클릭하는 사용자가 두 번 누르지 않게).
        /// </summary>
        public bool PreviewOnTouch { get; set; }

        /// <summary>
        /// 이 영역들 바깥을 누르면 취소한다. 키보드(X·Esc)가 없는 휴대폰에서 메뉴를 빠져나가는 길.
        /// 비어 있으면 바깥 탭으로 취소하지 않는다.
        /// </summary>
        public RectTransform[] CancelOutside { get; set; }

        /// <param name="root">보이기/숨기기 대상 (테두리 상자 전체)</param>
        /// <param name="parent">항목을 붙일 곳 (상자 안쪽)</param>
        /// <param name="cells">항목별 (x, y, w) — 부모 기준 픽셀 좌표</param>
        /// <param name="withCursor">▶ 커서를 그린다. false 면 고른 항목은 화면 쪽(Highlighted)이 표시한다 (기술 버튼)</param>
        /// <param name="itemHeight">항목 높이 = 눌림 영역 높이</param>
        public ChoiceMenu(GameObject root, RectTransform parent, TMP_FontAsset font, IReadOnlyList<Vector3> cells, int columns,
                          bool withCursor = true, float itemHeight = 14, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            this.columns = Mathf.Max(1, columns);
            this.root = root;
            showCursor = withCursor;
            for (int i = 0; i < cells.Count; i++)
            {
                int captured = i;
                var c = cells[i];
                var cursor = Ui.Label(parent, "Cursor" + i, font, c.x, c.y, 12, 14);
                cursor.text = "▶";
                var label = withCursor ? Ui.Label(parent, "Item" + i, font, c.x + 12, c.y, c.z - 12, itemHeight, align)
                                   : Ui.Label(parent, "Item" + i, font, c.x, c.y, c.z, itemHeight, align);
                label.textWrappingMode = TextWrappingModes.NoWrap;   // 칸보다 긴 이름이 다음 줄로 넘어가 아래 칸과 겹치지 않게 (한 줄 유지)
                label.raycastTarget = true;
                var button = label.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnClicked(captured));
                cursors.Add(cursor);
                labels.Add(label);
            }
        }

        public void Show(IReadOnlyList<string> items, int startIndex = 0)
        {
            Count = Mathf.Min(items.Count, labels.Count);
            for (int i = 0; i < labels.Count; i++)
            {
                labels[i].text = i < Count ? items[i] : "";
                labels[i].gameObject.SetActive(i < Count);
            }
            root.SetActive(true);
            Move(Mathf.Clamp(startIndex, 0, Count - 1));
        }

        public void Hide() => root.SetActive(false);

        /// <summary>항목 라벨 (화면 쪽이 버튼 안으로 옮기거나 여백을 줄 때)</summary>
        public TextMeshProUGUI Label(int i) => labels[i];

        void OnClicked(int i)
        {
            if (!Visible || i >= Count) return;
            if (PreviewOnTouch && BattleInput.IsTouch && i != index) { Move(i); Navigated?.Invoke(); return; }
            Move(i);
            Chosen?.Invoke(i);
        }

        bool PressedOutside()
        {
            if (CancelOutside == null || CancelOutside.Length == 0 || !BattleInput.PointerPressed) return false;
            var p = BattleInput.PointerPosition;
            foreach (var r in CancelOutside)
                if (r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, p, null)) return false;
            return true;   // 오버레이 캔버스라 카메라는 null
        }

        public void Tick()
        {
            if (!Visible || Count == 0) return;
            if (PressedOutside()) { Cancelled?.Invoke(); return; }
            int next = index;
            if (BattleInput.Up) next -= columns;
            if (BattleInput.Down) next += columns;
            if (columns > 1 && BattleInput.Left && index % columns > 0) next -= 1;
            if (columns > 1 && BattleInput.Right && index % columns < columns - 1) next += 1;
            if (next != index && next >= 0 && next < Count) { Move(next); Navigated?.Invoke(); }
            if (BattleInput.Confirm) Chosen?.Invoke(index);
            else if (BattleInput.Cancel) Cancelled?.Invoke();
        }

        void Move(int i)
        {
            index = i;
            for (int k = 0; k < cursors.Count; k++) cursors[k].enabled = showCursor && k == index;
            Highlighted?.Invoke(index);
        }
    }
}
