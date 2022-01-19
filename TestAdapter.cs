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
        public const string FileExtensionDll = ".dll";
        public const string FileExtensionYaml = ".yaml";
        public const string Executor = "executor://robch/v1";

        public static void Log(IMessageLogger logger)
        {
            TestAdapter.logger = logger;
        }

        public static void Log(string text)
        {
            File.AppendAllText("log", $"{DateTime.Now}: {text}\n");

            #if DEBUG
            logger?.SendMessage(TestMessageLevel.Informational, $"{DateTime.Now}: {text}");
            #endif
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
            Log($"TestAdapter::GetTestsFromFile('{source}')");

            var file = new FileInfo(source);
            Log($"TestAdapter::GetTestsFromFile('{source}'): Extension={file.Extension}");

            return file.Extension.Trim('.') == FileExtensionYaml.Trim('.')
                ? GetTestsFromYaml(source, file)
                : GetTestsFromDirectory(source, file.Directory);
        }

        public static void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            foreach (var test in tests)
            {
                RunTest(test, runContext, frameworkHandle);
            }
        }

        private static IEnumerable<TestCase> GetTestsFromDirectory(string source, DirectoryInfo directory)
        {
            Log($"TestAdapter::GetTestsFromDirectory('{source}', '{directory.FullName}'): ENTER");
            foreach (var file in directory.GetFiles($"*{FileExtensionYaml}"))
            {
                foreach (var test in GetTestsFromYaml(source, file))
                {
                    yield return test;
                }
            }
            Log($"TestAdapter::GetTestsFromDirectory('{source}', '{directory.FullName}'): EXIT");
        }

        private static IEnumerable<TestCase> GetTestsFromYaml(string source, FileInfo file)
        {
            Log($"TestAdapter::GetTestsFromYaml('{source}', '{file.FullName}'): ENTER");
            foreach (var test in YamlTestCaseParser.TestCasesFromYaml(source, file))
            {
                yield return test;
            }
            Log($"TestAdapter::GetTestsFromYaml('{source}', '{file.FullName}'): EXIT");
        }

        private static void RunTest(TestCase test, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            TestStart(test, frameworkHandle);
            TestEnd(test, frameworkHandle, TestRunAndRecord(test, frameworkHandle));
        }

        private static void TestStart(TestCase test, IFrameworkHandle frameworkHandle)
        {
            TestAdapter.Log($"TestAdapter.TestStart({test.DisplayName})");
            frameworkHandle.RecordStart(test);
        }

        private static void TestEnd(TestCase test, IFrameworkHandle frameworkHandle, TestOutcome outcome)
        {
            TestAdapter.Log($"TestAdapter.TestEnd({test.DisplayName})");
            frameworkHandle.RecordEnd(test, outcome);
        }

        private static TestOutcome TestRunAndRecord(TestCase test, IFrameworkHandle frameworkHandle)
        {
            TestAdapter.Log($"TestAdapter.TestRunAndRecord({test.DisplayName})");
            return YamlTestCaseRunner.RunTestCase(test, frameworkHandle);
        }

        private static IMessageLogger logger = null;
    }
}
