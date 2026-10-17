using System;
using System.Collections.Generic;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// ▶ 커서 메뉴. 방향키로 움직이고 Z 로 고르고 X 로 취소한다. 항목을 클릭·터치해도 고른다.
    /// columns 로 격자(기술 2×2)도 표현한다. MonoBehaviour 가 아니라 화면의 Update 가 Tick() 을 부른다.
    /// </summary>
    public sealed class ChoiceMenu
    {
        readonly GameObject root;
        readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> cursors = new List<TextMeshProUGUI>();
        readonly int columns;
        int index;

        public event Action<int> Chosen;
        public event Action Cancelled;
        /// <summary>커서가 옮겨 갈 때 (기술 정보 표시용, Show 직후 포함)</summary>
        public event Action<int> Highlighted;
        /// <summary>사용자가 방향키로 커서를 옮겼을 때 (효과음용)</summary>
        public event Action Navigated;

        public bool Visible => root.activeSelf;
        public int Count { get; private set; }

        /// <param name="root">보이기/숨기기 대상 (테두리 상자 전체)</param>
        /// <param name="parent">항목을 붙일 곳 (상자 안쪽)</param>
        /// <param name="cells">항목별 (x, y, w) — 부모 기준 픽셀 좌표</param>
        public ChoiceMenu(GameObject root, RectTransform parent, TMP_FontAsset font, IReadOnlyList<Vector3> cells, int columns)
        {
            this.columns = Mathf.Max(1, columns);
            this.root = root;
            for (int i = 0; i < cells.Count; i++)
            {
                int captured = i;
                var c = cells[i];
                var cursor = Ui.Label(parent, "Cursor" + i, font, c.x, c.y, 12, 14);
                cursor.text = "▶";
                var label = Ui.Label(parent, "Item" + i, font, c.x + 12, c.y, c.z - 12, 14);
                label.textWrappingMode = TextWrappingModes.NoWrap;   // 칸보다 긴 이름이 다음 줄로 넘어가 아래 칸과 겹치지 않게 (한 줄 유지)
                label.raycastTarget = true;
                var button = label.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => { if (Visible && captured < Count) { Move(captured); Chosen?.Invoke(captured); } });
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

        public void Tick()
        {
            if (!Visible || Count == 0) return;
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
            for (int k = 0; k < cursors.Count; k++) cursors[k].enabled = k == index;
            Highlighted?.Invoke(index);
        }
    }
}
