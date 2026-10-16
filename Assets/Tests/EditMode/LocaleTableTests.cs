using NUnit.Framework;
using Pokepedia.Content;

namespace Pokepedia.Tests
{
    public class LocaleTableTests
    {
        const string Json = @"{
  ""title"": { ""start"": ""시작"" },
  ""battle"": { ""fainted"": ""{0}은(는) 쓰러졌다!"" }
}";

        [Test]
        public void NestedKeys_AreFlattenedWithDots_AndFormatted()
        {
            var t = LocaleTable.Parse(Json);
            Assert.AreEqual("시작", t.Get("title.start"));
            Assert.AreEqual("피카츄은(는) 쓰러졌다!", t.Format("battle.fainted", "피카츄"));
        }

        [Test]
        public void MissingKey_ReturnsKeyItself()
        {
            Assert.AreEqual("nope.key", LocaleTable.Parse(Json).Get("nope.key"));
        }

        [Test]
        public void NonStringValue_IsRejected()
        {
            Assert.Throws<System.FormatException>(() => LocaleTable.Parse("{ \"a\": 1 }"));
        }
    }
}
