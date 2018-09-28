using System;
using DURF.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DURF.Tests
{
    [TestClass]
    public class TrackableDictionaryTests
    {
        [TrackingTestMethod]
        public void TestReplace(bool track)
        {
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) {{"one", 1}, {"two", 2}};
            var other = new TrackableDictionary<string, int>() {{"three", 3}, {"four", 4}};
            dic.Replace(other);
            Assert.AreEqual(2, other.Count);
            foreach(var o in other)
                Assert.IsTrue(dic.Contains(o));

            if(track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestSafeAdd(bool track)
        {
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            Assert.IsFalse(dic.SafeAdd("one", 1));
            Assert.IsTrue(dic.SafeAdd("five", 5));
            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestSafeRemove(bool track)
        {
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            Assert.IsTrue(dic.SafeRemove("one"));
            Assert.IsFalse(dic.SafeRemove("one"));
            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTryGetValueAndRemoveKey(bool track)
        {
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            Assert.IsTrue(dic.TryGetValueAndRemoveKey("one", out var val));
            Assert.AreEqual(1, val);
            Assert.IsFalse(dic.ContainsKey("one"));

            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        public void TestGetEnuemrator()
        {
            var dic = new TrackableDictionary<string, int>();
            Assert.IsNotNull(dic.GetEnumerator());
        }

        [TrackingTestMethod]
        public void TestRemove(bool track)
        {
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            Assert.IsTrue(dic.Remove("one"));
            Assert.IsFalse(dic.Remove("one"));

            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestAdd(bool track)
        {
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.Add("three", 3);
            Assert.IsTrue(dic.ContainsKey("three"));
            Assert.AreEqual(dic["three"], 3);

            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }
    }
}
