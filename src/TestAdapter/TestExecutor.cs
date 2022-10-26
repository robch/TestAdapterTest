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
    [ExtensionUri(YamlTestAdapter.Executor)]
    public class TestExecutor : ITestExecutor
    {
        public sealed class TargetSite : IDisposable
        {
            private readonly IDisposable target;

            public TargetSite(IEnumerable<string> sources)
            {
                if (sources == null) return; // nothing to do

                Type type = null;

                foreach (var source in sources)
                {
                    var assembly = Assembly.LoadFile(source);

                    if (assembly.GetReferencedAssemblies().Where(a => a.Name.Contains("Azure.AI.CLI.TestAdapter"))?.Any() ?? false)
                    {
                        type = assembly.GetTypes().Where(t => t.GetCustomAttribute(typeof(YamlTestRunnerTriggerAttribute)) != null).FirstOrDefault();

                        break; // we assume the first assembly is sufficient
                    }
                }

                if (type == null) return; // nothing to do

                var instance = Activator.CreateInstance(type);

                if (instance is IDisposable)
                {
                    target = instance as IDisposable;
                }
            }

            public void Dispose()
            {
                target?.Dispose();
            }
        }

        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            Logger.Log(frameworkHandle);
            Logger.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): ENTER");
            Logger.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): count={tests.Count()}");
            YamlTestAdapter.RunTests(tests, runContext, frameworkHandle);
            Logger.Log($"TextExecutor.RunTests(IEnumerable<TestCase>(): EXIT");
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            using(new TargetSite(sources))
            {
                Logger.Log(frameworkHandle);
                Logger.Log($"TextExecutor.RunTests(IEnumerable<string>(): ENTER");
                Logger.Log($"TextExecutor.RunTests(IEnumerable<string>(): count={sources.Count()}");
                RunTests(YamlTestAdapter.GetTestsFromFiles(sources), runContext, frameworkHandle);
                Logger.Log($"TextExecutor.RunTests(IEnumerable<string>(): EXIT");
            }
        }

        public void Cancel()
        {
            Logger.Log($"TextExecutor.Cancel(): ENTER/EXIT");
        }
    }
}
