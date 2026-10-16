using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

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

        static void AssertLines(IReadOnlyList<MessageLine> actual, params (string key, string[] args)[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count,
                "lines: " + string.Join(" | ", actual.Select(l => l.Key + "(" + string.Join(",", l.Args) + ")")));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].key, actual[i].Key, $"line {i} key");
                CollectionAssert.AreEqual(expected[i].args, actual[i].Args, $"line {i} args");
            }
        }

        static (string, string[]) L(string key, params string[] args) => (key, args);

        // ---------- 등장 ----------

        [Test]
        public void NpcSentOut_IsWildAppeared()
            => AssertLines(For(new PokemonSentOut(BattleState.Npc, 9, 100, 100)), L(BattleTextKeys.WildAppeared, "S9"));

        [Test]
        public void PlayerSentOut_IsGo()
            => AssertLines(For(new PokemonSentOut(BattleState.Player, 25, 90, 90)), L(BattleTextKeys.PlayerSentOut, "S25"));

        [Test]
        public void SentOut_UsesEventSpeciesId()
        {
            // 등장 이벤트는 종 id 를 직접 가진다 (교체 확장 대비, speciesIdBySide 보다 이벤트를 믿는다).
            AssertLines(For(new PokemonSentOut(BattleState.Npc, 150, 100, 100)), L(BattleTextKeys.WildAppeared, "S150"));
        }

        [Test]
        public void TurnStarted_HasNoLines()
            => CollectionAssert.IsEmpty(For(new TurnStarted(3)));

        // ---------- 기술 ----------

        [Test]
        public void PlayerMoveUsed_NamesUserAndMove()
            => AssertLines(For(new MoveUsed(BattleState.Player, "thunderbolt")), L(BattleTextKeys.PlayerMove, "S25", "M:thunderbolt"));

        [Test]
        public void NpcMoveUsed_IsMarkedWild()
            => AssertLines(For(new MoveUsed(BattleState.Npc, "hydro_pump")), L(BattleTextKeys.WildMove, "S9", "M:hydro_pump"));

        [Test]
        public void MoveMissed_IsOneLineWithoutArgs()
        {
            AssertLines(For(new MoveMissed(BattleState.Player, "thunderbolt")), L(BattleTextKeys.Missed));
            AssertLines(For(new MoveMissed(BattleState.Npc, "hydro_pump")), L(BattleTextKeys.Missed));
        }

        // ---------- 피해: 급소 → 상성 ----------

        [Test]
        public void NeutralDamage_HasNoLines()
            => CollectionAssert.IsEmpty(For(new DamageDealt(BattleState.Npc, 20, 80, 1.0, false)));

        [Test]
        public void CriticalNeutral_IsCriticalOnly()
            => AssertLines(For(new DamageDealt(BattleState.Npc, 30, 70, 1.0, true)), L(BattleTextKeys.CriticalHit));

        [TestCase(2.0)]
        [TestCase(4.0)]
        public void SuperEffective(double eff)
            => AssertLines(For(new DamageDealt(BattleState.Npc, 40, 60, eff, false)), L(BattleTextKeys.SuperEffective));

        [TestCase(0.5)]
        [TestCase(0.25)]
        public void NotVeryEffective(double eff)
            => AssertLines(For(new DamageDealt(BattleState.Player, 10, 90, eff, false)), L(BattleTextKeys.NotVeryEffective));

        [Test]
        public void NoEffect_WhenEffectivenessAndAmountAreZero()
            => AssertLines(For(new DamageDealt(BattleState.Npc, 0, 100, 0.0, false)), L(BattleTextKeys.NoEffect));

        [Test]
        public void CriticalComesBeforeEffectiveness()
        {
            AssertLines(For(new DamageDealt(BattleState.Npc, 60, 40, 2.0, true)), L(BattleTextKeys.CriticalHit), L(BattleTextKeys.SuperEffective));
            AssertLines(For(new DamageDealt(BattleState.Player, 15, 85, 0.5, true)), L(BattleTextKeys.CriticalHit), L(BattleTextKeys.NotVeryEffective));
        }

        // ---------- 기절·종료·도망 ----------

        [Test]
        public void PlayerFainted_NamesPlayerSpecies()
            => AssertLines(For(new Fainted(BattleState.Player)), L(BattleTextKeys.PlayerFainted, "S25"));

        [Test]
        public void NpcFainted_IsMarkedWild()
            => AssertLines(For(new Fainted(BattleState.Npc)), L(BattleTextKeys.WildFainted, "S9"));

        [Test]
        public void BattleEnded_WinOrLose()
        {
            AssertLines(For(new BattleEnded(BattleState.Player)), L(BattleTextKeys.Won));
            AssertLines(For(new BattleEnded(BattleState.Npc)), L(BattleTextKeys.Lost));
        }

        [Test]
        public void RunOutcome_Messages()
        {
            Assert.AreEqual(BattleTextKeys.Escaped, BattleMessages.ForRun(RunOutcome.Escaped).Key);
            Assert.AreEqual(BattleTextKeys.CannotEscape, BattleMessages.ForRun(RunOutcome.CannotEscape).Key);
            CollectionAssert.IsEmpty(BattleMessages.ForRun(RunOutcome.Escaped).Args);
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => BattleMessages.For(null, Names, SpeciesBySide));
            Assert.Throws<ArgumentNullException>(() => BattleMessages.For(new TurnStarted(1), null, SpeciesBySide));
            Assert.Throws<ArgumentNullException>(() => BattleMessages.For(new TurnStarted(1), Names, null));
        }

        // ---------- 키 목록 ----------

        /// <summary>모든 메시지 키를 한 번씩 내는 표본. 키 → 인자 수를 여기서 뽑는다.</summary>
        static IEnumerable<MessageLine> AllSampleLines()
        {
            var events = new BattleEvent[]
            {
                new PokemonSentOut(BattleState.Npc, 9, 1, 1), new PokemonSentOut(BattleState.Player, 25, 1, 1),
                new MoveUsed(BattleState.Player, "a"), new MoveUsed(BattleState.Npc, "b"), new MoveMissed(0, "a"),
                new DamageDealt(1, 1, 1, 2.0, true), new DamageDealt(1, 1, 1, 0.5, false), new DamageDealt(1, 0, 1, 0, false),
                new Fainted(0), new Fainted(1), new BattleEnded(0), new BattleEnded(1),
            };
            return events.SelectMany(For)
                .Concat(new[] { BattleMessages.ForRun(RunOutcome.Escaped), BattleMessages.ForRun(RunOutcome.CannotEscape) });
        }

        static Dictionary<string, int> ArgCountByKey()
        {
            var map = new Dictionary<string, int>();
            foreach (var line in AllSampleLines())
            {
                if (map.TryGetValue(line.Key, out var n)) Assert.AreEqual(n, line.Args.Count, $"{line.Key}: arg count must not vary");
                map[line.Key] = line.Args.Count;
            }
            return map;
        }

        [Test]
        public void MessageKeys_ListsExactlyTheKeysTheMessagesUse()
            => CollectionAssert.AreEquivalent(BattleTextKeys.MessageKeys, ArgCountByKey().Keys);

        [Test]
        public void UiKeys_IncludeMenusResultsMoveInfoAndAllTypes()
        {
            var ui = BattleTextKeys.UiKeys;
            foreach (var k in new[]
                     {
                         BattleTextKeys.MenuFight, BattleTextKeys.MenuRun, BattleTextKeys.ResultNextOpponent, BattleTextKeys.ResultRetry,
                         BattleTextKeys.ResultTitle, BattleTextKeys.InfoType, BattleTextKeys.InfoPower, BattleTextKeys.InfoAccuracy,
                     })
                CollectionAssert.Contains(ui, k);
            foreach (PokemonType t in Enum.GetValues(typeof(PokemonType)))
                CollectionAssert.Contains(ui, BattleTextKeys.TypeName(t));
            Assert.AreEqual("types.electric", BattleTextKeys.TypeName(PokemonType.Electric));
            CollectionAssert.AreEquivalent(BattleTextKeys.MessageKeys.Concat(ui), BattleTextKeys.All);
            CollectionAssert.AllItemsAreUnique(BattleTextKeys.All);
        }

        // ---------- 실제 로케일 파일 ----------

        static readonly string[] Locales = { "ko", "en", "ja" };

        static string contentDir;

        static string ContentDir => contentDir ??= FindContentDir();

        static string FindContentDir()
        {
            var starts = new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory, AppContext.BaseDirectory };
            foreach (var start in starts)
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Content");
                    if (Directory.Exists(Path.Combine(candidate, "Locale"))) return candidate;
                }
            throw new DirectoryNotFoundException("Assets/Content/Locale not found above: " + string.Join(", ", starts));
        }

        static LocaleTable Locale(string lang) => LocaleTable.Parse(File.ReadAllText(Path.Combine(ContentDir, "Locale", lang + ".json")));

        [Test]
        public void EveryBattleTextKey_ExistsInAllLocales()
        {
            foreach (var lang in Locales)
            {
                var table = Locale(lang);
                var missing = BattleTextKeys.All.Where(k => !table.Has(k) || string.IsNullOrWhiteSpace(table.Get(k))).ToList();
                CollectionAssert.IsEmpty(missing, $"{lang}.json is missing: {string.Join(", ", missing)}");
            }
        }

        [Test]
        public void LocaleStrings_UseEveryArgumentAndNoOther()
        {
            var counts = ArgCountByKey();
            foreach (var k in BattleTextKeys.UiKeys) counts[k] = 0;
            foreach (var lang in Locales)
            {
                var table = Locale(lang);
                foreach (var pair in counts)
                {
                    var text = table.Get(pair.Key);
                    for (int i = 0; i < pair.Value; i++)
                        StringAssert.Contains("{" + i + "}", text, $"{lang} {pair.Key}");
                    StringAssert.DoesNotContain("{" + pair.Value + "}", text, $"{lang} {pair.Key}");
                    var args = Enumerable.Range(0, pair.Value).Select(i => (object)("x" + i)).ToArray();
                    Assert.DoesNotThrow(() => table.Format(pair.Key, args), $"{lang} {pair.Key}");
                }
            }
        }

        [Test]
        public void KoreanStrings_MatchProjectSpec()
        {
            // PROJECT.md 5.3 의 문구. 조사는 "이(가)", "은(는)" 병기.
            var ko = Locale("ko");
            Assert.AreEqual("야생의 거북왕이(가) 나타났다!", ko.Format(BattleTextKeys.WildAppeared, "거북왕"));
            Assert.AreEqual("가라! 피카츄!", ko.Format(BattleTextKeys.PlayerSentOut, "피카츄"));
            Assert.AreEqual("피카츄의 10만볼트!", ko.Format(BattleTextKeys.PlayerMove, "피카츄", "10만볼트"));
            Assert.AreEqual("야생의 거북왕의 하이드로펌프!", ko.Format(BattleTextKeys.WildMove, "거북왕", "하이드로펌프"));
            Assert.AreEqual("공격은 빗나갔다!", ko.Get(BattleTextKeys.Missed));
            Assert.AreEqual("급소에 맞았다!", ko.Get(BattleTextKeys.CriticalHit));
            Assert.AreEqual("효과가 굉장했다!", ko.Get(BattleTextKeys.SuperEffective));
            Assert.AreEqual("효과가 별로인 듯하다…", ko.Get(BattleTextKeys.NotVeryEffective));
            Assert.AreEqual("효과가 없는 것 같다…", ko.Get(BattleTextKeys.NoEffect));
            Assert.AreEqual("피카츄은(는) 쓰러졌다!", ko.Format(BattleTextKeys.PlayerFainted, "피카츄"));
            Assert.AreEqual("야생의 거북왕은(는) 쓰러졌다!", ko.Format(BattleTextKeys.WildFainted, "거북왕"));
            Assert.AreEqual("무사히 도망쳤다!", ko.Get(BattleTextKeys.Escaped));
            Assert.AreEqual("도망칠 수 없다!", ko.Get(BattleTextKeys.CannotEscape));
            Assert.AreEqual("싸우다", ko.Get(BattleTextKeys.MenuFight));
            Assert.AreEqual("도망", ko.Get(BattleTextKeys.MenuRun));
            Assert.AreEqual("다음 상대와 싸운다", ko.Get(BattleTextKeys.ResultNextOpponent));
            Assert.AreEqual("다시 도전", ko.Get(BattleTextKeys.ResultRetry));
            Assert.AreEqual("처음으로", ko.Get(BattleTextKeys.ResultTitle));
        }

        [Test]
        public void TypeNames_AreDistinctPerLocale()
        {
            foreach (var lang in Locales)
            {
                var table = Locale(lang);
                var names = Enum.GetValues(typeof(PokemonType)).Cast<PokemonType>().Select(t => table.Get(BattleTextKeys.TypeName(t))).ToList();
                CollectionAssert.AllItemsAreUnique(names, lang);
            }
        }

        [Test]
        public void RealDemoBattle_EveryEventFormatsInEveryLocale()
        {
            // 실제 데이터로 Demo 한 판(피카츄 vs 거북왕)을 끝까지 돌려, 모든 줄이 세 로케일에서 키 누락 없이 문장이 되는지 본다.
            var dataDir = Path.Combine(ContentDir, "Data");
            var catalog = ContentLoader.Load(
                File.ReadAllText(Path.Combine(dataDir, "species.json")),
                File.ReadAllText(Path.Combine(dataDir, "moves.json")),
                File.ReadAllText(Path.Combine(dataDir, "roster.json")));
            var session = new BattleSession(catalog, BattleRules.Default, new SeededRandom(20261016), BattleMode.Demo, 25, () => 9);
            var events = new List<BattleEvent>(session.Start());
            for (int i = 0; !session.State.IsOver; i++)
            {
                Assert.That(i, Is.LessThan(100));
                events.AddRange(session.UseMove(session.Player.Moves.Count - 1));
            }

            foreach (var lang in Locales)
            {
                var table = Locale(lang);
                var names = new BattleNames(id => table.Get(catalog.GetSpecies(id).NameKey), id => table.Get(catalog.GetMove(id).NameKey));
                int Side(int side) => side == BattleState.Player ? session.Player.Species.Id : session.Npc.Species.Id;
                var text = events.SelectMany(e => BattleMessages.For(e, names, Side))
                    .Select(l => table.Format(l.Key, l.Args.Cast<object>().ToArray())).ToList();
                Assert.That(text.Count, Is.GreaterThan(4));
                foreach (var t in text)
                {
                    StringAssert.DoesNotContain("battle.", t, lang);
                    StringAssert.DoesNotContain("species.", t, lang);
                    StringAssert.DoesNotContain("move.", t, lang);
                }
                TestContext.WriteLine($"[{lang}] " + string.Join(" / ", text));
            }
        }
    }
}
