using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain.Battle;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 배틀 이벤트 → 메시지 키·인자 (PROJECT.md 5.3 "턴 처리 메시지", 7.3), 그리고 그 키가 실제 로케일 파일 세 개에 모두 있는지.
    /// </summary>
    public class BattleMessagesTests
    {
        const int PlayerSpecies = 25;
        const int NpcSpecies = 9;

        static readonly BattleNames Names = new BattleNames(id => "S" + id, id => "M:" + id);

        static int SpeciesBySide(int side) => side == BattleState.Player ? PlayerSpecies : NpcSpecies;

        static IReadOnlyList<MessageLine> For(BattleEvent e) => BattleMessages.For(e, Names, SpeciesBySide);

        static void AssertLines(BattleEvent e, params (string key, string[] args)[] expected)
        {
            var actual = For(e);
            Assert.AreEqual(expected.Length, actual.Count,
                e + " → " + string.Join(" | ", actual.Select(l => l.Key + "(" + string.Join(",", l.Args) + ")")));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].key, actual[i].Key, $"{e} line {i} key");
                CollectionAssert.AreEqual(expected[i].args, actual[i].Args, $"{e} line {i} args");
            }
        }

        static (string, string[]) L(string key, params string[] args) => (key, args);

        [Test]
        public void SentOutAndMoveUsed_NameTheSide()
        {
            // 등장 이벤트는 종 id 를 직접 가진다 (교체 확장 대비, speciesIdBySide 보다 이벤트를 믿는다).
            AssertLines(new PokemonSentOut(BattleState.Npc, 150, 100, 100), L(BattleTextKeys.WildAppeared, "S150"));
            AssertLines(new PokemonSentOut(BattleState.Player, 25, 90, 90), L(BattleTextKeys.PlayerSentOut, "S25"));
            AssertLines(new MoveUsed(BattleState.Player, "thunderbolt"), L(BattleTextKeys.PlayerMove, "S25", "M:thunderbolt"));
            AssertLines(new MoveUsed(BattleState.Npc, "hydro_pump"), L(BattleTextKeys.WildMove, "S9", "M:hydro_pump"));
            AssertLines(new MoveMissed(BattleState.Player, "thunderbolt"), L(BattleTextKeys.Missed));
        }

        [Test]
        public void Damage_CriticalComesBeforeEffectiveness()
        {
            AssertLines(new DamageDealt(BattleState.Npc, 20, 80, 1.0, false));
            AssertLines(new DamageDealt(BattleState.Npc, 30, 70, 1.0, true), L(BattleTextKeys.CriticalHit));
            AssertLines(new DamageDealt(BattleState.Npc, 60, 40, 4.0, true), L(BattleTextKeys.CriticalHit), L(BattleTextKeys.SuperEffective));
            AssertLines(new DamageDealt(BattleState.Player, 10, 90, 0.25, false), L(BattleTextKeys.NotVeryEffective));
            AssertLines(new DamageDealt(BattleState.Npc, 0, 100, 0.0, false), L(BattleTextKeys.NoEffect));
        }

        [Test]
        public void FaintedAndEnded()
        {
            AssertLines(new Fainted(BattleState.Player), L(BattleTextKeys.PlayerFainted, "S25"));
            AssertLines(new Fainted(BattleState.Npc), L(BattleTextKeys.WildFainted, "S9"));
            AssertLines(new BattleEnded(BattleState.Player), L(BattleTextKeys.Won));
            AssertLines(new BattleEnded(BattleState.Npc), L(BattleTextKeys.Lost));
        }

        // ---------- 키 목록 ----------

        /// <summary>모든 메시지 키를 한 번씩 내는 표본. 키 → 인자 수를 여기서 뽑는다.</summary>
        static Dictionary<string, int> ArgCountByKey()
        {
            var events = new BattleEvent[]
            {
                new PokemonSentOut(BattleState.Npc, 9, 1, 1), new PokemonSentOut(BattleState.Player, 25, 1, 1),
                new MoveUsed(BattleState.Player, "a"), new MoveUsed(BattleState.Npc, "b"), new MoveMissed(0, "a"),
                new DamageDealt(1, 1, 1, 2.0, true), new DamageDealt(1, 1, 1, 0.5, false), new DamageDealt(1, 0, 1, 0, false),
                new Fainted(0), new Fainted(1), new BattleEnded(0), new BattleEnded(1),
            };
            var lines = events.SelectMany(For)
                .Concat(new[] { BattleMessages.ForRun(RunOutcome.Escaped), BattleMessages.ForRun(RunOutcome.CannotEscape) });
            var map = new Dictionary<string, int>();
            foreach (var line in lines)
            {
                if (map.TryGetValue(line.Key, out var n)) Assert.AreEqual(n, line.Args.Count, $"{line.Key}: arg count must not vary");
                map[line.Key] = line.Args.Count;
            }
            return map;
        }

        [Test]
        public void MessageKeys_ListsExactlyTheKeysTheMessagesUse()
            => CollectionAssert.AreEquivalent(BattleTextKeys.MessageKeys, ArgCountByKey().Keys);

        // ---------- 실제 로케일 파일 ----------

        static readonly string[] Locales = { "ko", "en", "ja" };

        static LocaleTable Locale(string lang)
        {
            var starts = new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory, AppContext.BaseDirectory };
            foreach (var start in starts)
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var file = Path.Combine(dir.FullName, "Assets", "Content", "Locale", lang + ".json");
                    if (File.Exists(file)) return LocaleTable.Parse(File.ReadAllText(file));
                }
            throw new FileNotFoundException("Assets/Content/Locale/" + lang + ".json not found");
        }

        [Test]
        public void LocaleStrings_ExistAndUseEveryArgumentAndNoOther()
        {
            var counts = ArgCountByKey();
            foreach (var k in BattleTextKeys.UiKeys) counts[k] = k == BattleTextKeys.PromptWhatToDo ? 1 : 0; // "{0}은(는) 무엇을 할까?"
            CollectionAssert.AreEquivalent(BattleTextKeys.All, counts.Keys);
            foreach (var lang in Locales)
            {
                var table = Locale(lang);
                foreach (var pair in counts)
                {
                    Assert.IsTrue(table.Has(pair.Key) && !string.IsNullOrWhiteSpace(table.Get(pair.Key)), $"{lang}.json is missing {pair.Key}");
                    var text = table.Get(pair.Key);
                    for (int i = 0; i < pair.Value; i++)
                        StringAssert.Contains("{" + i + "}", text, $"{lang} {pair.Key}");
                    StringAssert.DoesNotContain("{" + pair.Value + "}", text, $"{lang} {pair.Key}");
                }
            }
        }
    }
}
