using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pokepedia.Content;
using Pokepedia.Domain;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 저장소의 실제 Assets/Content/Data/*.json 과 Locale 을 검증한다 (PROJECT.md 7.8).
    /// Unity Test Runner(작업 폴더 = 프로젝트 루트)와 dotnet test(테스트 dll 폴더) 양쪽에서 동작하도록
    /// 두 위치에서 위로 올라가며 Assets/Content 폴더를 찾는다.
    /// </summary>
    public class ContentDataFilesTests
    {
        static readonly string[] Locales = { "ko", "en", "ja" };

        static string contentDir;

        static string ContentDir => contentDir ??= FindContentDir();

        static string FindContentDir()
        {
            var starts = new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory, AppContext.BaseDirectory };
            foreach (var start in starts)
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Content");
                    if (Directory.Exists(Path.Combine(candidate, "Data"))) return candidate;
                }
            }
            throw new DirectoryNotFoundException("Assets/Content/Data not found above: " + string.Join(", ", starts));
        }

        static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { ContentDir }.Concat(parts).ToArray()));

        static ContentCatalog LoadCatalog()
            => ContentLoader.Load(Read("Data", "species.json"), Read("Data", "moves.json"), Read("Data", "roster.json"));

        [Test]
        public void DataFiles_PassSchemaAndCrossChecks()
        {
            var c = LoadCatalog();
            Assert.That(c.Species.Count, Is.GreaterThan(0));
            Assert.That(c.Roster.Count, Is.GreaterThan(0));
            Assert.IsNotNull(c.BasicMove);
        }

        [Test]
        public void EveryRosterId_ExistsInSpecies()
        {
            var c = LoadCatalog();
            foreach (var r in c.Roster)
                Assert.IsTrue(c.TryGetSpecies(r.Id, out _), $"roster id {r.Id} missing from species.json");
        }

        [Test]
        public void EverySpeciesType_HasSignatureMove()
        {
            var c = LoadCatalog();
            foreach (var s in c.Species)
            {
                foreach (var t in s.Species.Types)
                    Assert.IsNotNull(c.SignatureMoveFor(t), $"species {s.Id}: no signature move for {t}");
                // 기본기 + 타입 수만큼
                Assert.AreEqual(1 + s.Species.Types.Count, c.MoveSetFor(s.Id).Count, $"species {s.Id}");
                Assert.AreEqual(MoveRole.Basic, c.MoveSetFor(s.Id)[0].Role, $"species {s.Id}: basic move first");
            }
        }

        [Test]
        public void EveryNameKey_ExistsInAllLocales()
        {
            var c = LoadCatalog();
            var keys = c.Species.Select(s => s.NameKey).Concat(c.Moves.Select(m => m.NameKey)).ToList();
            foreach (var lang in Locales)
            {
                var table = LocaleTable.Parse(Read("Locale", lang + ".json"));
                var missing = keys.Where(k => !table.Has(k) || string.IsNullOrWhiteSpace(table.Get(k))).ToList();
                CollectionAssert.IsEmpty(missing, $"{lang}.json is missing: {string.Join(", ", missing)}");
            }
        }

        [Test]
        public void NameKeys_FollowConvention()
        {
            var c = LoadCatalog();
            foreach (var s in c.Species) Assert.AreEqual("species." + s.Id.ToString("000"), s.NameKey);
            foreach (var m in c.Moves) Assert.AreEqual("move." + m.Id, m.NameKey);
        }

        [Test]
        public void SpriteNames_FollowSpriteToolOutput()
        {
            // Tools/sprites 의 출력 규칙: {front|back}_{id}[_shiny]_sheet(.png/.json). 시트 자체는 저장소에 없어 이름만 확인한다.
            foreach (var s in LoadCatalog().Species)
            {
                Assert.AreEqual(new SpriteSet($"front_{s.Id}_sheet", $"back_{s.Id}_sheet", $"front_{s.Id}_shiny_sheet", $"back_{s.Id}_shiny_sheet"),
                    s.Sprites, $"species {s.Id}");
            }
        }

        [Test]
        public void Demo_Matchup_IsInRosterAsShowcase()
        {
            // Demo 모드는 피카츄 vs 거북왕 고정 (PROJECT.md 5.3).
            var c = LoadCatalog();
            var tiers = c.Roster.ToDictionary(r => r.Id, r => r.Tier);
            Assert.AreEqual(RosterTier.Showcase, tiers[25]);
            Assert.AreEqual(RosterTier.Showcase, tiers[9]);
        }

        [Test]
        public void LegendarySpecies_AreRarerNpcsThanOthers()
        {
            // 전설은 드물게만 등장 (PROJECT.md 6.5). 값 자체는 밸런스 조정 대상이라 대소 관계만 본다.
            var c = LoadCatalog();
            var legendary = c.Roster.Where(r => c.GetSpecies(r.Id).Legendary).Select(r => r.NpcWeight).ToList();
            var normal = c.Roster.Where(r => !c.GetSpecies(r.Id).Legendary).Select(r => r.NpcWeight).ToList();
            if (legendary.Count == 0 || normal.Count == 0) Assert.Pass("no legendary/normal pair to compare");
            Assert.That(legendary.Max(), Is.LessThan(normal.Min()));
        }

        [Test]
        public void ExistingLocaleKeys_AreStillPresent()
        {
            foreach (var lang in Locales)
            {
                var table = LocaleTable.Parse(Read("Locale", lang + ".json"));
                foreach (var key in new[] { "title.name", "title.start", "title.retry", "title.try_demo", "title.demo_mode", "title.connecting", "title.connect_failed", "title.connected" })
                    Assert.IsTrue(table.Has(key), $"{lang}: {key}");
            }
        }
    }
}
