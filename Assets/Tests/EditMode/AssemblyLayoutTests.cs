using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Pokepedia.Tests
{
    /// <summary>
    /// Unity 는 asmdef 마다 따로 컴파일하지만 Tests.Dotnet 은 모든 소스를 한 어셈블리로 컴파일한다.
    /// 그래서 "어셈블리마다 필요한 것"의 누락은 dotnet CI 에서 드러나지 않는다 → 파일 구조로 검사한다.
    /// </summary>
    public class AssemblyLayoutTests
    {
        static string ScriptsDir()
        {
            var starts = new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory, AppContext.BaseDirectory };
            foreach (var start in starts)
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Scripts");
                    if (Directory.Exists(candidate)) return candidate;
                }
            throw new DirectoryNotFoundException("Assets/Scripts not found");
        }

        static readonly Regex RecordOrInit = new Regex(@"\brecord\b|\binit\s*;", RegexOptions.Compiled);

        [Test]
        public void EveryAssemblyUsingRecords_DefinesIsExternalInit()
        {
            var missing = Directory.GetFiles(ScriptsDir(), "*.asmdef", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .Where(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                    .Any(f => RecordOrInit.IsMatch(File.ReadAllText(f))))
                .Where(dir => !File.Exists(Path.Combine(dir, "IsExternalInit.cs")))
                .Select(Path.GetFileName)
                .ToArray();
            Assert.IsEmpty(missing, "C# 9 record/init 을 쓰는 asmdef 폴더에 IsExternalInit.cs 가 없다 (Unity 컴파일 오류 CS0518): " + string.Join(", ", missing));
        }
    }
}
