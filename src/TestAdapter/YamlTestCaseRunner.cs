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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace TestAdapterTest
{
    public class YamlTestCaseRunner
    {
        public static TestOutcome RunAndRecordTestCase(TestCase test, IFrameworkHandle frameworkHandle)
        {
            TestCaseStart(test, frameworkHandle);
            TestCaseRun(test, frameworkHandle, out TestOutcome outcome);
            TestCaseStop(test, frameworkHandle, outcome);
            return outcome;
        }

        #region private methods

        private static void TestCaseStart(TestCase test, IFrameworkHandle frameworkHandle)
        {
            Logger.Log($"YamlTestCaseRunner.TestCaseStart({test.DisplayName})");
            frameworkHandle.RecordStart(test);
        }

        private static TestOutcome TestCaseRun(TestCase test, IFrameworkHandle frameworkHandle, out TestOutcome outcome) 
        {
            Logger.Log($"YamlTestCaseRunner.TestCaseRun({test.DisplayName})");
            
            // run the test case, getting all the results, prior to recording any of those results
            // (not doing this in this order seems to, for some reason, cause "foreach" test cases to run 5 times!?)
            var results = TestCaseGetResults(test).ToList();
            foreach (var result in results)
            {
                frameworkHandle.RecordResult(result);
            }

            var failed = results.Count(x => x.Outcome == TestOutcome.Failed) > 0;
            var skipped = results.Count(x => x.Outcome == TestOutcome.Skipped) > 0;
            var notFound = results.Count(x => x.Outcome == TestOutcome.NotFound) > 0 || results.Count() == 0;

            return outcome =
                failed ? TestOutcome.Failed
                : skipped ? TestOutcome.Skipped
                : notFound ? TestOutcome.NotFound
                : TestOutcome.Passed;
        }

        private static IEnumerable<TestResult> TestCaseGetResults(TestCase test)
        {
            Logger.Log($"YamlTestCaseRunner.TestCaseGetResults: ENTER");

            var command = YamlTestProperties.Get(test, "command");
            var script = YamlTestProperties.Get(test, "script");
            var @foreach = YamlTestProperties.Get(test, "foreach");
            var arguments = YamlTestProperties.Get(test, "arguments");
            var expect = YamlTestProperties.Get(test, "expect");
            var notExpect = YamlTestProperties.Get(test, "not-expect");
            var workingDirectory = YamlTestProperties.Get(test, "working-directory");
            var simulate = YamlTestProperties.Get(test, "simulate");

            var expanded = ExpandForEachGroups(@foreach);
            Logger.Log($"YamlTestCaseRunner.TestCaseGetResults: expanded count = {expanded.Count()}");

            foreach (var foreachItem in expanded)
            {
                var start = DateTime.Now;

                var outcome = string.IsNullOrEmpty(simulate)
                    ? RunTestCase(test, command, script, foreachItem, arguments, expect, notExpect, workingDirectory, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace)
                    : SimulateTestCase(test, simulate, command, script, foreachItem, arguments, expect, notExpect, workingDirectory, out stdOut, out stdErr, out errorMessage, out stackTrace, out additional, out debugTrace);

                #if DEBUG
                additional += outcome == TestOutcome.Failed ? $"\nEXTRA: {ExtraDebugInfo()}" : "";
                #endif

                var stop = DateTime.Now;
                var result = CreateTestResult(test, start, stop, stdOut, stdErr, errorMessage, stackTrace, additional, debugTrace, outcome);
                if (!string.IsNullOrEmpty(foreachItem) && foreachItem != "{}")
                {
                    result.DisplayName = $"{test.DisplayName}: {RedactSensitiveDataFromForeachItem(foreachItem)}";
                }
                yield return result;
            }

            Logger.Log($"YamlTestCaseRunner.TestCaseGetResults: EXIT");
        }

        // Finds "token" in foreach key and redacts its value
        private static string RedactSensitiveDataFromForeachItem(string foreachItem)
        {
            var foreachObject = JObject.Parse(foreachItem);
            
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw){Formatting = Formatting.None})
            {
                writer.WriteStartObject();
                foreach (var item in foreachObject)
                {
                    if (string.IsNullOrWhiteSpace(item.Value.ToString()))
                    {
                        continue;
                    }
                    var keys = item.Key.ToLower().Split("\t", StringSplitOptions.RemoveEmptyEntries);
                    
                    // find index of "token" in foreach key and redact its value to avoid getting it displayed
                    var tokenIndex = Array.IndexOf(keys, "token");
                    var valueString = item.Value;
                    
                    if (tokenIndex >= 0)
                    {
                        var values = item.Value.ToString().Split("\t", StringSplitOptions.RemoveEmptyEntries);
                        if (values.Count() == keys.Count())
                        {
                            values[tokenIndex] = "***";
                            valueString = string.Join("\t", values);
                        }
                    }
                    writer.WritePropertyName(item.Key);
                    writer.WriteValue(valueString);
                }

                writer.WriteEndObject();
            }

            return sb.ToString();
        }

        private static IEnumerable<string> ExpandForEachGroups(string @foreach)
        {
            var kvs = KeyValuePairsFromJson(@foreach, false)
                .Select(kv => new KeyValuePair<string, IEnumerable<string>>(
                    kv.Key,
                    kv.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries)));

            var dicts = new[] { new Dictionary<string, string>() }.ToList();
            foreach (var item in kvs)
            {
                var lines = item.Value;
                dicts = lines.SelectMany(
                    line => dicts.Select(
                        d => DupAndAdd(d, item.Key, line)))
                    .ToList();
            }

            return dicts.Select(d => JsonConvert.SerializeObject(d));
        }

        private static Dictionary<string, string> DupAndAdd(Dictionary<string, string> d, string key, string value)
        {
            var dup = new Dictionary<string, string>(d);
            dup[key] = value;
            return dup;
        }

        private static TestOutcome RunTestCase(TestCase test, string command, string script, string @foreach, string arguments, string expect, string notExpect, string workingDirectory, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace)
        {
            var outcome = TestOutcome.None;

            additional = $"START TIME: {DateTime.Now}";
            debugTrace = "";
            stackTrace = script;

            Task<string> stdOutTask = null;
            Task<string> stdErrTask = null;
            List<string> filesToDelete = null;

            try
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                script = WriteTextToTempFile(script, isWindows ? "cmd" : null);

                expect = WriteTextToTempFile(expect);
                notExpect = WriteTextToTempFile(notExpect);

                var kvs = KeyValuePairsFromJson(arguments, true);
                kvs.AddRange(KeyValuePairsFromJson(@foreach, false));
                kvs = ConvertValuesToAtArgs(kvs, ref filesToDelete);

                var startArgs = GetStartInfo(out string startProcess, command, script, kvs, expect, notExpect, ref filesToDelete);
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
                filesToDelete?.ForEach(x => File.Delete(x));
            }

            stdOut = stdOutTask?.Result;
            stdErr = stdErrTask?.Result;

            return outcome;
        }

        private static List<KeyValuePair<string, string>> ConvertValuesToAtArgs(List<KeyValuePair<string, string>> kvs, ref List<string> files)
        {
            var newList = new List<KeyValuePair<string, string>>();
            foreach (var item in kvs)
            {
                Logger.Log($"YamlTestCaseRunner.ConvertValuesToAtArgs: item.Value={item.Value}");
                Logger.Log($"YamlTestCaseRunner.ConvertValuesToAtArgs: item.Value should convert to file ={item.Value.Count(x => x == '\t' || x == '\r' || x == '\n' || x == '\f' || x == '\"') > 0}");
                
                if (item.Value.Count(x => x == '\t' || x == '\r' || x == '\n' || x == '\f' || x == '\"') > 0)
                {
                    string file = WriteMultilineTsvToTempFile(item.Value, ref files);
                    newList.Add(new KeyValuePair<string, string>(item.Key, $"@{file}"));
                }
                else
                {
                    newList.Add(item);
                }
            }

            return newList;
        }

        private static List<KeyValuePair<string, string>> KeyValuePairsFromJson(string json, bool allowSimpleString)
        {
            var kvs = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrEmpty(json))
            {
                Logger.Log($"KeyValuePairsFromJson: 'json'='{json}'");
                var parsed = JToken.Parse(json);
                if (parsed.Type == JTokenType.String && allowSimpleString)
                {
                    // if it's a simple string, there is no "key" for the argument... pass it as value with an empty string as key
                    // this will ensure that an additional '--' isn't emitted preceding the string-only arguments
                    kvs.Add(new KeyValuePair<string, string>("", parsed.Value<string>()));
                }
                else if (parsed.Type != JTokenType.Object)
                {
                    // if it's not a simple string, it must be an object... if it's not, we'll just log and continue
                    Logger.Log("KeyValuePairsFromJson: Invalid json (only supports `\"string\"`, or `{\"mapItem1\": \"value1\", \"...\": \"...\"}`!");
                }
                else
                {
                    foreach (var item in parsed as JObject)
                    {
                        kvs.Add(new KeyValuePair<string, string>(item.Key, item.Value.Value<string>()));
                    }
                }
            }
            return kvs;
        }

        private static string WriteMultilineTsvToTempFile(string text, ref List<string> files)
        {
            files ??= new List<string>();

            var lines = text.Split('\r', '\n');
            var newLines = new List<string>();
            foreach (var line in lines)
            {
                if (!line.Contains('\f'))
                {
                    newLines.Add(line);
                    continue;
                }

                var values = line.Split('\t');
                var newValues = new List<string>();
                foreach (var value in values)
                {
                    if (!value.Contains('\f'))
                    {
                        newValues.Add(value);
                        continue;
                    }

                    var newValue = WriteTextToTempFile(value.Replace('\f', '\n'));
                    files.Add(newValue);

                    newValues.Add($"@{newValue}");
                }

                newLines.Add(string.Join("\t", newValues));
            }

            var newText = string.Join("\n", newLines);
            var file = WriteTextToTempFile(newText);
            files.Add(file);
            return file;
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

                var content = File.ReadAllText(tempFile).Replace("\n", "\\n");
                Logger.Log($"FILE: {tempFile}: '{content}'");

                return tempFile;
            }
            return null;
        }

        private static string FindCli(string cli)
        {
            var dll = $"{cli}.dll";
            var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{cli}.exe" : cli;

            var path1 = Environment.GetEnvironmentVariable("PATH");
            var path2 = Directory.GetCurrentDirectory();
            var path3 = (new FileInfo(typeof(YamlTestCaseRunner).Assembly.Location)).DirectoryName;
            var path = $"{path3}{Path.PathSeparator}{path2}{Path.PathSeparator}{path1}";

            var paths = path.Split(Path.PathSeparator);
            var found = paths
                .Where(x => File.Exists(Path.Combine(x, dll)) && File.Exists(Path.Combine(x, exe)))
                .Select(x => Path.Combine(x, exe))
                .FirstOrDefault();
            if (string.IsNullOrEmpty(found))
            {
                found = paths
                    .Where(x => File.Exists(Path.Combine(x, "net6.0", dll)) && File.Exists(Path.Combine(x, "net6.0", dll)))
                    .Select(x => Path.Combine(x, "net6.0", exe))
                    .FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(found))
            {
                Logger.Log($"FindCli: Found {cli}={found}");
                return found;
            }

            var message = $"FindCli: Not found! ... using {cli}";
            Logger.LogWarning(message);
            Logger.TraceWarning(message);

            return cli;
        }

        private static string GetStartInfo(out string startProcess, string command, string script, List<KeyValuePair<string, string>> kvs, string expect, string notExpect, ref List<string> files)
        {
            var cli = "spx";
            startProcess = FindCli(cli);

            var isCommand = !string.IsNullOrEmpty(command) || string.IsNullOrEmpty(script);
            if (isCommand)
            {
                command = $"{command} {GetKeyValueArgs(kvs)}";

                var expectLess = string.IsNullOrEmpty(expect) && string.IsNullOrEmpty(notExpect);
                if (expectLess) return command;

                command = WriteTextToTempFile(command);
                files ??= new List<string>();
                files.Add(command);

                return $"quiet run --command @{command} {GetAtArgs(expect, notExpect)}";
            }

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return isWindows
                ? $"quiet run --cmd --script {script} {GetKeyValueArgs(kvs)} {GetAtArgs(expect, notExpect)}"
                : $"quiet run --process /bin/bash --pre.script -l --script {script} {GetKeyValueArgs(kvs)} {GetAtArgs(expect, notExpect)}";
        }

        private static string GetAtArgs(string expect, string notExpect)
        {
            var atArgs = $"";
            if (!string.IsNullOrEmpty(expect)) atArgs += $" --expect @{expect}";
            if (!string.IsNullOrEmpty(notExpect)) atArgs += $" --not expect @{notExpect}";
            return atArgs.TrimStart(' ');
        }

        private static string GetKeyValueArgs(List<KeyValuePair<string, string>> kvs)
        {
            var args = new StringBuilder();
            foreach (var item in kvs)
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    if (item.Key.Contains('\t'))
                    {
                        var key = item.Key.Replace('\t', ';');
                        args.Append($"--foreach {key} in ");
                    }
                    else
                    {
                        args.Append($"--{item.Key} ");
                    }
                    
                    if (!string.IsNullOrEmpty(item.Value))
                    {
                        args.Append($"\"{item.Value}\" ");
                    }
                }
                else if (!string.IsNullOrEmpty(item.Value))
                {
                    args.Append(item.Value);
                }
            }
            return args.ToString().TrimEnd();
        }

        private static TestOutcome SimulateTestCase(TestCase test, string simulate, string command, string script, string @foreach, string arguments, string expect, string notExpect, string workingDirectory, out string stdOut, out string stdErr, out string errorMessage, out string stackTrace, out string additional, out string debugTrace)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"command='{command?.Replace("\n", "\\n")}'");
            sb.AppendLine($"script='{script?.Replace("\n", "\\n")}'");
            sb.AppendLine($"foreach='{@foreach?.Replace("\n", "\\n")}'");
            sb.AppendLine($"arguments='{arguments?.Replace("\n", "\\n")}'");
            sb.AppendLine($"expect='{expect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"not-expect='{notExpect?.Replace("\n", "\\n")}'");
            sb.AppendLine($"working-directory='{workingDirectory}'");

            stdOut = sb.ToString();
            stdErr = "STDERR";
            additional = "ADDITIONAL-INFO";
            debugTrace = "DEBUG-TRACE";
            errorMessage = "ERRORMESSAGE";
            stackTrace = "STACKTRACE";

            var outcome = OutcomeFromString(simulate);
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

        private static void TestCaseStop(TestCase test, IFrameworkHandle frameworkHandle, TestOutcome outcome)
        {
            Logger.Log($"YamlTestCaseRunner.TestCaseStop({test.DisplayName})");
            frameworkHandle.RecordEnd(test, outcome);
        }

        private static TestResult CreateTestResult(TestCase test, DateTime start, DateTime stop, string stdOut, string stdErr, string errorMessage, string stackTrace, string additional, string debugTrace, TestOutcome outcome)
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

            return result;
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
