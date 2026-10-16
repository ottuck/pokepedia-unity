using UnityEngine.InputSystem;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 키보드(방향키 + Z/X, Enter/Space/Esc)와 포인터(마우스·터치) 입력을 한곳에서 읽는다 (PROJECT.md 5.3 입력).
    /// 메뉴 항목 클릭은 uGUI Button 이 처리하고, 여기서는 "아무 곳이나 눌러 다음 메시지" 같은 전역 입력만 본다.
    /// </summary>
    public static class BattleInput
    {
        static Keyboard Kb => Keyboard.current;

        public static bool Confirm => Kb != null && (Kb.zKey.wasPressedThisFrame || Kb.enterKey.wasPressedThisFrame || Kb.spaceKey.wasPressedThisFrame);
        public static bool Cancel => Kb != null && (Kb.xKey.wasPressedThisFrame || Kb.escapeKey.wasPressedThisFrame || Kb.backspaceKey.wasPressedThisFrame);
        public static bool Up => Kb != null && Kb.upArrowKey.wasPressedThisFrame;
        public static bool Down => Kb != null && Kb.downArrowKey.wasPressedThisFrame;
        public static bool Left => Kb != null && Kb.leftArrowKey.wasPressedThisFrame;
        public static bool Right => Kb != null && Kb.rightArrowKey.wasPressedThisFrame;

        /// <summary>마우스 클릭 또는 터치 시작 (Pointer 는 마우스·터치·펜을 모두 포함)</summary>
        public static bool PointerPressed => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;

        /// <summary>메시지 넘기기: 확인 키 또는 화면 아무 곳 누르기</summary>
        public static bool Advance => Confirm || PointerPressed;
    }
}
