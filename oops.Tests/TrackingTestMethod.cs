using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DURF.Tests
{
    public class TrackingTestMethodAttribute : TestMethodAttribute
    {
        /// <inheritdoc />
        public override TestResult[] Execute(ITestMethod testMethod)
        {
            var result = new List<TestResult>();
            result.Add(testMethod.Invoke(new object[] {false}));
            result.Add(testMethod.Invoke(new object[] {true}));
            return result.ToArray();
        }
    }
}
