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
    [ExtensionUri(TestAdapter.Executor)]
    public class TextExecutor : ITestExecutor
    {
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            Logger.Log(frameworkHandle);
            Logger.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): ENTER");
            Logger.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): count={tests.Count()}");
            TestAdapter.RunTests(tests, runContext, frameworkHandle);
            Logger.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): EXIT");
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            Logger.Log(frameworkHandle);
            Logger.Log($"TextExecutor.RunTests(IEnumerable<string>(): ENTER");
            Logger.Log($"TextExecutor.RunTests(IEnumerable<string>(): count={sources.Count()}");
            RunTests(TestAdapter.GetTestsFromFiles(sources), runContext, frameworkHandle);
            Logger.Log($"TextExecutor.RunTests(IEnumerable<string>(): EXIT");
        }

        public void Cancel()
        {
            Logger.Log($"TextExecutor.Cancel(): ENTER/EXIT");
        }

        private IEnumerable<TestCase> FilterTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var filter = runContext.GetTestCaseFilter(supportedFilterProperties, null);
            return tests.Where(test => filter == null || filter.MatchTestCase(test, name => GetPropertyValue(test, name)));
        }

        private object GetPropertyValue(TestCase test, string name)
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
