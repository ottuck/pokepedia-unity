using System;
using System.Collections.Generic;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;

namespace Pokepedia.Tests
{
    /// <summary>
    /// Domain 테스트 전용 가짜 종족·기술(PROJECT.md 7.8). 실제 JSON 데이터에 의존하지 않는다.
    /// </summary>
    public static class TestMon
    {
        public static Species Species(int id, params PokemonType[] types)
            => new Species(id, types, new StatBlock(80, 80, 80, 80, 80, 80));

        public static StatBlock Stats(int hp = 100, int atk = 50, int def = 50, int spa = 50, int spd = 50, int spe = 50)
            => new StatBlock(hp, atk, def, spa, spd, spe);

        /// <summary>능력치를 직접 지정한 Battler</summary>
        public static Battler Battler(StatBlock stats, PokemonType type, params Move[] moves)
            => new Battler(Species(1, type), stats, moves.Length > 0 ? moves : new[] { TestMove.Tackle });

        public static Battler Battler(StatBlock stats, PokemonType[] types, params Move[] moves)
            => new Battler(new Species(1, types, new StatBlock(80, 80, 80, 80, 80, 80)), stats, moves.Length > 0 ? moves : new[] { TestMove.Tackle });
    }

    public static class TestMove
    {
        public static readonly Move Tackle = new("test_tackle", PokemonType.Normal, MoveCategory.Physical, 40, 100);

        public static Move Of(PokemonType type, int power = 40, int accuracy = 100, MoveCategory category = MoveCategory.Special, string id = null)
            => new(id ?? "test_" + type.ToString().ToLowerInvariant(), type, category, power, accuracy);
    }

    /// <summary>
    /// 정해 둔 값을 순서대로 돌려주는 IRandom 스텁. 값이 모자라거나 범위를 벗어나면 테스트를 실패시킨다.
    /// 엔진이 난수를 뽑는 순서를 테스트로 고정하는 역할도 한다.
    /// </summary>
    public sealed class ScriptedRandom : IRandom
    {
        readonly Queue<int> ints;
        readonly Queue<double> doubles;

        public ScriptedRandom(IEnumerable<int> ints, IEnumerable<double> doubles = null)
        {
            this.ints = new Queue<int>(ints);
            this.doubles = new Queue<double>(doubles ?? Array.Empty<double>());
        }

        public static ScriptedRandom Of(params int[] values) => new(values);

        public int Remaining => ints.Count;

        public int Next(int maxExclusive)
        {
            if (ints.Count == 0) throw new InvalidOperationException($"ScriptedRandom: no value left (Next({maxExclusive}))");
            var v = ints.Dequeue();
            if (v < 0 || v >= maxExclusive) throw new InvalidOperationException($"ScriptedRandom: {v} is out of range for Next({maxExclusive})");
            return v;
        }

        public double NextDouble()
        {
            if (doubles.Count == 0) throw new InvalidOperationException("ScriptedRandom: no double left");
            return doubles.Dequeue();
        }
    }
}
