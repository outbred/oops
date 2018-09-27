using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DURF.Collections;
using DURF.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DURF.Tests
{
    [TestClass]
    public class ConcurrentListTests
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

        [TestMethod]
        public void TestConcurrentList_GetCopy()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            var copy = coll.GetCopy();
            Assert.IsNotNull(copy);
            Assert.AreNotEqual(coll, copy);
            Assert.AreEqual(coll.Count, copy.Count);
            for (int i = 0; i < coll.Count; i++)
                Assert.AreEqual(coll[i], copy[i]);
        }

        [TestMethod]
        public void TestConcurrentList_GetEnumerator()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            var enumerator = coll.GetEnumerator();
            Assert.IsNotNull(enumerator);
            Assert.AreNotEqual(coll, enumerator);
            var count = 0;
            while (enumerator.MoveNext())
                count++;

            Assert.AreEqual(coll.Count, count);
        }

        [TestMethod]
        public void TestConcurrentList_Index()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.IsNotNull(coll[1]);
            Assert.AreEqual("b", coll[1].Value);

            coll[1] = new Simple("d");
            Assert.IsNotNull(coll[1]);
            Assert.AreEqual("d", coll[1].Value);
            Assert.AreEqual(3, coll.Count);
        }

        [TestMethod]
        public void TestConcurrentList_Add()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            coll.Add(new Simple("d"));
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual("d", coll[3].Value);
        }

        [TestMethod]
        public void TestConcurrentList_AddIfNew()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            var d = new Simple("d");
            coll.Add(d);
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual("d", coll[3].Value);
            coll.AddIfNew(d);
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual("d", coll[3].Value);
        }

        [TestMethod]
        public void TestConcurrentList_AddAndGetindex()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            var idx = coll.AddAndGetIndex(new Simple("d"));
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual(3, idx);
            Assert.AreEqual("d", coll[3].Value);
        }

        [TestMethod]
        public void TestConcurrentList_Clear()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            Assert.AreEqual(3, coll.Count);
            coll.Clear();
            Assert.AreEqual(0, coll.Count);
        }

        [TestMethod]
        public void TestConcurrentList_ReplaceWith()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            coll.ReplaceWith(new ConcurrentList<Simple>() {new Simple("c"), new Simple("d"), new Simple("e")});
            Assert.AreEqual(3, coll.Count);
            Assert.AreEqual("c", coll[0].Value);
            Assert.AreEqual("d", coll[1].Value);
            Assert.AreEqual("e", coll[2].Value);
        }

        [TestMethod]
        public void TestConcurrentList_Contains()
        {
            var b = new Simple("b");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, new Simple("c")};
            Assert.IsTrue(coll.Contains(b));
            var d = new Simple("d");
            Assert.IsFalse(coll.Contains(d));
            coll.Add(d);
            Assert.IsTrue(coll.Contains(d));
        }

        [TestMethod]
        public void TestConcurrentList_Remove()
        {
            var b = new Simple("b");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, new Simple("c")};
            Assert.IsTrue(coll.Remove(b));
            Assert.AreEqual(2, coll.Count);
            Assert.IsFalse(coll.Contains(b));
            coll.Add(b);
            Assert.AreEqual(3, coll.Count);
            Assert.IsTrue(coll.Contains(b));

            Assert.IsTrue(coll.Remove(b));
            Assert.AreEqual(2, coll.Count);
            Assert.IsFalse(coll.Contains(b));
        }

        [TestMethod]
        public void TestConcurrentList_RemoveMany()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, c};
            Assert.AreEqual(2, coll.Remove(new[] {b, c}));
            Assert.AreEqual(1, coll.Count);
            Assert.IsFalse(coll.Contains(b));
            Assert.IsFalse(coll.Contains(c));
        }

        [TestMethod]
        public void TestConcurrentList_ReplaceAll()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var d = new Simple("d");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, c, b, c, b, b};
            var total = coll.ReplaceAll(b, d);
            Assert.AreEqual(4, total);
            Assert.AreEqual(coll[1], d);
            Assert.AreEqual(coll[3], d);
            Assert.AreEqual(coll[5], d);
            Assert.AreEqual(coll[6], d);
        }

        [TestMethod]
        public void TestConcurrentList_RemoveAndGetIndex()
        {
            var b = new Simple("b");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, new Simple("c"), b};
            var idx = coll.RemoveAndGetIndex(b);
            Assert.AreEqual(1, idx);
            idx = coll.RemoveAndGetIndex(b);
            Assert.AreEqual(2, idx);
        }

        [TestMethod]
        public void TestConcurrentList_IndexOf()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, new Simple("c"), b, c};
            var idx = coll.IndexOf(b);
            Assert.AreEqual(1, idx);
            idx = coll.IndexOf(c);
            Assert.AreEqual(4, idx);
        }

        [TestMethod]
        public void TestConcurrentList_ReverseIndexOf()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, new Simple("c"), b, c};
            var idx = coll.ReverseIndexOf(b);
            Assert.AreEqual(3, idx);
            idx = coll.ReverseIndexOf(c);
            Assert.AreEqual(4, idx);
        }


        [TestMethod]
        public void TestConcurrentList_Insert()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, new Simple("c")};
            coll.Insert(0, c);
            Assert.AreEqual(4, coll.Count);
            Assert.AreEqual(c, coll[0]);
        }

        [TestMethod]
        public void TestConcurrentList_RemoveAt()
        {
            var b = new Simple("b");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, new Simple("c")};
            coll.RemoveAt(1);
            Assert.AreEqual(2, coll.Count);
            Assert.AreEqual("c", coll[1].Value);
        }

        [TestMethod]
        public void TestConcurrentList_AddRange()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            coll.AddRange(new List<Simple> {new Simple("d"), new Simple("e"), new Simple("f")});
            Assert.AreEqual(6, coll.Count);
            Assert.AreEqual("d", coll[3].Value);
            Assert.AreEqual("e", coll[4].Value);
            Assert.AreEqual("f", coll[5].Value);
        }

        [TestMethod]
        public void TestConcurrentList_RemoveAllInstances()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var d = new Simple("d");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, c, b, c, b, b};
            Assert.AreEqual(7, coll.Count);
            coll.RemoveAllInstances(b);
            Assert.AreEqual(3, coll.Count);
            Assert.IsFalse(coll.Contains(b));
        }

        [TestMethod]
        public void TestConcurrentList_Transfer()
        {
            var coll = new ConcurrentList<Simple>() {new Simple("a"), new Simple("b"), new Simple("c")};
            var other = new List<Simple> {new Simple("d"), new Simple("e"), new Simple("f")};
            coll.Transfer(other);
            Assert.AreEqual(6, coll.Count);
            Assert.AreEqual(0, other.Count);
        }

        [TestMethod]
        public void TestConcurrentList_ExtractAll()
        {
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {new Simple("a"), b, c, b, c, b, b};
            Assert.AreEqual(7, coll.Count);
            var copy = coll.ExtractAll();
            Assert.AreNotEqual(copy, coll);
            Assert.AreEqual(7, copy.Count);
            Assert.AreEqual(0, coll.Count);
            Assert.AreEqual("a", copy[0].Value);
            Assert.AreEqual(b, copy[1]);
            Assert.AreEqual("c", copy[2].Value);
            Assert.AreEqual(b, copy[3]);
            Assert.AreEqual("c", copy[4].Value);
            Assert.AreEqual(b, copy[5]);
            Assert.AreEqual(b, copy[6]);
        }


        [TestMethod]
        public void TestConcurrentList_FirstOrDefault()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {a, b, c, b, c, b, b};
            var first = coll.FirstOrDefault();
            Assert.AreEqual(a, first);
            coll.Clear();
            first = coll.FirstOrDefault();
            Assert.IsNull(first);

            var coll2 = new ConcurrentList<int>() {-1, 1, 2};
            var f = coll2.FirstOrDefault();
            Assert.AreEqual(-1, f);
            coll2.Clear();
            f = coll2.FirstOrDefault();
            Assert.AreEqual(default(int), f);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestConcurrentList_First_Empty()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {a, b, c, b, c, b, b};
            coll.Clear();
            coll.First();
        }

        [TestMethod]
        public void TestConcurrentList_LastOrDefault()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {a, b, c, b, c, b, b};
            var last = coll.LastOrDefault();
            Assert.AreEqual(b, last);
            coll.Clear();
            last = coll.LastOrDefault();
            Assert.IsNull(last);

            var coll2 = new ConcurrentList<int>() {-1, 1, 2};
            var f = coll2.LastOrDefault();
            Assert.AreEqual(2, f);
            coll2.Clear();
            f = coll2.LastOrDefault();
            Assert.AreEqual(default(int), f);
        }

        [TestMethod]
        public void TestConcurrentList_Last()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {a, b, c, b, c, b, b};
            var last = coll.Last();
            Assert.AreEqual(b, last);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestConcurrentList_Last_Empty()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {a, b, c, b, c, b, b};
            var last = coll.Last();
            Assert.AreEqual(b, last);
            coll.Clear();
            coll.Last();
        }

        [TestMethod]
        public void TestConcurrentList_Any()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {a, b, c, b, c, b, b};
            Assert.IsTrue(coll.Any());
            Assert.IsFalse(coll.Any(i => i.Value == "d"));
            Assert.IsTrue(coll.Any(i => i.Value == "b"));
            coll.Clear();
            Assert.IsFalse(coll.Any());
            Assert.IsFalse(coll.Any(i => i.Value == "d"));
        }


        [TestMethod]
        public void TestConcurrentList_Swap()
        {
            var a = new Simple("a");
            var b = new Simple("b");
            var c = new Simple("c");
            var coll = new ConcurrentList<Simple>() {a, b, c, b, c, b, b};
            Assert.IsTrue(coll.Swap(a, 3));
            Assert.AreEqual(3, coll.IndexOf(a));
            Assert.IsTrue(coll.Swap(a, 3));

            Assert.IsFalse(coll.Swap(a, 40));
            Assert.IsFalse(coll.Swap(new Simple("q"), 0));

            coll.Clear();
            coll.Add(a);
            Assert.IsTrue(coll.Swap(a, 0));
            Assert.IsFalse(coll.Swap(a, 1));
            coll.Clear();
            Assert.IsFalse(coll.Swap(a, 0));
        }

        [TestMethod]
        public void TestConcurrentList_Enqueue()
        {
            IQueue<Simple> coll = new ConcurrentList<Simple>();
            Assert.AreEqual(0, coll.Count());
            coll.Enqueue(new Simple("a"));
            Assert.AreEqual(1, coll.Count());
            coll.Enqueue(new Simple("b"));
            Assert.AreEqual(2, coll.Count());
        }

        [TestMethod]
        public void TestConcurrentList_Dequeue()
        {
            IQueue<Simple> coll = new ConcurrentList<Simple>();
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
        }

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void TestConcurrentList_Dequeue_Empty()
        {
            IQueue<Simple> coll = new ConcurrentList<Simple>();
            Assert.AreEqual(0, coll.Count());
            coll.Dequeue();
        }

        [TestMethod]
        public void TestConcurrentList_TryDequeue()
        {
            IQueue<Simple> coll = new ConcurrentList<Simple>();
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
        }

        [TestMethod]
        public void TestConcurrentList_Push()
        {
            IStack<Simple> coll = new ConcurrentList<Simple>();
            Assert.AreEqual(0, coll.Count());
            coll.Push(new Simple("a"));
            Assert.AreEqual(1, coll.Count());
            coll.Push(new Simple("b"));
            Assert.AreEqual(2, coll.Count());
        }

        [TestMethod]
        public void TestConcurrentList_Pop()
        {
            IStack<Simple> coll = new ConcurrentList<Simple>();
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
        }

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void TestConcurrentList_Pop_Empty()
        {
            IStack<Simple> coll = new ConcurrentList<Simple>();
            Assert.AreEqual(0, coll.Count());
            coll.Pop();
        }

        [TestMethod]
        public void TestConcurrentList_TryPop()
        {
            IStack<Simple> coll = new ConcurrentList<Simple>();
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
        }

        #endregion

        #region Threading Tests

        private readonly ConcurrentList<Simple> _testList = new ConcurrentList<Simple>();
        private int countToAdd = 5000;
        private int numThreadsAdding = 12;
        private bool allDoneWriting = false;

        [TestMethod]
        public void TestConcurrentList_Concurrency()
        {
            var list = new ConcurrentList<object>();
            for (var i = 0; i < 100; i++)
                list.Add(i);

            var threadList = new List<Thread>();
            var readerThreadList = new List<Thread>();
            for (var i = 0; i < numThreadsAdding; i++)
            {
                threadList.Add(new Thread(AddStuffToList) {IsBackground = true});
                readerThreadList.Add(new Thread(WalkThroughList) {IsBackground = true});
            }

            // start all the threads
            for (var index = 0; index < threadList.Count; index++)
            {
                threadList[index].Start(index);
                readerThreadList[index].Start(index);
            }

            // Wait for all of the adders to finish
            foreach (var thread in threadList)
                thread.Join();

            // wait on reader threads
            allDoneWriting = true;
            foreach (var thread in readerThreadList)
                thread.Join();


            // Verify we added the right count
            var totalCount = _testList.Count;
            var intendedCount = numThreadsAdding * countToAdd;
            Assert.IsTrue(totalCount == intendedCount);
        }

        private void AddStuffToList(object threadIndex)
        {
            for (var i = 0; i < countToAdd; i++)
            {
                _testList.Add(new Simple(i.ToString()));
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
