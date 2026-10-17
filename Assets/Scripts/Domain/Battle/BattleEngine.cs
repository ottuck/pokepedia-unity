using System;
using System.Collections.Generic;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Domain.Battle
{
    /// <summary>
    /// 턴제 배틀 엔진. 입력은 양쪽의 BattleAction, 출력은 BattleEvent 목록이다.
    /// 상태(BattleState)를 직접 바꾸며, 난수는 모두 생성자로 받은 IRandom 에서만 뽑는다.
    ///
    /// 난수 순서(테스트로 고정, BattleEngineTests 참고):
    ///   1) 턴 순서: 스피드가 같을 때만 Next(2) — 0 이면 플레이어 먼저
    ///   2) 행동마다: 명중률 &lt; 100 이면 Next(100) (값 &lt; 명중률이면 명중)
    ///   3) 명중하면: 급소 Next(CritChanceDenominator) (0 이면 급소) → 데미지 난수 Next(101 − RandomMinPercent)
    ///
    /// 지금은 1:1 이다. 파티(3:3)는 BattleSide 구조만 준비돼 있고, 쓰러진 뒤 교체는 아직 없다.
    /// </summary>
    public sealed class BattleEngine
    {
        readonly BattleRules rules;
        readonly IRandom random;
        bool started;

        public BattleState State { get; }

        public BattleEngine(BattleState state, BattleRules rules, IRandom random)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>배틀 시작: 상대(NPC)가 먼저 나오고("야생의 ○○이(가) 나타났다!") 플레이어가 나온다("가라! ○○!").</summary>
        public IReadOnlyList<BattleEvent> Start()
        {
            if (started) throw new InvalidOperationException("battle already started");
            started = true;
            var events = new List<BattleEvent>();
            foreach (var side in new[] { BattleState.Npc, BattleState.Player })
            {
                var b = State.Sides[side].Active;
                events.Add(new PokemonSentOut(side, b.Species.Id, b.Hp, b.MaxHp));
            }
            return events;
        }

        /// <summary>한 턴 진행. 스피드 순으로 양쪽이 행동하고, 한쪽이 전멸하면 그 자리에서 끝난다.</summary>
        public IReadOnlyList<BattleEvent> PlayTurn(BattleAction playerAction, BattleAction npcAction)
        {
            if (!started) throw new InvalidOperationException("call Start() first");
            if (State.IsOver) throw new InvalidOperationException("battle is over");
            var actions = new[]
            {
                Validate(BattleState.Player, playerAction),
                Validate(BattleState.Npc, npcAction),
            };

            var events = new List<BattleEvent>();
            State.Turn++;
            events.Add(new TurnStarted(State.Turn));

            foreach (var side in TurnOrder())
            {
                Act(side, actions[side], events);
                if (State.IsOver) break;
            }
            return events;
        }

        /// <summary>
        /// 진 쪽(side)을 다시 일으킨다: HP 가득, 주는 피해 ×powerBoost, 승패를 지워 배틀이 이어진다 (데모 응원 부활, PROJECT.md 5.3).
        /// side 가 방금 져서 배틀이 끝났을 때만 부를 수 있다. 몇 번 허용할지는 호출자(BattleSession)가 정한다.
        /// </summary>
        public IReadOnlyList<BattleEvent> Revive(int side, double powerBoost)
        {
            if (!State.IsOver || State.Winner != BattleState.Opponent(side)) throw new InvalidOperationException("only the side that just lost can be revived");
            if (powerBoost <= 0) throw new ArgumentOutOfRangeException(nameof(powerBoost));
            var b = State.Sides[side].Active;
            b.Revive(powerBoost);
            State.Winner = null;
            return new BattleEvent[] { new Revived(side, b.Hp, b.MaxHp) };
        }

        /// <summary>
        /// 방금 이긴 쪽(side)을 바깥에서 쓰러뜨려 승패를 뒤집는다 (트레이너가 처리한다, PROJECT.md 5.3): HP 0, 상대가 승자.
        /// side 가 이겨서 배틀이 끝났을 때만 부를 수 있다.
        /// </summary>
        public IReadOnlyList<BattleEvent> KnockOut(int side)
        {
            if (!State.IsOver || State.Winner != side) throw new InvalidOperationException("only the side that just won can be knocked out");
            State.Sides[side].Active.TakeDamage(int.MaxValue);
            State.Winner = BattleState.Opponent(side);
            return new BattleEvent[] { new Fainted(side), new BattleEnded(BattleState.Opponent(side)) };
        }

        UseMove Validate(int side, BattleAction action)
        {
            if (action == null) throw new ArgumentNullException(side == BattleState.Player ? "playerAction" : "npcAction");
            if (!(action is UseMove use)) throw new ArgumentException($"unsupported action {action.GetType().Name}");
            var moves = State.Sides[side].Active.Moves;
            if (use.MoveIndex < 0 || use.MoveIndex >= moves.Count)
                throw new ArgumentOutOfRangeException(nameof(action), $"move index {use.MoveIndex} (side {side} has {moves.Count})");
            return use;
        }

        int[] TurnOrder()
        {
            var p = State.Sides[BattleState.Player].Active.Stats.Speed;
            var n = State.Sides[BattleState.Npc].Active.Stats.Speed;
            bool playerFirst = p != n ? p > n : random.Next(2) == 0;
            return playerFirst
                ? new[] { BattleState.Player, BattleState.Npc }
                : new[] { BattleState.Npc, BattleState.Player };
        }

        void Act(int side, UseMove action, List<BattleEvent> events)
        {
            var actor = State.Sides[side].Active;
            if (actor.Fainted) return;
            var targetSide = BattleState.Opponent(side);
            var target = State.Sides[targetSide].Active;
            var move = actor.Moves[action.MoveIndex];

            events.Add(new MoveUsed(side, move.Id));

            if (move.Accuracy < 100 && random.Next(100) >= move.Accuracy)
            {
                events.Add(new MoveMissed(side, move.Id));
                return;
            }

            bool critical = random.Next(rules.CritChanceDenominator) == 0;
            int roll = rules.RandomMinPercent + random.Next(101 - rules.RandomMinPercent);
            var result = DamageCalculator.Calculate(actor, target, move, critical, roll, rules);
            int dealt = target.TakeDamage(result.Amount);
            bool showCritical = result.Critical && result.Effectiveness > 0; // 효과가 없으면 급소 문구도 없다
            events.Add(new DamageDealt(targetSide, dealt, target.Hp, result.Effectiveness, showCritical));

            if (!target.Fainted) return;
            events.Add(new Fainted(targetSide));
            if (State.Sides[targetSide].AllFainted)
            {
                State.Winner = side;
                events.Add(new BattleEnded(side));
            }
        }
    }
}
