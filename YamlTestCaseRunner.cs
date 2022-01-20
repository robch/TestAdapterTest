using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using YamlDotNet.RepresentationModel;

namespace TestAdapterTest
{
    public class YamlTestCaseRunner
    {
        public static TestOutcome RunAndRecordTestCase(TestCase test, IFrameworkHandle frameworkHandle)
        {
            RunTest(test, out var stdOut, out var stdErr, out string errorMessage, out var additional, out var debugTrace, out var outcome);
            RecordResult(test, frameworkHandle, stdOut, stdErr, errorMessage, additional, debugTrace, outcome);
            return outcome;
        }

        #region private methods

        private static TestOutcome RunTest(TestCase test, out string stdOut, out string stdErr, out string errorMessage, out string additional, out string debugTrace, out TestOutcome outcome)
        {
            var command = TestProperties.Get(test, "command");
            var script = TestProperties.Get(test, "script");
            var expect = TestProperties.Get(test, "expect");
            var notExpect = TestProperties.Get(test, "not-expect");
            var logExpect = TestProperties.Get(test, "log-expect");
            var logNotExpect = TestProperties.Get(test, "log-not-expect");

            var simulate = TestProperties.Get(test, "simulate");
            return string.IsNullOrEmpty(simulate)
                ? RunTestCase(test, command, script, expect, notExpect, logExpect, logNotExpect, out stdOut, out stdErr, out errorMessage, out additional, out debugTrace, out outcome)
                : SimulateTestCase(test, simulate, command, script, expect, notExpect, logExpect, logNotExpect, out stdOut, out stdErr, out errorMessage, out additional, out debugTrace, out outcome);
        }

        private static TestOutcome RunTestCase(TestCase test, string command, string script, string expect, string notExpect, string logExpect, string logNotExpect, out string stdOut, out string stdErr, out string errorMessage, out string additional, out string debugTrace, out TestOutcome outcome)
        {
            additional = $"START TIME: {DateTime.UtcNow}";
            debugTrace = "";

            Task<string> stdOutTask = null;
            Task<string> stdErrTask = null;

            try
            {
                script = WriteTextToTempFile(script);
                expect = WriteTextToTempFile(expect);
                notExpect = WriteTextToTempFile(notExpect);
                logExpect = WriteTextToTempFile(logExpect);
                logNotExpect = WriteTextToTempFile(logNotExpect);

                var args = GetStartArgs(command, script, expect, notExpect, logExpect, logNotExpect);
                var startInfo = new ProcessStartInfo("spx", args)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                var process = Process.Start(startInfo);
                stdOutTask = process.StandardOutput.ReadToEndAsync();
                stdErrTask = process.StandardError.ReadToEndAsync();

                process.WaitForExit();
                outcome = process.ExitCode == 0
                    ? TestOutcome.Passed
                    : TestOutcome.Failed;

                errorMessage = outcome == TestOutcome.Passed
                    ? $"EXIT CODE: {process.ExitCode}"
                    : null;
                additional = ""
                    + $"START TIME: {process.StartTime}"
                    + $" STOP TIME: {process.ExitTime}"
                    + $" EXIT CODE: {process.ExitCode}";
            }
            catch (Exception ex)
            {
                outcome = TestOutcome.Failed;
                errorMessage = ex.Message;
                debugTrace = ex.ToString();
            }
            finally
            {
                if (script != null) File.Delete(script);
                if (expect != null) File.Delete(expect);
                if (notExpect != null) File.Delete(notExpect);
                if (logExpect != null) File.Delete(logExpect);
                if (logNotExpect != null) File.Delete(logNotExpect);
            }

            stdOut = stdOutTask?.Result;
            stdErr = stdErrTask?.Result;

            return outcome;
        }

        private static string WriteTextToTempFile(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, text);
                return tempFile;
            }
            return null;
        }

        private static string GetStartArgs(string command, string script, string expect, string notExpect, string logExpect, string logNotExpect)
        {
            return !string.IsNullOrEmpty(command) || string.IsNullOrEmpty(script)
                ? $"{command} {GetAtArgs(expect, notExpect, logExpect, logNotExpect)}"
                : $"quiet run --script @{script} {GetAtArgs(expect, notExpect, logExpect, logNotExpect)}";
        }

        private static string GetAtArgs(string expect, string notExpect, string logExpect, string logNotExpect)
        {
            var atArgs = $"";
            if (!string.IsNullOrEmpty(expect)) atArgs += $" --expect @{expect}";
            if (!string.IsNullOrEmpty(notExpect)) atArgs += $" --not expect @{notExpect}";
            if (!string.IsNullOrEmpty(logExpect)) atArgs += $" --log expect @{logExpect}";
            if (!string.IsNullOrEmpty(logNotExpect)) atArgs += $" --log not expect @{logNotExpect}";
            return atArgs.TrimStart();
        }

        private static TestOutcome SimulateTestCase(TestCase test, string simulate, string command, string script, string expect, string notExpect, string logExpect, string logNotExpect, out string stdOut, out string stdErr, out string errorMessage, out string additional, out string debugTrace, out TestOutcome outcome)
        {
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
            errorMessage = "ERRORMESSAGE";

            outcome = OutcomeFromString(simulate);
            if (outcome == TestOutcome.Passed)
            {
                stdErr = null;
                debugTrace = null;
                errorMessage = null;
            }

            return outcome;
        }

        private static TestOutcome OutcomeFromString(string simulate)
        {
            TestOutcome outcome = TestOutcome.None;
            switch (simulate?.ToLower())
            {
                case "failed":
                    outcome = TestOutcome.Failed;
                    break;

                case "skipped":
                    outcome = TestOutcome.Skipped;
                    break;

                case "passed":
                    outcome = TestOutcome.Passed;
                    break;
            }

            return outcome;
        }

        private static void RecordResult(TestCase test, IFrameworkHandle frameworkHandle, string stdOut, string stdErr, string errorMessage, string additional, string debugTrace, TestOutcome outcome)
        {
            var result = new TestResult(test) { Outcome = outcome };
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, stdOut));
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, stdErr));
            result.Messages.Add(new TestResultMessage(TestResultMessage.AdditionalInfoCategory, additional));
            result.Messages.Add(new TestResultMessage(TestResultMessage.DebugTraceCategory, debugTrace));
            result.ErrorMessage = errorMessage;

            frameworkHandle.RecordResult(result);
        }

        #endregion
    }
}