using Pokepedia.Content;
using Pokepedia.Domain.Battle;
using Pokepedia.Presentation.Audio;
using Pokepedia.Presentation.Audio.Synth;
using Pokepedia.Presentation.Battle.Fx;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 배틀 화면의 "보이는 것"을 만들고 들고 있는다 (PROJECT.md 5.3·5.5): HUD 캔버스(정보 상자·메시지·메뉴)와
    /// 월드(배경·조명·후처리·포켓몬 자리), 연출 도구(VfxLayer·AnimationDirector·VsIntro)와 소리.
    /// 흐름(언제 무엇을 보일지)은 BattleScreen 이, 이벤트 재생은 BattleEventPlayer 가, 실험실은 BattleLab 이 맡는다.
    /// </summary>
    public sealed class BattleHud
    {
        const int CanvasOrder = 10;

        public TMP_FontAsset Font { get; }
        public GameObject Canvas { get; }
        public RectTransform Screen { get; }
        public BattleWorld World { get; }
        public BattleLighting Lighting { get; }
        public BackdropTheme Theme { get; }
        public BattleSideView[] Sides { get; } = new BattleSideView[2];
        public VfxLayer Vfx { get; }
        public AnimationDirector Director { get; }
        public VsIntro Vs { get; }
        public SoundBank Sound { get; }
        public MessageBox Message { get; }
        public ChoiceMenu ActionMenu { get; }
        public ChoiceMenu MoveMenu { get; }
        public ChoiceMenu ResultMenu { get; }
        public MoveCards Moves { get; }

        public BattleHud(MonoBehaviour host, TMP_FontAsset font, Material flashMaterial, Material particleMaterial, Material litMaterial,
                         ContentCatalog catalog, AnimationCatalog animation)
        {
            Font = font;
            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PixelCanvasScaler));
            canvasGo.transform.SetParent(host.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            // HUD(HP 바·메뉴·메시지)는 Overlay 캔버스: 월드 카메라의 후처리(PROJECT.md 5.5)를 받지 않고 항상 월드 위에 그려진다
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;
            Canvas = canvasGo;
            Sound = new SoundBank(host.gameObject);

            // 월드(배경·발판·포켓몬·이펙트)는 SpriteRenderer 와 카메라로 그린다
            World = new BattleWorld(host.transform, Camera.main, particleMaterial, litMaterial);
            // 2.5D 연출 (PROJECT.md 5.5): 배경 레이어(패럴랙스·안개·비) + 조명(분위기·타입별 glow) + 후처리(Bloom)
            Theme = BackdropTheme.Storm;
            var backdrop = new BattleBackdrop(World, Theme);
            World.Backdrop = backdrop;
            World.Container.AddComponent<BackdropTicker>().OnTick = backdrop.Tick;
            Lighting = new BattleLighting(World.Root, Theme.Ambient, 1f);
            new BattlePostFx(World.Container.transform, Camera.main);

            // 240×216 HUD 영역을 캔버스 가운데에 둔다 (정수 배율은 PixelCanvasScaler, 홀수 창의 반 픽셀은 PixelAlign)
            var screen = new GameObject("Screen", typeof(RectTransform), typeof(PixelAlign));
            screen.transform.SetParent(canvasGo.transform, false);
            var srt = (RectTransform)screen.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight);
            Screen = srt;

            Sides[BattleState.Npc] = MakeSide("Npc", new Vector2(178, 86), 6, 6, 116, 32, false, flashMaterial);
            Sides[BattleState.Player] = MakeSide("Player", new Vector2(66, 148), 122, 100, 112, 46, true, flashMaterial);
            Vfx = new VfxLayer(World, srt, particleMaterial);
            Director = new AnimationDirector(host, Sides, Vfx, Sound, animation, catalog, World, Lighting);
            Vs = new VsIntro(host, srt, font, Sound, Vfx);

            var msgInner = Ui.Box(srt, "MessageBox", 2, 156, 236, 58);
            Message = new MessageBox(msgInner, font);

            var actInner = Ui.Box(srt, "ActionMenu", 144, 156, 94, 58);
            ActionMenu = new ChoiceMenu(actInner.parent.gameObject, actInner, font,
                new[] { new Vector3(6, 8, 80), new Vector3(6, 28, 80) }, 1);
            ActionMenu.Hide();

            // 기술 메뉴: 큰 타입 색 버튼 3개 (시안 C, MoveCards). 버튼 바깥을 누르면 뒤로(휴대폰엔 X·Esc 가 없다)
            Moves = new MoveCards(srt, font, animation);
            MoveMenu = Moves.Menu;
            MoveMenu.Hide();

            var resInner = Ui.Box(srt, "ResultMenu", 96, 108, 142, 46);
            ResultMenu = new ChoiceMenu(resInner.parent.gameObject, resInner, font,
                new[] { new Vector3(6, 6, 128), new Vector3(6, 24, 128) }, 1);
            ResultMenu.Hide();

            Vfx.BringToFront(); // 화면 번쩍임이 모든 것 위에 오도록
            foreach (var m in new[] { ActionMenu, MoveMenu, ResultMenu }) WireSounds(m);
            MoveMenu.Cancelled += () => Sound.Play(SfxKind.Cancel);
        }

        /// <summary>메뉴 커서 이동·결정 소리</summary>
        public void WireSounds(ChoiceMenu menu)
        {
            menu.Navigated += () => Sound.Play(SfxKind.CursorMove);
            menu.Chosen += _ => Sound.Play(SfxKind.Confirm);
        }

        /// <summary>배틀 화면(캔버스와 월드)을 켜고 끈다</summary>
        public void SetVisible(bool visible)
        {
            Canvas.SetActive(visible);
            World.Container.SetActive(visible);
        }

        /// <summary>새 판 무대: 카메라 가운데, 포켓몬·정보 상자 숨김, 메시지 비움, 비 오는 날씨·기본 조명, 배틀 음악 처음부터</summary>
        public void ResetStage()
        {
            World.ResetPan();
            foreach (var v in Sides) v.Reset();
            Message.Clear();
            World.Backdrop?.SetWeather(1, 0);
            Lighting.SetAmbient(Theme.Ambient);
            Sound.PlayMusic(SoundBank.BattleMusicKey);
        }

        BattleSideView MakeSide(string name, Vector2 feet, float bx, float by, float bw, float bh, bool player, Material flashMaterial)
        {
            var inner = Ui.Panel(Screen, name + "Info", bx, by, bw, bh, ledgeToRight: !player);
            var nameLabel = Ui.Label(inner, "Name", Font, 4, 2, bw - 50, 14);
            Ui.Label(inner, "Level", Font, bw - 42, 2, 36, 14, TextAlignmentOptions.TopRight).text = ":L50";
            var hp = new HpBar(inner, Font, 6, 18, bw - 34, player);
            var view = new BattleSideView(World, name, feet, flashMaterial, inner.parent.parent.gameObject, nameLabel, hp);
            view.Reset();
            return view;
        }
    }
}
