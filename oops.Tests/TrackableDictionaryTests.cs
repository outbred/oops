using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) {{"one", 1}, {"two", 2}};
            var other = new TrackableDictionary<string, int>() {{"three", 3}, {"four", 4}};
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Replace);
            };

            dic.Replace(other);
            Assert.AreEqual(1, events);
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
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Add);
                Assert.AreEqual(e.NewItems[0], new KeyValuePair<string, int>("five", 5));
            };
            Assert.IsFalse(dic.SafeAdd("one", 1));
            Assert.IsTrue(dic.SafeAdd("five", 5));
            Assert.AreEqual(1, events);
            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestSafeRemove(bool track)
        {
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Remove);
                Assert.AreEqual(e.NewItems[0], new KeyValuePair<string, int>("one", 1));
            };
            Assert.IsTrue(dic.SafeRemove("one"));
            Assert.IsFalse(dic.SafeRemove("one"));
            Assert.AreEqual(1, events);
            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTryGetValueAndRemoveKey(bool track)
        {
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Remove);
                Assert.AreEqual(e.OldItems[0], new KeyValuePair<string, int>("one", 1));
            };
            Assert.IsTrue(dic.TryGetValueAndRemoveKey("one", out var val));
            Assert.AreEqual(1, val);
            Assert.IsFalse(dic.ContainsKey("one"));
            Assert.AreEqual(1, events);
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
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Remove);
                Assert.AreEqual(e.OldItems[0], new KeyValuePair<string, int>("one", 1));
            };

            Assert.IsTrue(dic.Remove("one"));
            Assert.IsFalse(dic.Remove("one"));
            Assert.AreEqual(1, events);

            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestAdd(bool track)
        {
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Add);
                Assert.AreEqual(e.NewItems[0], new KeyValuePair<string, int>("three", 3));
            };
            dic.Add("three", 3);
            Assert.IsTrue(dic.ContainsKey("three"));
            Assert.AreEqual(dic["three"], 3);

            Assert.AreEqual(1, events);

            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestAddRange(bool track)
        {
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            var other = new TrackableDictionary<string, int>() { { "three", 3 }, { "four",4 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Reset);
            };
            dic.AddRange(other);
            Assert.IsTrue(dic.ContainsKey("three"));
            Assert.IsTrue(dic.ContainsKey("four"));
            Assert.AreEqual(dic["three"], 3);
            Assert.AreEqual(dic["four"], 4);
            Assert.AreEqual(1, events);
            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestClear(bool track)
        {
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Reset);
            };
            dic.Clear();
            Assert.AreEqual(0, dic.Count);
            Assert.AreEqual(1, events);
            if (track)
                Assert.AreEqual(3, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTryGetValue(bool track)
        {
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            Assert.IsTrue(dic.TryGetValue("one", out var val));
            Assert.AreEqual(1, val);
            Assert.IsTrue(dic.ContainsKey("one"));

            Assert.IsFalse(dic.TryGetValue("four", out val));

            if (track)
                Assert.AreEqual(2, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestIndexing(bool track)
        {
            var events = 0;
            var acc = new Accumulator("test");
            var dic = new TrackableDictionary<string, int>(acc, track) { { "one", 1 }, { "two", 2 } };
            dic.CollectionChanged += (s, e) =>
            {
                events++;
                Assert.AreEqual(e.Action, NotifyCollectionChangedAction.Replace);
            };
            dic["one"] = 3;
            Assert.AreEqual(3, dic["one"]);
            dic["four"] = 4;
            Assert.AreEqual(4, dic["four"]);
            Assert.AreEqual(2, events);
            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }
    }
}
