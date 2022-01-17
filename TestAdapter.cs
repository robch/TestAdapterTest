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
    public class TestAdapter
    {
        public const string FileExtension1 = ".dll";
        public const string FileExtension2 = ".yaml";
        public const string Executor = "executor://robch/v1";
        public static Uri ExecutorUri = new Uri(Executor);

        public static void Log(string text)
        {
            File.AppendAllText("log", $"{DateTime.Now}: {text}\n");
        }

        public static IEnumerable<TestCase> GetTestsFromFiles(IEnumerable<string> sources)
        {
            foreach (var source in sources)
            {
                foreach (var test in GetTestsFromFile(source))
                {
                    yield return test;
                }
            }
        }

        public static IEnumerable<TestCase> GetTestsFromFile(string source)
        {
            var file = new FileInfo(source);
            return GetTestsFromYaml(file);
        }

        public static void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            foreach (var test in tests)
            {
                RunTest(test, runContext, frameworkHandle);
            }
        }

        private static IEnumerable<TestCase> GetTestsFromYaml(FileInfo file)
        {
            var name = file.FullName.Remove(file.FullName.LastIndexOf(FileExtension1)).Replace(" ", "");
            var parts = name.Split(":/\\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var tailParts = parts.Reverse().Take(5).Reverse();
            var prefix = string.Join(".", tailParts);

            yield return new TestCase($"{prefix}.Test1", ExecutorUri, file.FullName);
            yield return new TestCase($"{prefix}.Test2", ExecutorUri, file.FullName);
            yield return new TestCase($"{prefix}.Test3", ExecutorUri, file.FullName);
        }

        private static void RunTest(TestCase test, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            TestAdapter.Log($"RunTest({test.DisplayName}): RecordStart, Wait...");
            frameworkHandle.RecordStart(test);
            Task.Delay(500).Wait();

            var outcome = test.DisplayName.Contains("2") ? TestOutcome.Failed : TestOutcome.Passed;

            TestAdapter.Log($"RunTest({test.DisplayName}): RecordResult...");
            var result = new TestResult(test);
            result.Outcome = outcome;
            frameworkHandle.RecordResult(result);

            TestAdapter.Log($"RunTest({test.DisplayName}): RecordEnd");
            frameworkHandle.RecordEnd(test, outcome);
        }

    }
}
