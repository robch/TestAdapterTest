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
    [FileExtension(TestAdapter.FileExtension1)]
    [FileExtension(TestAdapter.FileExtension2)]
    [DefaultExecutorUri(TestAdapter.Executor)]
    public class TestDiscoverer : ITestDiscoverer
    {
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            TestAdapter.Log($"TestDiscoverer.DiscoverTests(): ENTER");
            TestAdapter.Log($"TestDiscoverer.DiscoverTests(): count={sources.Count()}");
            foreach (var test in TestAdapter.GetTestsFromFiles(sources))
            {
                discoverySink.SendTestCase(test);
            }
            TestAdapter.Log($"TestDiscoverer.DiscoverTests(): EXIT");
        }
    }
}
