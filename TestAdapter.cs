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
           Logger.Log($"TestAdapter::GetTestsFromFile('{source}')");

            var file = new FileInfo(source);
           Logger.Log($"TestAdapter::GetTestsFromFile('{source}'): Extension={file.Extension}");

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

        #region private methods

        private static IEnumerable<TestCase> GetTestsFromDirectory(string source, DirectoryInfo directory)
        {
           Logger.Log($"TestAdapter::GetTestsFromDirectory('{source}', '{directory.FullName}'): ENTER");
            foreach (var file in FindFiles(directory))
            {
                foreach (var test in GetTestsFromYaml(source, file))
                {
                    yield return test;
                }
            }
           Logger.Log($"TestAdapter::GetTestsFromDirectory('{source}', '{directory.FullName}'): EXIT");
        }

        private static IEnumerable<FileInfo> FindFiles(DirectoryInfo directory)
        {
            var files1 = directory.GetFiles($"*{FileExtensionYaml}");
            var files2 = directory.GetFiles($"tests\\*{FileExtensionYaml}");
            return files1.Concat(files2);
        }

        private static IEnumerable<TestCase> GetTestsFromYaml(string source, FileInfo file)
        {
           Logger.Log($"TestAdapter::GetTestsFromYaml('{source}', '{file.FullName}'): ENTER");
            foreach (var test in YamlTestCaseParser.TestCasesFromYaml(source, file))
            {
                yield return test;
            }
           Logger.Log($"TestAdapter::GetTestsFromYaml('{source}', '{file.FullName}'): EXIT");
        }

        private static void RunTest(TestCase test, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            TestStart(test, frameworkHandle);
            TestEnd(test, frameworkHandle, TestRunAndRecord(test, frameworkHandle));
        }

        private static void TestStart(TestCase test, IFrameworkHandle frameworkHandle)
        {
            Logger.Log($"TestAdapter.TestStart({test.DisplayName})");
            frameworkHandle.RecordStart(test);
        }

        private static void TestEnd(TestCase test, IFrameworkHandle frameworkHandle, TestOutcome outcome)
        {
            Logger.Log($"TestAdapter.TestEnd({test.DisplayName})");
            frameworkHandle.RecordEnd(test, outcome);
        }

        private static TestOutcome TestRunAndRecord(TestCase test, IFrameworkHandle frameworkHandle)
        {
            Logger.Log($"TestAdapter.TestRunAndRecord({test.DisplayName})");
            return YamlTestCaseRunner.RunAndRecordTestCase(test, frameworkHandle);
        }

        #endregion

        #region test adapter registration data
        public const string FileExtensionDll = ".dll";
        public const string FileExtensionYaml = ".yaml";
        public const string Executor = "executor://robch/v1";
        #endregion
    }
}
