using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using YamlDotNet.RepresentationModel;

namespace TestAdapterTest
{
    public class YamlTestCaseRunner
    {
        public static TestOutcome RunTestCase(TestCase test, IFrameworkHandle frameworkHandle)
        {
            var outcome = test.DisplayName.Contains("2") ? TestOutcome.Failed : TestOutcome.Passed;

            var result = new TestResult(test) { Outcome = outcome };
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, "STDOUT stuff goes here!!"));
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, "STDERR stuff goes here!!"));
            result.Messages.Add(new TestResultMessage(TestResultMessage.AdditionalInfoCategory, "ADDITIONAL-INFO stuff goes here!!"));
            result.Messages.Add(new TestResultMessage(TestResultMessage.DebugTraceCategory, "DEBUG-TRACE stuff goes here!!"));

            frameworkHandle.RecordResult(result);
            return outcome;
        }
    }
}