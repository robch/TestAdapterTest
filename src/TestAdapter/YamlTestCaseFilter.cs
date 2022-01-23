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

                case "command": return YameTestProperties.Get(test, "command");
                case "script": return YameTestProperties.Get(test, "script");

                case "expect": return YameTestProperties.Get(test, "expect");
                case "not-expect": return YameTestProperties.Get(test, "not-expect");
                case "log-expect": return YameTestProperties.Get(test, "log-expect");
                case "log-not-expect": return YameTestProperties.Get(test, "log-not-expect");

                case "simulate": return YameTestProperties.Get(test, "simulate");
            }
            return null;
        }

        private static readonly string[] supportedFilterProperties = { "DisplayName", "FullyQualifiedName", "command", "script", "expect", "not-expect", "log-expect", "log-not-expect", "simulate" };
    }
}
