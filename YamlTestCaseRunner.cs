using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using YamlDotNet.RepresentationModel;

namespace TestAdapterTest
{
    public class YamlTestCaseRunner
    {
        public static TestOutcome RunAndRecordTestCase(TestCase test, IFrameworkHandle frameworkHandle)
        {
            RunTest(test, out var stdOut, out var stdErr, out var additional, out var debugTrace, out var outcome);
            RecordResult(test, frameworkHandle, stdOut, stdErr, additional, debugTrace, outcome);
            return outcome;
        }

        private static void RunTest(TestCase test, out string stdOut, out string stdErr, out string additional, out string debugTrace, out TestOutcome outcome)
        {
            var command = TestCaseProperties.Get(test, "command");
            var script = TestCaseProperties.Get(test, "script");
            var expect = TestCaseProperties.Get(test, "expect");
            var notExpect = TestCaseProperties.Get(test, "not-expect");
            var logExpect = TestCaseProperties.Get(test, "log-expect");
            var logNotExpect = TestCaseProperties.Get(test, "log-not-expect");

            var sb = new StringBuilder();
            sb.AppendLine($"command='{command?.Replace("\n", "\\n")}'");
            sb.AppendLine($"script='{script?.Replace("\n", "\\n")}'");
            sb.AppendLine($"expect='{expect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"not-expect='{notExpect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"log-expect='{logExpect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"log-not-expect='{logNotExpect?.Replace("\n", "\\n")}'");
            stdOut = sb.ToString();
            stdErr = "STDERR";
            additional = "ADDITIONAL-INFO";
            debugTrace = "DEBUG-TRACE";

            outcome = test.DisplayName.Contains("2") ? TestOutcome.Failed : TestOutcome.Passed;
            if (outcome == TestOutcome.Passed)
            {
                stdErr = null;
                debugTrace = null;
            }
        }

        private static void RecordResult(TestCase test, IFrameworkHandle frameworkHandle, string stdOut, string stdErr, string additional, string debugTrace, TestOutcome outcome)
        {
            var result = new TestResult(test) { Outcome = outcome };
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, stdOut));
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, stdErr));
            result.Messages.Add(new TestResultMessage(TestResultMessage.AdditionalInfoCategory, additional));
            result.Messages.Add(new TestResultMessage(TestResultMessage.DebugTraceCategory, debugTrace));

            frameworkHandle.RecordResult(result);
        }
    }
}