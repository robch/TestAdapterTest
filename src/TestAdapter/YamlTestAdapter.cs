using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestAdapterTest
{
    public class YamlTestAdapter
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
                : GetTestsFromSource(source, file);
        }

        public static void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            foreach (var test in FilterTestCases(tests, runContext, frameworkHandle))
            {
                RunAndRecordTestCase(test, frameworkHandle);
            }
        }

        #region private methods

        private static IEnumerable<TestCase> GetTestsFromSource(string source, FileInfo file)
        {
            var sourceOk =
                source.Contains("Azure.AI.CLI.TestAdapter") ||
                Assembly.LoadFile(source).GetReferencedAssemblies().Count(x => x.Name.Contains("Azure.AI.CLI.TestAdapter")) > 0;

            // foreach (var a in Assembly.LoadFile(source).GetReferencedAssemblies())
            // {
            //     Logger.Log($"a.Name={a.Name}");
            //     Logger.Log($"a.FullName={a.FullName}");
            // }

            Logger.Log($"GetTestsFromSource('{source}'): sourceOk = {sourceOk}");

            return !sourceOk
                ? Enumerable.Empty<TestCase>()
                : GetTestsFromDirectory(source, file.Directory);
        }

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
            return directory.GetFiles($"*{FileExtensionYaml}", SearchOption.AllDirectories);
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

        private static IEnumerable<TestCase> FilterTestCases(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            Logger.Log($"TestAdapter.FilterTestCases()");
            return YamlTestCaseFilter.FilterTestCases(tests, runContext, frameworkHandle);
        }

        private static TestOutcome RunAndRecordTestCase(TestCase test, IFrameworkHandle frameworkHandle)
        {
            Logger.Log($"TestAdapter.TestRunAndRecord({test.DisplayName})");
            return YamlTestCaseRunner.RunAndRecordTestCase(test, frameworkHandle);
        }

        #endregion

        #region test adapter registration data
        public const string FileExtensionDll = ".dll";
        public const string FileExtensionYaml = ".yaml";
        public const string Executor = "executor://spx/yaml/VsTestRunner1";
        #endregion
    }
}
