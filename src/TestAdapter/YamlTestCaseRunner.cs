using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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

            #if DEBUG
            additional += outcome == TestOutcome.Failed ? $"\nEXTRA: {ExtraDebugInfo()}" : "";
            #endif

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
            var command = YamlTestProperties.Get(test, "command");
            var script = YamlTestProperties.Get(test, "script");
            var expect = YamlTestProperties.Get(test, "expect");
            var notExpect = YamlTestProperties.Get(test, "not-expect");
            var logExpect = YamlTestProperties.Get(test, "log-expect");
            var logNotExpect = YamlTestProperties.Get(test, "log-not-expect");
            var workingDirectory = YamlTestProperties.Get(test, "working-directory");

            var simulate = YamlTestProperties.Get(test, "simulate");
            return string.IsNullOrEmpty(simulate)
                ? RunTestCase(test, command, script, expect, notExpect, logExpect, logNotExpect, workingDirectory, out stdOut, out stdErr, out errorMessage, out stackTrace, out additional, out debugTrace, out outcome)
                : SimulateTestCase(test, simulate, command, script, expect, notExpect, logExpect, logNotExpect, workingDirectory, out stdOut, out stdErr, out errorMessage, out stackTrace, out additional, out debugTrace, out outcome);
        }

        private static TestOutcome RunTestCase(TestCase test, string command, string script, string expect, string notExpect, string logExpect, string logNotExpect, string workingDirectory, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace, out TestOutcome outcome)
        {
            additional = $"START TIME: {DateTime.UtcNow}";
            debugTrace = "";
            stackTrace = script;

            Task<string> stdOutTask = null;
            Task<string> stdErrTask = null;

            try
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                script = WriteTextToTempFile(script, isWindows ? "cmd" : null);

                expect = WriteTextToTempFile(expect);
                notExpect = WriteTextToTempFile(notExpect);
                logExpect = WriteTextToTempFile(logExpect);
                logNotExpect = WriteTextToTempFile(logNotExpect);

                var startArgs = GetStartInfo(out string startProcess, command, script, expect, notExpect, logExpect, logNotExpect);
                stackTrace = stackTrace ?? $"{startProcess} {startArgs}";

                Logger.Log($"Process.Start('{startProcess} {startArgs}')");
                var startInfo = new ProcessStartInfo(startProcess, startArgs)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = workingDirectory
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
                stackTrace = $"{stackTrace}\n{ex.StackTrace}";
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

                #if DEBUG
                    var content = File.ReadAllText(tempFile).Replace("\n", "\\n");
                    Logger.Log($"FILE: {tempFile}: '{content}'");
                #endif

                return tempFile;
            }
            return null;
        }

        private static string GetStartInfo(out string startProcess, string command, string script, string expect, string notExpect, string logExpect, string logNotExpect)
        {
            startProcess = "spx";
            
            var isCommand = !string.IsNullOrEmpty(command) || string.IsNullOrEmpty(script);
            if (isCommand) return $"{command} {GetAtArgs(expect, notExpect, logExpect, logNotExpect)}";

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return isWindows
                ? $"quiet run --cmd --script {script} {GetAtArgs(expect, notExpect, logExpect, logNotExpect)}"
                : $"quiet run --process /bin/bash --pre.script -l --script {script} {GetAtArgs(expect, notExpect, logExpect, logNotExpect)}";
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

        private static TestOutcome SimulateTestCase(TestCase test, string simulate, string command, string script, string expect, string notExpect, string logExpect, string logNotExpect, string workingDirectory, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace, out TestOutcome outcome)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"command='{command?.Replace("\n", "\\n")}'");
            sb.AppendLine($"script='{script?.Replace("\n", "\\n")}'");
            sb.AppendLine($"expect='{expect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"not-expect='{notExpect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"log-expect='{logExpect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"log-not-expect='{logNotExpect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"working-directory='{workingDirectory}'");

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

            Logger.Log("----------------------------\n\n");
            Logger.Log($"    STDOUT: {stdOut}");
            Logger.Log($"    STDERR: {stdErr}");
            Logger.Log($"     STACK: {stackTrace}");
            Logger.Log($"     ERROR: {errorMessage}");
            Logger.Log($"   OUTCOME: {outcome}");
            Logger.Log($"ADDITIONAL: {additional}");
            Logger.Log($"DEBUGTRACE: {debugTrace}");
            Logger.Log("----------------------------\n\n");

            frameworkHandle.RecordResult(result);
        }

        private static string ExtraDebugInfo()
        {
            var sb = new StringBuilder();

            var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
            sb.AppendLine($"CURRENT DIRECTORY: {cwd.FullName}");

            var files = cwd.GetFiles("*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                sb.AppendLine($"{file.Length,10}   {file.CreationTime.Date:MM/dd/yyyy}   {file.CreationTime:hh:mm:ss tt}   {file.FullName}");
            }

            var variables = Environment.GetEnvironmentVariables();
            var keys = new List<string>(variables.Count);
            foreach (var key in variables.Keys) keys.Add(key as string);

            keys.Sort();
            foreach (var key in keys)
            {
                var value = variables[key] as string;
                sb.AppendLine($"{key,-20}  {value}");
            }

            return sb.ToString();
        }

        #endregion
    }
}