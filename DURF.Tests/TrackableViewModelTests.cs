using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DURF.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DURF.Tests
{
    [TestClass]
    public class TrackableViewModelTests
    {
        [TestMethod]
        public void TestPropertyChanges()
        {
            var simple = new SimpleViewModel(true);
            var newC = new TrackableCollection<string>();
            simple.ACollection = newC;
            Assert.AreEqual(simple.ACollection, newC);
            simple.ADouble = 5;
            Assert.AreEqual(simple.ADouble, 5);
            simple.AnInt = 5;
            Assert.AreEqual(simple.AnInt, 5);
            simple.AString = "yupperz";
            Assert.AreEqual(simple.AString, "yupperz");
            simple.AnEnum = SimpleViewModel.TestEnum.Three;
            Assert.AreEqual(SimpleViewModel.TestEnum.Three, simple.AnEnum);

            simple.NullableDouble = null;
            Assert.IsNull(simple.NullableDouble);
            simple.NullableInt = null;
            Assert.IsNull(simple.NullableInt);
            simple.NullableEnum = null;
            Assert.IsNull(simple.NullableEnum);
            simple.ACollection = null;
            Assert.IsNull(simple.ACollection);
            simple.StrongReference = null;
            Assert.IsNull(simple.StrongReference);
            simple.StrongReferenceBoxed = null;
            Assert.IsNull(simple.StrongReferenceBoxed);
        }

        [TestMethod]
        public void TestPropertyChanged()
        {
            var simple = new SimpleViewModel(true);
            Dictionary<string,int> propChanges = new Dictionary<string, int>();
            simple.PropertyChanged += (s, e) =>
            {
                if (propChanges.ContainsKey(e.PropertyName))
                    propChanges[e.PropertyName]++;
                else
                    propChanges[e.PropertyName] = 1;
            };
            var newC = new TrackableCollection<string>();
            simple.ACollection = newC;
            Assert.AreEqual(simple.ACollection, newC);
            simple.ADouble = 5;
            Assert.AreEqual(simple.ADouble, 5);
            simple.AnInt = 5;
            Assert.AreEqual(simple.AnInt, 5);
            simple.AString = "yupperz";
            Assert.AreEqual(simple.AString, "yupperz");
            simple.AnEnum = SimpleViewModel.TestEnum.Three;
            Assert.AreEqual(SimpleViewModel.TestEnum.Three, simple.AnEnum);

            simple.NullableDouble = null;
            Assert.IsNull(simple.NullableDouble);
            simple.NullableInt = null;
            Assert.IsNull(simple.NullableInt);
            simple.NullableEnum = null;
            Assert.IsNull(simple.NullableEnum);
            simple.ACollection = null;
            Assert.IsNull(simple.ACollection);
            simple.StrongReference = null;
            Assert.IsNull(simple.StrongReference);
            simple.StrongReferenceBoxed = null;
            Assert.IsNull(simple.StrongReferenceBoxed);

            foreach (var prop in simple.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.CanWrite && p.Name != nameof(TrackableViewModel.Accumulator)))
            {
                Assert.IsTrue(propChanges.ContainsKey(prop.Name), $"Property {prop.Name} was not raised!");
                Assert.IsTrue(propChanges[prop.Name] > 0);
            }
        }

        [TestMethod]
        public void TestTracking()
        {
            var simple = new SimpleViewModel(true);
            var acc = new Accumulator("test");
            simple.Accumulator = acc;
            var newC = new TrackableCollection<string>(acc, true);

            simple.ACollection = newC;
            Assert.AreEqual(simple.ACollection, newC);
            newC.Add("poop");
            newC.Add("stinks");

            simple.ADouble = 5;
            Assert.AreEqual(simple.ADouble, 5);
            simple.AnInt = 5;
            Assert.AreEqual(simple.AnInt, 5);
            simple.AString = "yupperz";
            Assert.AreEqual(simple.AString, "yupperz");
            simple.AnEnum = SimpleViewModel.TestEnum.Three;
            Assert.AreEqual(SimpleViewModel.TestEnum.Three, simple.AnEnum);

            simple.NullableDouble = null;
            Assert.IsNull(simple.NullableDouble);
            simple.NullableInt = null;
            Assert.IsNull(simple.NullableInt);
            simple.NullableEnum = null;
            Assert.IsNull(simple.NullableEnum);
            simple.ACollection = null;
            Assert.IsNull(simple.ACollection);
            simple.StrongReference = null;
            Assert.IsNull(simple.StrongReference);
            simple.StrongReferenceBoxed = null;
            Assert.IsNull(simple.StrongReferenceBoxed);

            Assert.AreEqual(13, acc.Records.Count);
        }

        [TestMethod]
        public void TestNoTracking()
        {
            var simple = new SimpleViewModel(true);
            var newC = new TrackableCollection<string>();

            Assert.IsNull(simple.Accumulator);
            Assert.IsNull(simple.ACollection.Accumulator);
            simple.ACollection = newC;
            Assert.AreEqual(simple.ACollection, newC);
            newC.Add("poop");
            newC.Add("stinks");

            simple.ADouble = 5;
            Assert.AreEqual(simple.ADouble, 5);
            simple.AnInt = 5;
            Assert.AreEqual(simple.AnInt, 5);
            simple.AString = "yupperz";
            Assert.AreEqual(simple.AString, "yupperz");
            simple.AnEnum = SimpleViewModel.TestEnum.Three;
            Assert.AreEqual(SimpleViewModel.TestEnum.Three, simple.AnEnum);

            simple.NullableDouble = null;
            Assert.IsNull(simple.NullableDouble);
            simple.NullableInt = null;
            Assert.IsNull(simple.NullableInt);
            simple.NullableEnum = null;
            Assert.IsNull(simple.NullableEnum);
            simple.ACollection = null;
            Assert.IsNull(simple.ACollection);
            simple.StrongReference = null;
            Assert.IsNull(simple.StrongReference);
            simple.StrongReferenceBoxed = null;
            Assert.IsNull(simple.StrongReferenceBoxed);
            Assert.IsNull(simple.Accumulator);
            Assert.IsNull(Accumulator.Current);
        }

        [TestMethod]
        public async Task TestUndo()
        {
            var simple = new SimpleViewModel(true);
            var originalC = simple.ACollection;
            Assert.AreEqual(0, originalC.Count);
            var origBoxed = simple.StrongReferenceBoxed;
            var origStrong = simple.StrongReference;
            var acc = new Accumulator("test");
            simple.Accumulator = acc;
            var newC = new TrackableCollection<string>(acc, true);

            simple.ACollection = newC;
            Assert.AreEqual(simple.ACollection, newC);
            newC.Add("poop");
            newC.Add("stinks");

            simple.ADouble = 5;
            Assert.AreEqual(simple.ADouble, 5);
            simple.AnInt = 5;
            Assert.AreEqual(simple.AnInt, 5);
            simple.AString = "yupperz";
            Assert.AreEqual(simple.AString, "yupperz");
            simple.AnEnum = SimpleViewModel.TestEnum.Three;
            Assert.AreEqual(SimpleViewModel.TestEnum.Three, simple.AnEnum);

            simple.NullableDouble = null;
            Assert.IsNull(simple.NullableDouble);
            simple.NullableInt = null;
            Assert.IsNull(simple.NullableInt);
            simple.NullableEnum = null;
            Assert.IsNull(simple.NullableEnum);
            simple.ACollection = null;
            Assert.IsNull(simple.ACollection);
            simple.StrongReference = null;
            Assert.IsNull(simple.StrongReference);
            simple.StrongReferenceBoxed = null;
            Assert.IsNull(simple.StrongReferenceBoxed);

            Assert.AreEqual(13, acc.Records.Count);

            await acc.UndoAll(acc.Name);

            Assert.AreEqual(simple.AString, nameof(SimpleViewModel.AString));
            Assert.AreEqual(simple.AnInt, 542);
            Assert.AreEqual(simple.ADouble, 542);
            Assert.AreEqual(simple.StrongReferenceBoxed, origBoxed);
            Assert.AreEqual(simple.StrongReference, origStrong);
            Assert.AreEqual(simple.ACollection, originalC);
            Assert.AreEqual(0, originalC.Count);
            Assert.AreEqual(simple.NullableInt, 0);
            Assert.AreEqual(simple.NullableDouble, 0);
            Assert.AreEqual(simple.NullableEnum, SimpleViewModel.TestEnum.Two);
        }

        private class SimpleViewModel : TrackableViewModel
        {
            public enum TestEnum
            {
                One,
                Two,
                Three
            }

            public SimpleViewModel(bool setup)
            {
                if (!setup)
                    return;
                AString = nameof(AString);
                AnInt = 542;
                ADouble = 542.0;
                StrongReferenceBoxed = new SimpleViewModel(false);
                StrongReference = new SimpleViewModel(false);
                NullableInt = 0;
                NullableDouble = 0;
                NullableEnum = TestEnum.Two;
                AnEnum = TestEnum.Two;
                ACollection = new TrackableCollection<string>();
            }

            public string AString
            {
                get => Get<string>();
                set => Set(value);
            }

            public int AnInt
            {
                get => Get<int>();
                set => Set(value);
            }

            public double ADouble
            {
                get => Get<double>();
                set => Set(value);
            }

            public object StrongReferenceBoxed
            {
                get => Get<object>();
                set => Set(value);
            }

            public SimpleViewModel StrongReference
            {
                get => Get<SimpleViewModel>();
                set => Set(value);
            }

            public int? NullableInt
            {
                get => Get<int?>();
                set => Set(value);
            }

            public double? NullableDouble
            {
                get => Get<double?>();
                set => Set(value);
            }

            public TestEnum AnEnum
            {
                get => Get<TestEnum>();
                set => Set(value);
            }

            public TestEnum? NullableEnum
            {
                get => Get<TestEnum?>();
                set => Set(value);
            }

            public TrackableCollection<string> ACollection
            {
                get => Get<TrackableCollection<string>>();
                set => Set(value);
            }

            // example showing how a 'top-level' vm can push the Accumulator down to everything it manages the lifetime for
            /// <inheritdoc />
            public override Accumulator Accumulator
            {
                get => Get<Accumulator>();
                set
                {
                    if (Set(value) && ACollection != null)
                        ACollection.Accumulator = value;
                }
            }
        }
    }
}
