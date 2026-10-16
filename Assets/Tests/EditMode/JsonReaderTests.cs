using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pokepedia.Content;

namespace Pokepedia.Tests
{
    public class JsonReaderTests
    {
        [Test]
        public void Object_BecomesDictionary()
        {
            var o = (Dictionary<string, object>)JsonReader.Parse(@"{ ""a"": 1, ""b"": ""x"" }");
            Assert.AreEqual(2, o.Count);
            Assert.AreEqual(1.0, o["a"]);
            Assert.AreEqual("x", o["b"]);
        }

        [Test]
        public void Array_BecomesList_InOrder()
        {
            var a = (List<object>)JsonReader.Parse("[1, \"two\", [3], {}]");
            Assert.AreEqual(4, a.Count);
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual("two", a[1]);
            Assert.AreEqual(3.0, ((List<object>)a[2])[0]);
            Assert.IsInstanceOf<Dictionary<string, object>>(a[3]);
        }

        [Test]
        public void EmptyContainers()
        {
            Assert.AreEqual(0, ((List<object>)JsonReader.Parse("[ ]")).Count);
            Assert.AreEqual(0, ((Dictionary<string, object>)JsonReader.Parse(" { } ")).Count);
        }

        [Test]
        public void Literals()
        {
            var a = (List<object>)JsonReader.Parse("[true, false, null]");
            Assert.AreEqual(true, a[0]);
            Assert.AreEqual(false, a[1]);
            Assert.IsNull(a[2]);
        }

        [TestCase("0", 0.0)]
        [TestCase("-12", -12.0)]
        [TestCase("3.5", 3.5)]
        [TestCase("1e2", 100.0)]
        [TestCase("-2.5E-1", -0.25)]
        public void Numbers_AreDoubles(string json, double expected)
        {
            Assert.AreEqual(expected, JsonReader.Parse(json));
        }

        [Test]
        public void StringEscapes_AreDecoded()
        {
            var s = (string)JsonReader.Parse(@"""q\"" b\\ s\/ n\n t\t r\r b\b f\f""");
            Assert.AreEqual("q\" b\\ s/ n\n t\t r\r b\b f\f", s);
        }

        [Test]
        public void UnicodeEscape_IsDecoded_IncludingSurrogatePairs()
        {
            Assert.AreEqual("피카", JsonReader.Parse(@"""\uD53C\uCE74"""));
            Assert.AreEqual("\U0001F600", JsonReader.Parse(@"""\ud83d\ude00"""));
        }

        [Test]
        public void NonAsciiText_PassesThrough()
        {
            Assert.AreEqual("ピカチュウ １０まんボルト", JsonReader.Parse("\"ピカチュウ １０まんボルト\""));
        }

        [Test]
        public void Nested_ObjectInArrayInObject()
        {
            var o = (Dictionary<string, object>)JsonReader.Parse(@"{ ""list"": [ { ""id"": 25, ""ok"": true } ] }");
            var inner = (Dictionary<string, object>)((List<object>)o["list"])[0];
            Assert.AreEqual(25.0, inner["id"]);
            Assert.AreEqual(true, inner["ok"]);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("[1, 2")]
        [TestCase("[1,]")]
        [TestCase("{\"a\":1,}")]
        [TestCase("{\"a\" 1}")]
        [TestCase("{a: 1}")]
        [TestCase("\"unterminated")]
        [TestCase("\"bad \\x escape\"")]
        [TestCase("\"bad \\u12G4\"")]
        [TestCase("\"short \\u12\"")]
        [TestCase("tru")]
        [TestCase("nul")]
        [TestCase("01")]
        [TestCase("1.")]
        [TestCase("-")]
        [TestCase("1e")]
        [TestCase(".5")]
        [TestCase("+1")]
        [TestCase("[1] 2")]
        [TestCase("{\"a\":1} x")]
        [TestCase("\"raw\nnewline\"")]
        public void Malformed_Throws(string json)
        {
            Assert.Throws<FormatException>(() => JsonReader.Parse(json));
        }

        [Test]
        public void Error_ReportsPosition()
        {
            //            0123456789
            var ex = Assert.Throws<FormatException>(() => JsonReader.Parse("[1, 2, @]"));
            StringAssert.Contains("7", ex.Message);
        }

        [Test]
        public void DuplicateKey_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => JsonReader.Parse(@"{ ""a"": 1, ""a"": 2 }"));
            StringAssert.Contains("a", ex.Message);
        }

        [Test]
        public void Null_Input_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => JsonReader.Parse(null));
        }
    }
}
