using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pokepedia.Content;
using Pokepedia.Domain;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 저장소의 실제 Assets/Content/Data/*.json 과 Locale 을 검증한다 (PROJECT.md 7.8).
    /// 스키마·교차 검증(로스터 id, 타입별 대표 기술 등)은 ContentLoader.Load 가 하므로 여기서는 Load 가 통과하는지와 파일 사이 연결만 본다.
    /// Unity Test Runner(작업 폴더 = 프로젝트 루트)와 dotnet test(테스트 dll 폴더) 양쪽에서 위로 올라가며 Assets/Content 를 찾는다.
    /// </summary>
    public class ContentDataFilesTests
    {
        static string contentDir;

        static string ContentDir => contentDir ??= FindContentDir();

        static string FindContentDir()
        {
            var starts = new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory, AppContext.BaseDirectory };
            foreach (var start in starts)
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Content");
                    if (Directory.Exists(Path.Combine(candidate, "Data"))) return candidate;
                }
            throw new DirectoryNotFoundException("Assets/Content/Data not found above: " + string.Join(", ", starts));
        }

        internal static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { ContentDir }.Concat(parts).ToArray()));

        static ContentCatalog LoadCatalog()
            => ContentLoader.Load(Read("Data", "species.json"), Read("Data", "moves.json"), Read("Data", "roster.json"));

        [Test]
        public void DataFiles_PassSchemaAndCrossChecks()
        {
            var c = LoadCatalog();
            Assert.That(c.Roster.Count, Is.GreaterThan(0));
        }

        [Test]
        public void EveryNameKey_ExistsInAllLocales()
        {
            var c = LoadCatalog();
            var keys = c.Species.Select(s => s.NameKey).Concat(c.Moves.Select(m => m.NameKey)).ToList();
            foreach (var lang in new[] { "ko", "en", "ja" })
            {
                var table = LocaleTable.Parse(Read("Locale", lang + ".json"));
                var missing = keys.Where(k => !table.Has(k) || string.IsNullOrWhiteSpace(table.Get(k))).ToList();
                CollectionAssert.IsEmpty(missing, $"{lang}.json is missing: {string.Join(", ", missing)}");
            }
        }

        [Test]
        public void SpriteNames_FollowSpriteToolOutput()
        {
            // Tools/sprites 의 출력 규칙: {front|back}_{id}[_shiny]_sheet(.png/.json). 시트 자체는 저장소에 없어 이름만 확인한다.
            foreach (var s in LoadCatalog().Species)
                Assert.AreEqual(new SpriteSet($"front_{s.Id}_sheet", $"back_{s.Id}_sheet", $"front_{s.Id}_shiny_sheet", $"back_{s.Id}_shiny_sheet"),
                    s.Sprites, $"species {s.Id}");
        }
    }
}
