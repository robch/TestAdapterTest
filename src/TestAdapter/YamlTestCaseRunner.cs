using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
            var start = DateTime.UtcNow;
            TestStart(test, frameworkHandle);
            TestRun(test, out var stdOut, out var stdErr, out string errorMessage, out string stackTrace, out var additional, out var debugTrace, out var outcome);
            TestStop(test, frameworkHandle, outcome);
            var stop = DateTime.UtcNow;

            TestRecordResult(test, frameworkHandle, start, stop, stdOut, stdErr, errorMessage, stackTrace, additional, debugTrace, outcome);
            return outcome;
        }

        #region private methods

        private static void TestStart(TestCase test, IFrameworkHandle frameworkHandle)
        {
            Logger.Log($"YamlTestCaseRunner.TestStart({test.DisplayName})");
            frameworkHandle.RecordStart(test);
        }

        private static TestOutcome TestRun(TestCase test, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace, out TestOutcome outcome)
        {
            var command = YameTestProperties.Get(test, "command");
            var script = YameTestProperties.Get(test, "script");
            var expect = YameTestProperties.Get(test, "expect");
            var notExpect = YameTestProperties.Get(test, "not-expect");
            var logExpect = YameTestProperties.Get(test, "log-expect");
            var logNotExpect = YameTestProperties.Get(test, "log-not-expect");

            var simulate = YameTestProperties.Get(test, "simulate");
            return string.IsNullOrEmpty(simulate)
                ? RunTestCase(test, command, script, expect, notExpect, logExpect, logNotExpect, out stdOut, out stdErr, out errorMessage, out stackTrace, out additional, out debugTrace, out outcome)
                : SimulateTestCase(test, simulate, command, script, expect, notExpect, logExpect, logNotExpect, out stdOut, out stdErr, out errorMessage, out stackTrace, out additional, out debugTrace, out outcome);
        }

        private static TestOutcome RunTestCase(TestCase test, string command, string script, string expect, string notExpect, string logExpect, string logNotExpect, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace, out TestOutcome outcome)
        {
            additional = $"START TIME: {DateTime.UtcNow}";
            debugTrace = "";
            stackTrace = script;

            Task<string> stdOutTask = null;
            Task<string> stdErrTask = null;

            try
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                script = WriteTextToTempFile(script, isWindows ? "cmd" : "sh");

                expect = WriteTextToTempFile(expect);
                notExpect = WriteTextToTempFile(notExpect);
                logExpect = WriteTextToTempFile(logExpect);
                logNotExpect = WriteTextToTempFile(logNotExpect);

                var args = GetStartArgs(command, script, expect, notExpect, logExpect, logNotExpect);
                stackTrace = stackTrace ?? $"spx {args}";

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

                errorMessage = $"EXIT CODE: {process.ExitCode}";
                additional = additional
                    + $" STOP TIME: {process.ExitTime}"
                    + $" EXIT CODE: {process.ExitCode}";
            }
            catch (Exception ex)
            {
                outcome = TestOutcome.Failed;
                errorMessage = ex.Message;
                debugTrace = ex.ToString();
                stackTrace = $"{stackTrace}\n{ex.StackTrace.ToString()}\n{ExtraDebugInfo()}";
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

        private static string WriteTextToTempFile(string text, string extension = null)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var tempFile = Path.GetTempFileName();
                if (!string.IsNullOrEmpty(extension))
                {
                    tempFile = $"{tempFile}.{extension}";
                }

                File.WriteAllText(tempFile, text);
                return tempFile;
            }
            return null;
        }

        private static string GetStartArgs(string command, string script, string expect, string notExpect, string logExpect, string logNotExpect)
        {
            return !string.IsNullOrEmpty(command) || string.IsNullOrEmpty(script)
                ? $"{command} {GetAtArgs(expect, notExpect, logExpect, logNotExpect)}"
                : $"quiet run --script {script} {GetAtArgs(expect, notExpect, logExpect, logNotExpect)}";
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

        private static TestOutcome SimulateTestCase(TestCase test, string simulate, string command, string script, string expect, string notExpect, string logExpect, string logNotExpect, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace, out TestOutcome outcome)
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
            stackTrace = "STACKTRACE";

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

        private static void TestStop(TestCase test, IFrameworkHandle frameworkHandle, TestOutcome outcome)
        {
            Logger.Log($"YamlTestCaseRunner.TestEnd({test.DisplayName})");
            frameworkHandle.RecordEnd(test, outcome);
        }

        private static void TestRecordResult(TestCase test, IFrameworkHandle frameworkHandle, DateTime start, DateTime stop, string stdOut, string stdErr, string errorMessage, string stackTrace, string additional, string debugTrace, TestOutcome outcome)
        {
            Logger.Log($"YamlTestCaseRunner.TestRecordResult({test.DisplayName})");

            var result = new TestResult(test) { Outcome = outcome };
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, stdOut));
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, stdErr));
            result.Messages.Add(new TestResultMessage(TestResultMessage.AdditionalInfoCategory, additional));
            result.Messages.Add(new TestResultMessage(TestResultMessage.DebugTraceCategory, debugTrace));
            result.ErrorMessage = errorMessage;
            result.ErrorStackTrace = stackTrace;
            result.StartTime = start;
            result.EndTime = stop;
            result.Duration = stop - start;

            frameworkHandle.RecordResult(result);
        }

        private static string ExtraDebugInfo()
        {
            var sb = new StringBuilder();

            var cwd = Directory.GetCurrentDirectory();
            sb.AppendLine($"CURRENT DIRECTORY: {cwd}");

            var files = Directory.GetFiles(cwd, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                sb.AppendLine(file);
            }

            return sb.ToString();
        }

        #endregion
    }
}
