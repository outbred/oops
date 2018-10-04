using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using oops.Collections;
using oops.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace oops.Tests
{
    [TestClass]
    public class TrackableCollectionTests
    {
        private class Simple
        {
            public Simple(string val)
            {
                Value = val;
            }

            public string Value { get; }
        }

        #region Basic Functionality

        [TrackingTestMethod]
        public void TestTrackableCollection_ToList(bool track)
        {
            var acc = new Accumulator("Test");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), new Simple("b"), new Simple("c")} ;
            var copy = coll.ToList();
            Assert.IsNotNull(copy);
            Assert.AreNotEqual(coll, copy);
            Assert.AreEqual(coll.Count, copy.Count);
            for (int i = 0; i < coll.Count; i++)
                Assert.AreEqual(coll[i], copy[i]);
            acc.Close("test");
            if(track)
                Assert.AreEqual(3, acc.Records.Count);
        }

        [TestMethod]
        public void TestTrackableCollection_GetEnumerator()
        {
            var coll = new TrackableCollection<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            var enumerator = coll.GetEnumerator();
            Assert.IsNotNull(enumerator);
            Assert.AreNotEqual(coll, enumerator);
            var count = 0;
            while (enumerator.MoveNext())
                count++;

            Assert.AreEqual(coll.Count, count);
        }

        [TestMethod]
        public void TestTrackableCollection_Index()
        {
            var coll = new TrackableCollection<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.IsNotNull(coll[1]);
            Assert.AreEqual("b", coll[1].Value);

            coll[1] = new Simple("d");
            Assert.IsNotNull(coll[1]);
            Assert.AreEqual("d", coll[1].Value);
            Assert.AreEqual(3, coll.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Add(bool track)
        {
            var acc = new Accumulator("test");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            coll.Add(new Simple("d"));
            acc.Close("test");
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual("d", coll[3].Value);

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_AddIfNew(bool track)
        {
            var acc = new Accumulator("test");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            var d = new Simple("d");
            coll.Add(d);
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual("d", coll[3].Value);
            coll.AddIfNew(d);
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual("d", coll[3].Value);

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_AddAndGetindex(bool track)
        {
            var acc = new Accumulator("test");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            var idx = coll.AddAndGetIndex(new Simple("d"));
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual(3, idx);
            Assert.AreEqual("d", coll[3].Value);

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Clear(bool track)
        {
            var acc = new Accumulator("test");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            coll.Clear();
            Assert.AreEqual(0, coll.Count);

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_ReplaceWith(bool track)
        {
            var acc = new Accumulator("test");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), new Simple("b"), new Simple("c")};
            coll.ReplaceWith(new TrackableCollection<Simple>() {new Simple("c"), new Simple("d"), new Simple("e")});
            Assert.AreEqual(3, coll.Count);
            Assert.AreEqual("c", coll[0].Value);
            Assert.AreEqual("d", coll[1].Value);
            Assert.AreEqual("e", coll[2].Value);

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        public void TestTrackableCollection_Contains()
        {
            var b = new Simple("b");
            var coll = new TrackableCollection<Simple>() {new Simple("a"), b, new Simple("c")};
            Assert.IsTrue(coll.Contains(b));
            var d = new Simple("d");
            Assert.IsFalse(coll.Contains(d));
            coll.Add(d);
            Assert.IsTrue(coll.Contains(d));
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Remove(bool track)
        {
            var acc = new Accumulator("test");
            var b = new Simple("b");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), b, new Simple("c")};
            Assert.IsTrue(coll.Remove(b));
            Assert.AreEqual(2, coll.Count);
            Assert.IsFalse(coll.Contains(b));
            coll.Add(b);
            Assert.AreEqual(3, coll.Count);
            Assert.IsTrue(coll.Contains(b));

            Assert.IsTrue(coll.Remove(b));
            Assert.AreEqual(2, coll.Count);
            Assert.IsFalse(coll.Contains(b));

            if (track)
                Assert.AreEqual(6, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_RemoveMany(bool track)
        {
            var acc = new Accumulator("test");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), b, c};
            Assert.AreEqual(2, coll.Remove(new[] {b, c}));
            Assert.AreEqual(1, coll.Count);
            Assert.IsFalse(coll.Contains(b));
            Assert.IsFalse(coll.Contains(c));

            if (track)
                Assert.AreEqual(5, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_ReplaceAll(bool track)
        {
            var acc = new Accumulator("test");
            var b = new Simple("b");
            var c = new Simple("c");
            var d = new Simple("d");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), b, c, b, c, b, b};
            var total = coll.ReplaceAll(b, d);
            Assert.AreEqual(4, total);
            Assert.AreEqual(coll[1], d);
            Assert.AreEqual(coll[3], d);
            Assert.AreEqual(coll[5], d);
            Assert.AreEqual(coll[6], d);

            if (track)
                Assert.AreEqual(11, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_RemoveAndGetIndex(bool track)
        {
            var acc = new Accumulator("test");
            var b = new Simple("b");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), b, new Simple("c"), b};
            var idx = coll.RemoveAndGetIndex(b);
            Assert.AreEqual(1, idx);
            idx = coll.RemoveAndGetIndex(b);
            Assert.AreEqual(2, idx);

            if (track)
                Assert.AreEqual(6, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        public void TestTrackableCollection_IndexOf()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>() {new Simple("a"), b, new Simple("c"), b, c};
            var idx = coll.IndexOf(b);
            Assert.AreEqual(1, idx);
            idx = coll.IndexOf(c);
            Assert.AreEqual(4, idx);
        }

        [TestMethod]
        public void TestTrackableCollection_ReverseIndexOf()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>() {new Simple("a"), b, new Simple("c"), b, c};
            var idx = coll.ReverseIndexOf(b);
            Assert.AreEqual(3, idx);
            idx = coll.ReverseIndexOf(c);
            Assert.AreEqual(4, idx);
        }


        [TrackingTestMethod]
        public void TestTrackableCollection_Insert(bool track)
        {
            var acc = new Accumulator("test");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), b, new Simple("c")};
            coll.Insert(0, c);
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual(c, coll[0]);

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_RemoveAt(bool track)
        {
            var acc = new Accumulator("test");
            var b = new Simple("b");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), b, new Simple("c")};
            coll.RemoveAt(1);
            Assert.AreEqual(2, coll.Count);
            Assert.AreEqual("c", coll[1].Value);

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_AddRange(bool track)
        {
            var acc = new Accumulator("test");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), new Simple("b"), new Simple("c")};
            coll.AddRange(new List<Simple> {new Simple("d"), new Simple("e"), new Simple("f")});
            Assert.AreEqual(6, coll.Count);
            Assert.AreEqual("d", coll[3].Value);
            Assert.AreEqual("e", coll[4].Value);
            Assert.AreEqual("f", coll[5].Value);

            if (track)
                Assert.AreEqual(6, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_RemoveAllInstances(bool track)
        {
            var acc = new Accumulator("test");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>(acc, track) {new Simple("a"), b, c, b, c, b, b};
            Assert.AreEqual(7, coll.Count);
            coll.RemoveAllInstances(b);
            Assert.AreEqual(3, coll.Count);
            Assert.IsFalse(coll.Contains(b));

            if (track)
                Assert.AreEqual(11, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }


        [TrackingTestMethod]
        public void TestTrackableCollection_FirstOrDefault(bool track)
        {
            var acc = new Accumulator("test");
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>(acc, track) {a, b, c, b, c, b, b};
            var first = coll.FirstOrDefault();
            Assert.AreEqual(a, first);
            coll.Clear();
            first = coll.FirstOrDefault();
            Assert.IsNull(first);

            var coll2 = new TrackableCollection<int>(acc, track) {-1, 1, 2};
            var f = coll2.FirstOrDefault();
            Assert.AreEqual(-1, f);
            coll2.Clear();
            f = coll2.FirstOrDefault();
            Assert.AreEqual(default(int), f);

            if (track)
                Assert.AreEqual(12, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestTrackableCollection_First_Empty()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>() {a, b, c, b, c, b, b};
            coll.Clear();
            coll.First();
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_LastOrDefault(bool track)
        {
            var acc = new Accumulator("test");
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>(acc, track) {a, b, c, b, c, b, b};
            var last = coll.LastOrDefault();
            Assert.AreEqual(b, last);
            coll.Clear();
            last = coll.LastOrDefault();
            Assert.IsNull(last);

            var coll2 = new TrackableCollection<int>(acc, track) {-1, 1, 2};
            var f = coll2.LastOrDefault();
            Assert.AreEqual(2, f);
            coll2.Clear();
            f = coll2.LastOrDefault();
            Assert.AreEqual(default(int), f);

            if (track)
                Assert.AreEqual(12, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        public void TestTrackableCollection_Last()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>() {a, b, c, b, c, b, b};
            var last = coll.Last();
            Assert.AreEqual(b, last);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestTrackableCollection_Last_Empty()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>() {a, b, c, b, c, b, b};
            var last = coll.Last();
            Assert.AreEqual(b, last);
            coll.Clear();
            coll.Last();
        }

        [TestMethod]
        public void TestTrackableCollection_Any()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>() {a, b, c, b, c, b, b};
            Assert.IsTrue(coll.Any());
            Assert.IsFalse(coll.Any(i => i.Value == "d"));
            Assert.IsTrue(coll.Any(i => i.Value == "b"));
            coll.Clear();
            Assert.IsFalse(coll.Any());
            Assert.IsFalse(coll.Any(i => i.Value == "d"));
        }


        [TrackingTestMethod]
        public void TestTrackableCollection_Move(bool track)
        {
            var acc = new Accumulator("test");
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>(acc, track) {a, b, c, b, c, b, b};
            coll.SafeMove(0, 3);
            Assert.AreEqual(2, coll.IndexOf(a));
            coll.SafeMove(3, 3);

            coll.SafeMove(1, 40); // should result in an Add

            coll.Clear();
            coll.Add(a);
            coll.SafeMove(0, 0);
            coll.SafeMove(0, 1);
            coll.Clear();
            coll.SafeMove(0, 0);

            if (track)
                Assert.AreEqual(13, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Move_OutOfRange(bool track)
        {
            var acc = new Accumulator("test");
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>(acc, track) { a, b, c, b, c, b, b };
            coll.SafeMove(0, 10);
            Assert.AreEqual(6, coll.IndexOf(a));

            if (track)
                Assert.AreEqual(8, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestTrackableCollection_Move_BadIndex()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new TrackableCollection<Simple>() { a, b, c, b, c, b, b };
            coll.SafeMove(-1, 10);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Enqueue(bool track)
        {
            var acc = new Accumulator("test");
            IQueue<Simple> coll = new TrackableCollection<Simple>(acc, track);
            Assert.AreEqual(0, coll.Count());
            coll.Enqueue(new Simple("a"));
            Assert.AreEqual(1, coll.Count());
            coll.Enqueue(new Simple("b"));
            Assert.AreEqual(2, coll.Count());

            if (track)
                Assert.AreEqual(2, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Dequeue(bool track)
        {
            var acc = new Accumulator("test");
            IQueue<Simple> coll = new TrackableCollection<Simple>(acc, track);
            Assert.AreEqual(0, coll.Count());
            var a = new Simple("a");
            var b = new Simple("b");
            coll.Enqueue(a);
            coll.Enqueue(b);
            var a1 = coll.Dequeue();
            Assert.AreEqual(a, a1);
            Assert.AreEqual(1, coll.Count());
            var b1 = coll.Dequeue();
            Assert.AreEqual(b, b1);
            Assert.AreEqual(0, coll.Count());

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void TestTrackableCollection_Dequeue_Empty()
        {
            IQueue<Simple> coll = new TrackableCollection<Simple>();
            Assert.AreEqual(0, coll.Count());
            coll.Dequeue();
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_TryDequeue(bool track)
        {
            var acc = new Accumulator("test");
            IQueue<Simple> coll = new TrackableCollection<Simple>(acc, track);
            Assert.AreEqual(0, coll.Count());
            var a = new Simple("a");
            var b = new Simple("b");
            coll.Enqueue(a);
            coll.Enqueue(b);
            Assert.IsTrue(coll.TryDequeue(out var a1));
            Assert.AreEqual(a, a1);
            Assert.AreEqual(1, coll.Count());
            Assert.IsTrue(coll.TryDequeue(out var b1));
            Assert.AreEqual(b, b1);
            Assert.AreEqual(0, coll.Count());
            Assert.IsFalse(coll.TryDequeue(out var c1));

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Push(bool track)
        {
            var acc = new Accumulator("test");
            IStack<Simple> coll = new TrackableCollection<Simple>(acc, track);
            Assert.AreEqual(0, coll.Count());
            coll.Push(new Simple("a"));
            Assert.AreEqual(1, coll.Count());
            coll.Push(new Simple("b"));
            Assert.AreEqual(2, coll.Count());

            if (track)
                Assert.AreEqual(2, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_Pop(bool track)
        {
            var acc = new Accumulator("test");
            IStack<Simple> coll = new TrackableCollection<Simple>(acc, track);
            Assert.AreEqual(0, coll.Count());
            var a = new Simple("a");
            var b = new Simple("b");
            coll.Push(a);
            coll.Push(b);
            var b1 = coll.Pop();
            Assert.AreEqual(b, b1);
            Assert.AreEqual(1, coll.Count());
            var a1 = coll.Pop();
            Assert.AreEqual(a, a1);
            Assert.AreEqual(0, coll.Count());

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void TestTrackableCollection_Pop_Empty()
        {
            IStack<Simple> coll = new TrackableCollection<Simple>();
            Assert.AreEqual(0, coll.Count());
            coll.Pop();
        }

        [TrackingTestMethod]
        public void TestTrackableCollection_TryPop(bool track)
        {
            var acc = new Accumulator("test");
            IStack<Simple> coll = new TrackableCollection<Simple>(acc, track);
            Assert.AreEqual(0, coll.Count());
            var a = new Simple("a");
            var b = new Simple("b");
            coll.Push(a);
            coll.Push(b);
            Assert.IsTrue(coll.TryPop(out var b1));
            Assert.AreEqual(b, b1);
            Assert.AreEqual(1, coll.Count());
            Assert.IsTrue(coll.TryPop(out var a1));
            Assert.AreEqual(a, a1);
            Assert.AreEqual(0, coll.Count());
            Assert.IsFalse(coll.TryPop(out var c1));

            if (track)
                Assert.AreEqual(4, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        #endregion

        #region Threading Tests 

        private TrackableCollection<Simple> _testList = null;
        private int countToAdd = 1200;
        private int numThreadsAdding = 10;
        private bool allDoneWriting = false;

        [Timeout(120 * 1000)]
        [TrackingTestMethod]
        public void TestTrackableCollection_Concurrency(bool track)
        {
            var acc = new Accumulator("test");
            _testList = new TrackableCollection<Simple>(acc, track);
            
            var threadList = new List<Thread>();
            var complexthreadList = new List<Thread>();
            var readerThreadList = new List<Thread>();
            for (var i = 0; i < numThreadsAdding; i++)
            {
                threadList.Add(new Thread(AddStuffToList) {IsBackground = true});
                complexthreadList.Add(new Thread(AddRemoveStuffFromList) {IsBackground = true});
                readerThreadList.Add(new Thread(WalkThroughList) {IsBackground = true});
            }

            // start all the threads
            for (var index = 0; index < readerThreadList.Count; index++)
            {
                threadList[index].Start(index);
                complexthreadList[index].Start(index);
                readerThreadList[index].Start(index);
            }

            // Wait for all of the adders to finish
            foreach (var thread in threadList)
                thread.Join();

            foreach (var thread in complexthreadList)
                thread.Join();

            // wait on reader threads
            allDoneWriting = true;
            foreach (var thread in readerThreadList)
                thread.Join();


            // Verify we added the right count
            var totalCount = _testList.Count;
            var intendedCount = numThreadsAdding * countToAdd;
            Assert.IsTrue(totalCount == intendedCount);

            if (track)
                Assert.AreEqual(3 * numThreadsAdding * countToAdd, acc.Records.Count);
            else
                Assert.AreEqual(0, acc.Records.Count);
        }

        private void AddStuffToList(object threadIndex)
        {
            for (var i = 0; i < countToAdd; i++)
            {
                _testList.Add(new Simple(i.ToString()));
            }
        }

        private void AddRemoveStuffFromList(object threadIndex)
        {
            for (var i = 0; i < countToAdd; i++)
            {
                var simple = new Simple(i.ToString());
                _testList.Add(simple);
                _testList.Remove(simple);
            }
        }

        private void WalkThroughList(object threadIndex)
        {
            try
            {
                while (!allDoneWriting)
                {
                    foreach (var simple in _testList)
                    {
                        var localVec = new Simple(simple.Value);
                    }

                    var tempList = new List<Simple>();
                    tempList.AddRange(_testList);
                }
            }
            catch (InvalidOperationException)
            {
                Assert.IsTrue(false); // our access to enumerate the collection was not thread safe!! - joe (08 Apr 2016)
            }
        }

        #endregion
    }
}