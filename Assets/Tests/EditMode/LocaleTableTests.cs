using NUnit.Framework;
using Pokepedia.Content;

namespace Pokepedia.Tests
{
    public class LocaleTableTests
    {
        const string Json = @"{
  ""title"": { ""start"": ""시작"", ""demo"": ""데모 체험"" },
  ""battle"": { ""fainted"": ""{0}은(는) 쓰러졌다!"", ""quote"": ""\""따옴표\"" 줄\n바꿈"" },
  ""ja"": ""あ""
}";

        [Test]
        public void NestedKeys_AreFlattenedWithDots()
        {
            var t = LocaleTable.Parse(Json);
            Assert.AreEqual("시작", t.Get("title.start"));
            Assert.AreEqual("데모 체험", t.Get("title.demo"));
            Assert.AreEqual(5, t.Count);
        }

        [Test]
        public void Escapes_AreDecoded()
        {
            var t = LocaleTable.Parse(Json);
            Assert.AreEqual("\"따옴표\" 줄\n바꿈", t.Get("battle.quote"));
            Assert.AreEqual("あ", t.Get("ja"));
        }

        [Test]
        public void Format_FillsPlaceholders()
        {
            Assert.AreEqual("피카츄은(는) 쓰러졌다!", LocaleTable.Parse(Json).Format("battle.fainted", "피카츄"));
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
