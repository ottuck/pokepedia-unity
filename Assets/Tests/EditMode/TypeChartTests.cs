using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pokepedia.Domain;
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
        static readonly object[] NonNeutral =
        {
            // Normal
            new object[] { T.Normal, T.Rock, 0.5 },
            new object[] { T.Normal, T.Ghost, 0.0 },
            new object[] { T.Normal, T.Steel, 0.5 },
            // Fire
            new object[] { T.Fire, T.Fire, 0.5 },
            new object[] { T.Fire, T.Water, 0.5 },
            new object[] { T.Fire, T.Grass, 2.0 },
            new object[] { T.Fire, T.Ice, 2.0 },
            new object[] { T.Fire, T.Bug, 2.0 },
            new object[] { T.Fire, T.Rock, 0.5 },
            new object[] { T.Fire, T.Dragon, 0.5 },
            new object[] { T.Fire, T.Steel, 2.0 },
            // Water
            new object[] { T.Water, T.Fire, 2.0 },
            new object[] { T.Water, T.Water, 0.5 },
            new object[] { T.Water, T.Grass, 0.5 },
            new object[] { T.Water, T.Ground, 2.0 },
            new object[] { T.Water, T.Rock, 2.0 },
            new object[] { T.Water, T.Dragon, 0.5 },
            // Electric
            new object[] { T.Electric, T.Water, 2.0 },
            new object[] { T.Electric, T.Electric, 0.5 },
            new object[] { T.Electric, T.Grass, 0.5 },
            new object[] { T.Electric, T.Ground, 0.0 },
            new object[] { T.Electric, T.Flying, 2.0 },
            new object[] { T.Electric, T.Dragon, 0.5 },
            // Grass
            new object[] { T.Grass, T.Fire, 0.5 },
            new object[] { T.Grass, T.Water, 2.0 },
            new object[] { T.Grass, T.Grass, 0.5 },
            new object[] { T.Grass, T.Poison, 0.5 },
            new object[] { T.Grass, T.Ground, 2.0 },
            new object[] { T.Grass, T.Flying, 0.5 },
            new object[] { T.Grass, T.Bug, 0.5 },
            new object[] { T.Grass, T.Rock, 2.0 },
            new object[] { T.Grass, T.Dragon, 0.5 },
            new object[] { T.Grass, T.Steel, 0.5 },
            // Ice
            new object[] { T.Ice, T.Fire, 0.5 },
            new object[] { T.Ice, T.Water, 0.5 },
            new object[] { T.Ice, T.Grass, 2.0 },
            new object[] { T.Ice, T.Ice, 0.5 },
            new object[] { T.Ice, T.Ground, 2.0 },
            new object[] { T.Ice, T.Flying, 2.0 },
            new object[] { T.Ice, T.Dragon, 2.0 },
            new object[] { T.Ice, T.Steel, 0.5 },
            // Fighting
            new object[] { T.Fighting, T.Normal, 2.0 },
            new object[] { T.Fighting, T.Ice, 2.0 },
            new object[] { T.Fighting, T.Poison, 0.5 },
            new object[] { T.Fighting, T.Flying, 0.5 },
            new object[] { T.Fighting, T.Psychic, 0.5 },
            new object[] { T.Fighting, T.Bug, 0.5 },
            new object[] { T.Fighting, T.Rock, 2.0 },
            new object[] { T.Fighting, T.Ghost, 0.0 },
            new object[] { T.Fighting, T.Dark, 2.0 },
            new object[] { T.Fighting, T.Steel, 2.0 },
            new object[] { T.Fighting, T.Fairy, 0.5 },
            // Poison
            new object[] { T.Poison, T.Grass, 2.0 },
            new object[] { T.Poison, T.Poison, 0.5 },
            new object[] { T.Poison, T.Ground, 0.5 },
            new object[] { T.Poison, T.Rock, 0.5 },
            new object[] { T.Poison, T.Ghost, 0.5 },
            new object[] { T.Poison, T.Steel, 0.0 },
            new object[] { T.Poison, T.Fairy, 2.0 },
            // Ground
            new object[] { T.Ground, T.Fire, 2.0 },
            new object[] { T.Ground, T.Electric, 2.0 },
            new object[] { T.Ground, T.Grass, 0.5 },
            new object[] { T.Ground, T.Poison, 2.0 },
            new object[] { T.Ground, T.Flying, 0.0 },
            new object[] { T.Ground, T.Bug, 0.5 },
            new object[] { T.Ground, T.Rock, 2.0 },
            new object[] { T.Ground, T.Steel, 2.0 },
            // Flying
            new object[] { T.Flying, T.Electric, 0.5 },
            new object[] { T.Flying, T.Grass, 2.0 },
            new object[] { T.Flying, T.Fighting, 2.0 },
            new object[] { T.Flying, T.Bug, 2.0 },
            new object[] { T.Flying, T.Rock, 0.5 },
            new object[] { T.Flying, T.Steel, 0.5 },
            // Psychic
            new object[] { T.Psychic, T.Fighting, 2.0 },
            new object[] { T.Psychic, T.Poison, 2.0 },
            new object[] { T.Psychic, T.Psychic, 0.5 },
            new object[] { T.Psychic, T.Dark, 0.0 },
            new object[] { T.Psychic, T.Steel, 0.5 },
            // Bug
            new object[] { T.Bug, T.Fire, 0.5 },
            new object[] { T.Bug, T.Grass, 2.0 },
            new object[] { T.Bug, T.Fighting, 0.5 },
            new object[] { T.Bug, T.Poison, 0.5 },
            new object[] { T.Bug, T.Flying, 0.5 },
            new object[] { T.Bug, T.Psychic, 2.0 },
            new object[] { T.Bug, T.Ghost, 0.5 },
            new object[] { T.Bug, T.Dark, 2.0 },
            new object[] { T.Bug, T.Steel, 0.5 },
            new object[] { T.Bug, T.Fairy, 0.5 },
            // Rock
            new object[] { T.Rock, T.Fire, 2.0 },
            new object[] { T.Rock, T.Ice, 2.0 },
            new object[] { T.Rock, T.Fighting, 0.5 },
            new object[] { T.Rock, T.Ground, 0.5 },
            new object[] { T.Rock, T.Flying, 2.0 },
            new object[] { T.Rock, T.Bug, 2.0 },
            new object[] { T.Rock, T.Steel, 0.5 },
            // Ghost
            new object[] { T.Ghost, T.Normal, 0.0 },
            new object[] { T.Ghost, T.Psychic, 2.0 },
            new object[] { T.Ghost, T.Ghost, 2.0 },
            new object[] { T.Ghost, T.Dark, 0.5 },
            // Dragon
            new object[] { T.Dragon, T.Dragon, 2.0 },
            new object[] { T.Dragon, T.Steel, 0.5 },
            new object[] { T.Dragon, T.Fairy, 0.0 },
            // Dark
            new object[] { T.Dark, T.Fighting, 0.5 },
            new object[] { T.Dark, T.Psychic, 2.0 },
            new object[] { T.Dark, T.Ghost, 2.0 },
            new object[] { T.Dark, T.Dark, 0.5 },
            new object[] { T.Dark, T.Fairy, 0.5 },
            // Steel
            new object[] { T.Steel, T.Fire, 0.5 },
            new object[] { T.Steel, T.Water, 0.5 },
            new object[] { T.Steel, T.Electric, 0.5 },
            new object[] { T.Steel, T.Ice, 2.0 },
            new object[] { T.Steel, T.Rock, 2.0 },
            new object[] { T.Steel, T.Steel, 0.5 },
            new object[] { T.Steel, T.Fairy, 2.0 },
            // Fairy
            new object[] { T.Fairy, T.Fire, 0.5 },
            new object[] { T.Fairy, T.Fighting, 2.0 },
            new object[] { T.Fairy, T.Poison, 0.5 },
            new object[] { T.Fairy, T.Dragon, 2.0 },
            new object[] { T.Fairy, T.Dark, 2.0 },
            new object[] { T.Fairy, T.Steel, 0.5 },
        };

        static IEnumerable<T> AllTypes => (T[])Enum.GetValues(typeof(T));

        static Dictionary<(T, T), double> NonNeutralMap()
        {
            var map = new Dictionary<(T, T), double>();
            foreach (object[] row in NonNeutral)
                map.Add(((T)row[0], (T)row[1]), (double)row[2]); // 중복 칸이 있으면 여기서 예외
            return map;
        }

        // ---- (a) 1이 아닌 칸 ----

        [TestCaseSource(nameof(NonNeutral))]
        public void SingleType_NonNeutralCell(T attack, T defend, double expected)
        {
            Assert.AreEqual(expected, TypeChart.Effectiveness(attack, defend), Eps);
        }

        [Test]
        public void NonNeutralTable_Has120UniqueCells()
        {
            // 6세대 이후 표에서 1이 아닌 칸은 120개(2배 51, 0.5배 61, 0배 8).
            var map = NonNeutralMap();
            Assert.AreEqual(120, map.Count);
            int super = 0, notVery = 0, none = 0;
            foreach (var v in map.Values)
            {
                if (v == 2.0) super++;
                else if (v == 0.5) notVery++;
                else if (v == 0.0) none++;
                else Assert.Fail($"unexpected multiplier {v}");
            }
            Assert.AreEqual(51, super);
            Assert.AreEqual(61, notVery);
            Assert.AreEqual(8, none);
        }

        // ---- (b) 나머지 칸은 모두 1 (324칸 전체 커버) ----

        [Test]
        public void EnumHas18Types()
        {
            Assert.AreEqual(18, Enum.GetValues(typeof(T)).Length);
        }

        [Test]
        public void AllOtherCells_AreNeutral()
        {
            var map = NonNeutralMap();
            int checkedCells = 0;
            foreach (var a in AllTypes)
            foreach (var d in AllTypes)
            {
                double expected = map.TryGetValue((a, d), out var v) ? v : 1.0;
                Assert.AreEqual(expected, TypeChart.Effectiveness(a, d), Eps, $"{a} -> {d}");
                checkedCells++;
            }
            Assert.AreEqual(324, checkedCells);
        }

        // ---- (c) 면역(0배) ----

        [TestCase(T.Normal, T.Ghost)]
        [TestCase(T.Fighting, T.Ghost)]
        [TestCase(T.Ghost, T.Normal)]
        [TestCase(T.Electric, T.Ground)]
        [TestCase(T.Ground, T.Flying)]
        [TestCase(T.Poison, T.Steel)]
        [TestCase(T.Psychic, T.Dark)]
        [TestCase(T.Dragon, T.Fairy)]
        public void Immunity_IsZero(T attack, T defend)
        {
            Assert.AreEqual(0.0, TypeChart.Effectiveness(attack, defend));
        }

        // ---- (d) 이중 타입은 곱한다 ----

        [TestCase(T.Electric, T.Water, T.Flying, 4.0)]   // 2 × 2
        [TestCase(T.Ground, T.Electric, T.Flying, 0.0)]  // 2 × 0
        [TestCase(T.Ground, T.Flying, T.Electric, 0.0)]  // 순서가 바뀌어도 같다
        [TestCase(T.Grass, T.Fire, T.Flying, 0.25)]      // 0.5 × 0.5
        [TestCase(T.Fire, T.Bug, T.Steel, 4.0)]          // 2 × 2
        [TestCase(T.Ice, T.Dragon, T.Flying, 4.0)]       // 2 × 2
        [TestCase(T.Fire, T.Water, T.Rock, 0.25)]        // 0.5 × 0.5
        [TestCase(T.Fighting, T.Normal, T.Flying, 1.0)]  // 2 × 0.5
        [TestCase(T.Water, T.Water, T.Ground, 1.0)]      // 0.5 × 2
        [TestCase(T.Normal, T.Ghost, T.Dark, 0.0)]       // 0 × 1
        public void DualType_Multiplies(T attack, T first, T second, double expected)
        {
            Assert.AreEqual(expected, TypeChart.Effectiveness(attack, new[] { first, second }), Eps);
        }

        [Test]
        public void SingleTypeList_MatchesSingleOverload()
        {
            foreach (var a in AllTypes)
            foreach (var d in AllTypes)
                Assert.AreEqual(TypeChart.Effectiveness(a, d), TypeChart.Effectiveness(a, new[] { d }), Eps, $"{a} -> [{d}]");
        }

        [Test]
        public void DualType_IsProductOfEachCell_ForAllCombinations()
        {
            foreach (var a in AllTypes)
            foreach (var d1 in AllTypes)
            foreach (var d2 in AllTypes)
            {
                if (d1 == d2) continue;
                double expected = TypeChart.Effectiveness(a, d1) * TypeChart.Effectiveness(a, d2);
                Assert.AreEqual(expected, TypeChart.Effectiveness(a, new[] { d1, d2 }), Eps, $"{a} -> [{d1}, {d2}]");
            }
        }

        // ---- (e) 로스터 관련 예 (이름은 주석에만, 엔진은 타입만 안다) ----

        [TestCase(T.Water, new[] { T.Fire, T.Flying }, 2.0)]    // 물 → 리자몽(불꽃/비행)
        [TestCase(T.Rock, new[] { T.Fire, T.Flying }, 4.0)]     // 바위 → 리자몽
        [TestCase(T.Electric, new[] { T.Fire, T.Flying }, 2.0)] // 전기 → 리자몽
        [TestCase(T.Ground, new[] { T.Fire, T.Flying }, 0.0)]   // 땅 → 리자몽 (비행 면역)
        [TestCase(T.Grass, new[] { T.Fire, T.Flying }, 0.25)]   // 풀 → 리자몽
        [TestCase(T.Electric, new[] { T.Water }, 2.0)]          // 전기 → 거북왕(물)
        [TestCase(T.Water, new[] { T.Water }, 0.5)]             // 물 → 거북왕
        [TestCase(T.Electric, new[] { T.Electric }, 0.5)]       // 전기 → 피카츄(전기)
        [TestCase(T.Ground, new[] { T.Electric }, 2.0)]         // 땅 → 피카츄
        [TestCase(T.Psychic, new[] { T.Ghost, T.Poison }, 2.0)] // 에스퍼 → 팬텀(고스트/독)
        [TestCase(T.Ghost, new[] { T.Ghost, T.Poison }, 2.0)]   // 고스트 → 팬텀
        [TestCase(T.Normal, new[] { T.Ghost, T.Poison }, 0.0)]  // 노말 → 팬텀
        [TestCase(T.Fighting, new[] { T.Ghost, T.Poison }, 0.0)] // 격투 → 팬텀
        [TestCase(T.Bug, new[] { T.Ghost, T.Poison }, 0.25)]    // 벌레 → 팬텀
        public void RosterExamples(T attack, T[] defenderTypes, double expected)
        {
            Assert.AreEqual(expected, TypeChart.Effectiveness(attack, defenderTypes), Eps);
        }

        // ---- 잘못된 입력 ----

        [Test]
        public void NoTypes_Throws()
        {
            Assert.Throws<ArgumentException>(() => TypeChart.Effectiveness(T.Fire, Array.Empty<T>()));
        }

        [Test]
        public void ThreeTypes_Throws()
        {
            Assert.Throws<ArgumentException>(() => TypeChart.Effectiveness(T.Fire, new[] { T.Grass, T.Bug, T.Steel }));
        }

        [Test]
        public void NullTypes_ThrowsArgumentException()
        {
            Assert.That(() => TypeChart.Effectiveness(T.Fire, (IReadOnlyList<T>)null), Throws.InstanceOf<ArgumentException>());
        }
    }
}
