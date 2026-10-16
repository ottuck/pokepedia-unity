using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pokepedia.Domain.Rules;
using T = Pokepedia.Domain.PokemonType;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 18×18 타입 상성표(6세대 이후) 검증. 기대값은 TypeChart.cs 주석의 출처(PokeAPI, pokemondb)에서 가져왔다.
    /// 구현의 표(double[,])와 달리 여기서는 "1이 아닌 칸 목록"으로 적어, 같은 실수를 두 번 하기 어렵게 한다.
    /// </summary>
    public class TypeChartTests
    {
        const double Eps = 1e-9;

        /// <summary>배수가 1이 아닌 모든 칸 (공격, 방어, 배수). 공격 타입별로 enum 순서대로 적었다.</summary>
        static readonly (T attack, T defend, double mult)[] NonNeutral =
        {
            (T.Normal, T.Rock, 0.5), (T.Normal, T.Ghost, 0.0), (T.Normal, T.Steel, 0.5),
            (T.Fire, T.Fire, 0.5), (T.Fire, T.Water, 0.5), (T.Fire, T.Grass, 2.0), (T.Fire, T.Ice, 2.0),
            (T.Fire, T.Bug, 2.0), (T.Fire, T.Rock, 0.5), (T.Fire, T.Dragon, 0.5), (T.Fire, T.Steel, 2.0),
            (T.Water, T.Fire, 2.0), (T.Water, T.Water, 0.5), (T.Water, T.Grass, 0.5), (T.Water, T.Ground, 2.0),
            (T.Water, T.Rock, 2.0), (T.Water, T.Dragon, 0.5),
            (T.Electric, T.Water, 2.0), (T.Electric, T.Electric, 0.5), (T.Electric, T.Grass, 0.5),
            (T.Electric, T.Ground, 0.0), (T.Electric, T.Flying, 2.0), (T.Electric, T.Dragon, 0.5),
            (T.Grass, T.Fire, 0.5), (T.Grass, T.Water, 2.0), (T.Grass, T.Grass, 0.5), (T.Grass, T.Poison, 0.5),
            (T.Grass, T.Ground, 2.0), (T.Grass, T.Flying, 0.5), (T.Grass, T.Bug, 0.5), (T.Grass, T.Rock, 2.0),
            (T.Grass, T.Dragon, 0.5), (T.Grass, T.Steel, 0.5),
            (T.Ice, T.Fire, 0.5), (T.Ice, T.Water, 0.5), (T.Ice, T.Grass, 2.0), (T.Ice, T.Ice, 0.5),
            (T.Ice, T.Ground, 2.0), (T.Ice, T.Flying, 2.0), (T.Ice, T.Dragon, 2.0), (T.Ice, T.Steel, 0.5),
            (T.Fighting, T.Normal, 2.0), (T.Fighting, T.Ice, 2.0), (T.Fighting, T.Poison, 0.5), (T.Fighting, T.Flying, 0.5),
            (T.Fighting, T.Psychic, 0.5), (T.Fighting, T.Bug, 0.5), (T.Fighting, T.Rock, 2.0), (T.Fighting, T.Ghost, 0.0),
            (T.Fighting, T.Dark, 2.0), (T.Fighting, T.Steel, 2.0), (T.Fighting, T.Fairy, 0.5),
            (T.Poison, T.Grass, 2.0), (T.Poison, T.Poison, 0.5), (T.Poison, T.Ground, 0.5), (T.Poison, T.Rock, 0.5),
            (T.Poison, T.Ghost, 0.5), (T.Poison, T.Steel, 0.0), (T.Poison, T.Fairy, 2.0),
            (T.Ground, T.Fire, 2.0), (T.Ground, T.Electric, 2.0), (T.Ground, T.Grass, 0.5), (T.Ground, T.Poison, 2.0),
            (T.Ground, T.Flying, 0.0), (T.Ground, T.Bug, 0.5), (T.Ground, T.Rock, 2.0), (T.Ground, T.Steel, 2.0),
            (T.Flying, T.Electric, 0.5), (T.Flying, T.Grass, 2.0), (T.Flying, T.Fighting, 2.0), (T.Flying, T.Bug, 2.0),
            (T.Flying, T.Rock, 0.5), (T.Flying, T.Steel, 0.5),
            (T.Psychic, T.Fighting, 2.0), (T.Psychic, T.Poison, 2.0), (T.Psychic, T.Psychic, 0.5),
            (T.Psychic, T.Dark, 0.0), (T.Psychic, T.Steel, 0.5),
            (T.Bug, T.Fire, 0.5), (T.Bug, T.Grass, 2.0), (T.Bug, T.Fighting, 0.5), (T.Bug, T.Poison, 0.5),
            (T.Bug, T.Flying, 0.5), (T.Bug, T.Psychic, 2.0), (T.Bug, T.Ghost, 0.5), (T.Bug, T.Dark, 2.0),
            (T.Bug, T.Steel, 0.5), (T.Bug, T.Fairy, 0.5),
            (T.Rock, T.Fire, 2.0), (T.Rock, T.Ice, 2.0), (T.Rock, T.Fighting, 0.5), (T.Rock, T.Ground, 0.5),
            (T.Rock, T.Flying, 2.0), (T.Rock, T.Bug, 2.0), (T.Rock, T.Steel, 0.5),
            (T.Ghost, T.Normal, 0.0), (T.Ghost, T.Psychic, 2.0), (T.Ghost, T.Ghost, 2.0), (T.Ghost, T.Dark, 0.5),
            (T.Dragon, T.Dragon, 2.0), (T.Dragon, T.Steel, 0.5), (T.Dragon, T.Fairy, 0.0),
            (T.Dark, T.Fighting, 0.5), (T.Dark, T.Psychic, 2.0), (T.Dark, T.Ghost, 2.0), (T.Dark, T.Dark, 0.5),
            (T.Dark, T.Fairy, 0.5),
            (T.Steel, T.Fire, 0.5), (T.Steel, T.Water, 0.5), (T.Steel, T.Electric, 0.5), (T.Steel, T.Ice, 2.0),
            (T.Steel, T.Rock, 2.0), (T.Steel, T.Steel, 0.5), (T.Steel, T.Fairy, 2.0),
            (T.Fairy, T.Fire, 0.5), (T.Fairy, T.Fighting, 2.0), (T.Fairy, T.Poison, 0.5), (T.Fairy, T.Dragon, 2.0),
            (T.Fairy, T.Dark, 2.0), (T.Fairy, T.Steel, 0.5),
        };

        [Test]
        public void EveryCell_MatchesSpec()
        {
            var map = new Dictionary<(T, T), double>();
            foreach (var (a, d, m) in NonNeutral) map.Add((a, d), m); // 중복 칸이 있으면 여기서 예외

            // 기대값 목록 자체의 오타 방지: 6세대 이후 1이 아닌 칸은 120개(2배 51, 0.5배 61, 0배 8).
            Assert.AreEqual(120, map.Count);
            Assert.AreEqual(51, CountOf(map, 2.0));
            Assert.AreEqual(61, CountOf(map, 0.5));
            Assert.AreEqual(8, CountOf(map, 0.0));

            var types = (T[])Enum.GetValues(typeof(T));
            Assert.AreEqual(18, types.Length);
            foreach (var a in types)
            foreach (var d in types)
                Assert.AreEqual(map.TryGetValue((a, d), out var v) ? v : 1.0, TypeChart.Effectiveness(a, d), Eps, $"{a} -> {d}");
        }

        static int CountOf(Dictionary<(T, T), double> map, double mult)
        {
            int n = 0;
            foreach (var v in map.Values) if (v == mult) n++;
            return n;
        }

        [TestCase(T.Electric, T.Water, T.Flying, 4.0)]   // 2 × 2
        [TestCase(T.Ground, T.Fire, T.Flying, 0.0)]      // 2 × 0
        [TestCase(T.Grass, T.Fire, T.Flying, 0.25)]      // 0.5 × 0.5
        [TestCase(T.Fighting, T.Normal, T.Flying, 1.0)]  // 2 × 0.5
        public void DualType_Multiplies(T attack, T first, T second, double expected)
        {
            Assert.AreEqual(expected, TypeChart.Effectiveness(attack, new[] { first, second }), Eps);
        }
    }
}
