using System;
using System.Collections.Generic;
using System.Linq;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Domain.Battle
{
    /// <summary>
    /// 배틀 중인 포켓몬 한 마리. 종(Species)과 달리 현재 HP 같은 상태를 가진다.
    /// </summary>
    public sealed class Battler
    {
        public Species Species { get; }
        /// <summary>레벨 50 환산 실제 능력치</summary>
        public StatBlock Stats { get; }
        public IReadOnlyList<Move> Moves { get; }
        public int Hp { get; private set; }
        public int MaxHp => Stats.Hp;
        public bool Fainted => Hp <= 0;
        public IReadOnlyList<PokemonType> Types => Species.Types;
        /// <summary>주는 피해에 곱하는 배율 (응원 부활 버프, 기본 1)</summary>
        public double PowerBoost { get; private set; } = 1.0;

        /// <summary>테스트용: 실제 능력치를 직접 지정한다.</summary>
        public Battler(Species species, StatBlock stats, IReadOnlyList<Move> moves)
        {
            if (moves == null || moves.Count == 0) throw new ArgumentException("at least one move", nameof(moves));
            Species = species;
            Stats = stats;
            Moves = moves.ToArray();
            Hp = stats.Hp;
        }

        /// <summary>종족값에서 능력치를 계산해 만든다.</summary>
        public static Battler Create(Species species, IReadOnlyList<Move> moves, BattleRules rules)
            => new Battler(species, StatCalculator.Compute(species.BaseStats, rules), moves);

        /// <summary>쓰러진 뒤 HP 를 가득 채우고 피해 배율을 건다 (BattleEngine.Revive)</summary>
        internal void Revive(double powerBoost)
        {
            Hp = MaxHp;
            PowerBoost = powerBoost;
        }

        /// <summary>HP 를 깎고 실제로 깎인 양을 돌려준다 (0 미만으로 내려가지 않음).</summary>
        internal int TakeDamage(int amount)
        {
            var dealt = Math.Min(Hp, Math.Max(0, amount));
            Hp -= dealt;
            return dealt;
        }
    }
}
