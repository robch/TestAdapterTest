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
            TestAdapter.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): ENTER");
            TestAdapter.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): count={tests.Count()}");
            TestAdapter.RunTests(tests, runContext, frameworkHandle);
            TestAdapter.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): EXIT");
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            TestAdapter.Log($"TextExecutor.RunTests(IEnumerable<string>(): ENTER");
            TestAdapter.Log($"TextExecutor.RunTests(IEnumerable<string>(): count={sources.Count()}");
            RunTests(TestAdapter.GetTestsFromFiles(sources), runContext, frameworkHandle);
            TestAdapter.Log($"TextExecutor.RunTests(IEnumerable<string>(): EXIT");
        }

        public void Cancel()
        {
            TestAdapter.Log($"TextExecutor.Cancel(): ENTER/EXIT");
        }
    }
}
