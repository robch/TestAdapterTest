using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestAdapterTest
{
    public class YamlTestCaseFilter
    {
        public static IEnumerable<TestCase> FilterTestCases(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var filter = runContext.GetTestCaseFilter(supportedFilterProperties, null);
            return tests.Where(test => filter == null || filter.MatchTestCase(test, name => GetPropertyValue(test, name)));
        }

        private static object GetPropertyValue(TestCase test, string name)
        {
            switch (name.ToLower())
            {
                case "name":
                case "displayname": return test.DisplayName;
                case "fullyqualifiedname": return test.FullyQualifiedName;
            }
            return null;
        }

        private static readonly string[] supportedFilterProperties = { "DisplayName", "FullyQualifiedName" };
    }
}
