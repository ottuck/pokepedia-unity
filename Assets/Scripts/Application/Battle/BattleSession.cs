using System;
using System.Collections.Generic;
using System.Linq;
using Pokepedia.App.Host;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.App.Battle
{
    /// <summary>Demo = 피카츄 vs 거북왕 고정(도망 불가), Collection = 보유 ∩ 지원 (PROJECT.md 9.2)</summary>
    public enum BattleMode { Demo, Collection }

    public enum RunOutcome
    {
        /// <summary>"도망칠 수 없다!" (Demo) — 같은 배틀이 이어진다</summary>
        CannotEscape,
        /// <summary>"무사히 도망쳤다!" (Collection) — 호출자가 NextBattle() 로 새 상대를 부른다</summary>
        Escaped,
    }

    /// <summary>
    /// 배틀 흐름(PROJECT.md 5.3, 7.2 Application): 플레이어 행동 접수 → NPC 행동 결정(NpcBattleAi) → 엔진 호출 → 이벤트 전달,
    /// 그리고 승리 후 다음 상대 / 패배 후 다시 도전 / 도망. 화면은 이 클래스가 돌려주는 이벤트를 재생만 한다.
    /// 한 판마다 BattleEngine 을 새로 만든다 (플레이어 HP 도 가득 찬 상태로 다시 시작).
    /// 생성자로 받은 IRandom 하나를 모든 판의 엔진이 이어서 쓴다: 시드가 같고 입력이 같으면 세션 전체가 재현된다
    /// (pickOpponent 가 같은 IRandom 을 쓰면 상대 추첨까지 포함).
    /// </summary>
    public sealed class BattleSession
    {
        readonly ContentCatalog catalog;
        readonly BattleRules rules;
        readonly IRandom random;
        readonly Func<int> pickOpponent;
        readonly int playerSpeciesId;
        readonly bool canEscape;
        readonly bool faintChoice;
        int npcSpeciesId;
        BattleEngine engine;
        bool escaped;

        /// <summary>응원 부활 때 플레이어 포켓몬이 주는 피해 배율</summary>
        public const double CheerPowerBoost = 1.5;

        /// <param name="canEscape">Collection 에서 도망칠 수 있는지. 상대 후보가 1마리면 false (OpponentPicker.CandidateCount, 9.2). Demo 는 항상 도망 불가.</param>
        /// <param name="faintChoice">플레이어가 쓰러질 때마다 지는 대신 [응원한다(Cheer) / 트레이너가 처리한다(TrainerFinish)]를 고르게 한다 (5.3)</param>
        public BattleSession(ContentCatalog catalog, BattleRules rules, IRandom random, BattleMode mode,
                             int playerSpeciesId, Func<int> pickOpponent, bool canEscape = true, bool faintChoice = false)
        {
            this.canEscape = canEscape;
            this.faintChoice = faintChoice;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.pickOpponent = pickOpponent ?? throw new ArgumentNullException(nameof(pickOpponent));
            Mode = mode;
            catalog.GetSpecies(playerSpeciesId); // 없는 종이면 여기서 KeyNotFoundException
            this.playerSpeciesId = playerSpeciesId;
            npcSpeciesId = pickOpponent();
            NewEngine();
        }

        public BattleMode Mode { get; }

        /// <summary>현재 배틀의 엔진 상태 (화면이 이름·HP·기술 목록을 읽는다)</summary>
        public BattleState State => engine.State;

        public Battler Player => State.Sides[BattleState.Player].Active;
        public Battler Npc => State.Sides[BattleState.Npc].Active;

        /// <summary>기술 정보(이름 키·animKind 포함). 인덱스는 Player.Moves 와 같다.</summary>
        public IReadOnlyList<MoveInfo> PlayerMoveInfos => catalog.MoveSetFor(playerSpeciesId);

        /// <summary>현재 배틀이 끝났다: 승패가 났거나(State.Winner) 도망쳤다(승자 없음). 이후엔 NextBattle / Retry 만 할 수 있다.</summary>
        public bool IsOver => escaped || (State.IsOver && !AwaitingFaintChoice);

        /// <summary>플레이어가 쓰러져 선택을 기다린다 (faintChoice). Cheer 또는 TrainerFinish 만 할 수 있다.</summary>
        public bool AwaitingFaintChoice => faintChoice && !escaped && State.Winner == BattleState.Npc;

        /// <summary>첫 배틀 시작: 등장 이벤트</summary>
        public IReadOnlyList<BattleEvent> Start() => engine.Start();

        /// <summary>플레이어가 기술을 골랐다. NPC 는 AI 가 고르고 한 턴을 진행한다.</summary>
        public IReadOnlyList<BattleEvent> UseMove(int moveIndex)
        {
            if (IsOver) throw new InvalidOperationException("battle is over; call NextBattle() or Retry()");
            if (AwaitingFaintChoice) throw new InvalidOperationException("player fainted; call Cheer() or TrainerFinish()");
            var npcAction = NpcBattleAi.Choose(Npc, Player, rules, State.Turn + 1);
            var events = engine.PlayTurn(new UseMove(moveIndex), npcAction);
            // 쓰러져 선택을 기다리면 패배(BattleEnded)는 아직 알리지 않는다
            return AwaitingFaintChoice ? events.Where(e => !(e is BattleEnded)).ToList() : events;
        }

        /// <summary>[응원한다] 쓰러진 플레이어가 HP 가득, 주는 피해 ×CheerPowerBoost 로 다시 일어나 배틀이 이어진다</summary>
        public IReadOnlyList<BattleEvent> Cheer()
        {
            if (!AwaitingFaintChoice) throw new InvalidOperationException("cheer is only for after the player faints");
            return engine.Revive(BattleState.Player, CheerPowerBoost);
        }

        /// <summary>[트레이너가 처리한다] 트레이너가 던진 몬스터볼에 맞아 상대가 쓰러지고 플레이어가 이긴다 (배틀을 바로 끝내고 싶을 때)</summary>
        public IReadOnlyList<BattleEvent> TrainerFinish()
        {
            if (!AwaitingFaintChoice) throw new InvalidOperationException("the trainer only steps in after the player faints");
            return new BattleEvent[] { new TrainerThrewBall(BattleState.Npc) }.Concat(engine.KnockOut(BattleState.Npc)).ToList();
        }

        /// <summary>Demo(와 상대 후보가 1마리인 Collection)는 도망칠 수 없다(상태 변화 없음). 그 밖의 Collection 은 도망에 항상 성공하고 이 배틀이 끝난다.</summary>
        public RunOutcome TryRun()
        {
            if (IsOver) throw new InvalidOperationException("battle is over");
            if (Mode == BattleMode.Demo || !canEscape) return RunOutcome.CannotEscape;
            escaped = true;
            return RunOutcome.Escaped;
        }

        /// <summary>새 상대(pickOpponent)로 새 배틀. 승리 후 [다음 상대와 싸운다], 도망 성공 후 호출. 배틀 중에는 InvalidOperationException.</summary>
        public IReadOnlyList<BattleEvent> NextBattle()
        {
            if (!IsOver) throw new InvalidOperationException("battle is still on; NextBattle() is only for after it ends");
            npcSpeciesId = pickOpponent();
            NewEngine();
            return engine.Start();
        }

        /// <summary>같은 상대로 처음부터 다시. 패배 후 [다시 도전]. 배틀 중에는 InvalidOperationException.</summary>
        public IReadOnlyList<BattleEvent> Retry()
        {
            if (!IsOver) throw new InvalidOperationException("battle is still on; Retry() is only for after it ends");
            NewEngine();
            return engine.Start();
        }

        void NewEngine()
        {
            var player = CreateBattler(playerSpeciesId);
            var npc = CreateBattler(npcSpeciesId);
            engine = new BattleEngine(new BattleState(new BattleSide(player), new BattleSide(npc)), rules, random);
            escaped = false;
        }

        Battler CreateBattler(int speciesId)
            => Battler.Create(catalog.GetSpecies(speciesId).Species, catalog.MovesFor(speciesId), rules);
    }

    /// <summary>NPC 상대 고르기 (PROJECT.md 6.5). Demo 는 () =&gt; 9 같은 고정 함수를 쓴다.</summary>
    public static class OpponentPicker
    {
        /// <summary>
        /// 로스터에서 npcWeight 비례로 추첨한다(가중치 0 은 나오지 않음). random.Next(가중치 합) 을 한 번 뽑아 로스터 순서대로 누적한다.
        /// excludeId 는 후보에서 뺀다(예: 플레이어와 같은 종 피하기). 후보가 없으면 InvalidOperationException.
        /// </summary>
        public static Func<int> Weighted(ContentCatalog catalog, IRandom random, int? excludeId = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var candidates = Candidates(catalog, excludeId);
            int total = candidates.Sum(r => r.NpcWeight);
            if (total <= 0) throw new InvalidOperationException("no roster entry can be an NPC (all weights are 0 or excluded)");
            return () =>
            {
                int roll = random.Next(total);
                foreach (var r in candidates)
                {
                    if (roll < r.NpcWeight) return r.Id;
                    roll -= r.NpcWeight;
                }
                throw new InvalidOperationException("unreachable: roll exceeded total weight");
            };
        }

        /// <summary>Weighted 가 고를 수 있는 상대 수. 1 이면 도망쳐도 같은 상대가 나오므로 도망을 끈다.</summary>
        public static int CandidateCount(ContentCatalog catalog, int? excludeId = null) => Candidates(catalog, excludeId).Length;

        static RosterEntry[] Candidates(ContentCatalog catalog, int? excludeId)
            => catalog.Roster.Where(r => r.NpcWeight > 0 && r.Id != excludeId).ToArray();
    }

    /// <summary>Collection 모드에서 플레이어로 고를 수 있는 포켓몬 하나 (Shiny 는 외형만, 6.5)</summary>
    public sealed record PlayerChoice(int SpeciesId, bool Shiny);

    /// <summary>Collection 모드의 플레이어 후보: 보유 ∩ 플레이어 가능(roster.json playable), 로스터 순서 (PROJECT.md 9.2)</summary>
    public static class CollectionLineup
    {
        /// <summary>같은 포켓몬을 여러 장(일반·shiny) 가졌으면 하나로 묶고, shiny 를 하나라도 가졌으면 shiny 로 낸다.</summary>
        public static IReadOnlyList<PlayerChoice> Players(ContentCatalog catalog, IEnumerable<OwnedSticker> owned)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var stickers = (owned ?? Enumerable.Empty<OwnedSticker>()).Where(s => s != null).ToArray();
            return catalog.Roster
                .Where(r => r.Playable && stickers.Any(s => s.id == r.Id))
                .Select(r => new PlayerChoice(r.Id, stickers.Any(s => s.id == r.Id && s.shiny)))
                .ToArray();
        }
    }
}
