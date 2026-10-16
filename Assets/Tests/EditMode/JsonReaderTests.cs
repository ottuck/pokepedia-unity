using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pokepedia.Content;

namespace Pokepedia.Tests
{
    public class JsonReaderTests
    {
        [Test]
        public void ParsesAllValueKinds()
        {
            var o = (Dictionary<string, object>)JsonReader.Parse(@"{ ""n"": -2.5E-1, ""s"": ""x"", ""a"": [1, true, false, null, {}] }");
            Assert.AreEqual(-0.25, o["n"]);
            Assert.AreEqual("x", o["s"]);
            var a = (List<object>)o["a"];
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual(true, a[1]);
            Assert.AreEqual(false, a[2]);
            Assert.IsNull(a[3]);
            Assert.IsInstanceOf<Dictionary<string, object>>(a[4]);
        }

        [Test]
        public void StringEscapes_AreDecoded_IncludingSurrogatePairs()
        {
            Assert.AreEqual("q\" b\\ s/ n\n t\t", JsonReader.Parse(@"""q\"" b\\ s\/ n\n t\t"""));
            Assert.AreEqual("피카", JsonReader.Parse(@"""피카"""));
            Assert.AreEqual("\U0001F600", JsonReader.Parse(@"""😀"""));
        }

        [TestCase("{")]
        [TestCase("[1,]")]
        [TestCase("{a: 1}")]
        [TestCase("\"unterminated")]
        [TestCase("\"bad \\x escape\"")]
        [TestCase("01")]
        [TestCase("[1] 2")]
        [TestCase("\"raw\nnewline\"")]
        public void Malformed_Throws(string json)
        {
            Assert.Throws<FormatException>(() => JsonReader.Parse(json));
        }

        [Test]
        public void DuplicateKey_Throws()
        {
            Assert.Throws<FormatException>(() => JsonReader.Parse(@"{ ""a"": 1, ""a"": 2 }"));
        }
    }
}
