using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TestAdapterTest
{
    public sealed class TestRunHost : IDisposable
    {
        private readonly IDisposable target;

        public static TestRunHost FromSources(IEnumerable<string> sources) => new TestRunHost(sources);

        public TestRunHost(IEnumerable<string> sources)
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
}