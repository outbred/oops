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
        /// <summary>Executes a test method.</summary>
        /// <param name="testMethod">The test method to execute.</param>
        /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
        /// <remarks>Extensions can override this method to customize running a TestMethod.</remarks>
        public override TestResult[] Execute(ITestMethod testMethod)
        {
            var result = new List<TestResult>();
            result.Add(testMethod.Invoke(new object[] {false}));
            result.Add(testMethod.Invoke(new object[] {true}));
            return result.ToArray();
        }
    }
}
