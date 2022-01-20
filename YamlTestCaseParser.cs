using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using YamlDotNet.RepresentationModel;

namespace TestAdapterTest
{
    public class YamlTestCaseParser
    {
        public static IEnumerable<TestCase> TestCasesFromYaml(string source, FileInfo file)
        {
            var parsed = ParseYamlStream(file.FullName);
            var sequence = parsed?.Documents?[0].RootNode as YamlSequenceNode;
            return TestCasesFromYamlSequence(source, file, sequence);
        }

        #region private methods
        
        private static YamlStream ParseYamlStream(string fullName)
        {
            var stream = new YamlStream();
            stream.Load(File.OpenText(fullName));
            return stream;
        }

        private static IEnumerable<TestCase> TestCasesFromYamlSequence(string source, FileInfo file, YamlSequenceNode sequence)
        {
            foreach (YamlMappingNode mapping in sequence?.Children)
            {
                yield return TestCaseFromYamlMapping(source, file, mapping);
            }
        }

        private static TestCase TestCaseFromYamlMapping(string source, FileInfo file, YamlMappingNode mapping)
        {
            string fullName = GetTestCaseFullName(source, file, mapping);

            Logger.Log($"YamlTestParser::GetTests(): new TestCase('{fullName}')");
            var test = new TestCase(fullName, new Uri(TestAdapter.Executor), source)
            {
                CodeFilePath = file.FullName,
                LineNumber = mapping.Start.Line
            };

            SetTestCaseProperty(test, "command", mapping, "command");
            SetTestCaseProperty(test, "script", mapping, "script");
            SetTestCaseProperty(test, "expect", mapping, "expect");
            SetTestCaseProperty(test, "not-expect", mapping, "not-expect");
            SetTestCaseProperty(test, "log-expect", mapping, "log-expect");
            SetTestCaseProperty(test, "log-not-expect", mapping, "log-not-expect");
            SetTestCaseProperty(test, "simulate", mapping, "simulate");
            CheckBadNodes(file, mapping, test);

            return test;
        }

        private static void CheckBadNodes(FileInfo file, YamlMappingNode mapping, TestCase test)
        {
            foreach (YamlScalarNode key in mapping.Children.Keys)
            {
                if (";name;command;script;expect;not-expect;log-expect;log-not-expect;simulate;".IndexOf($";{key.Value};") < 0)
                {
                    var error = $"**** Unexpected YAML node ('{key.Value}') in {file.FullName}({mapping[key].Start.Line})";
                    test.DisplayName = error;
                    Logger.Log(error);
                }
            }
        }

        private static string GetTestCaseFullName(string source, FileInfo file, YamlMappingNode mapping)
        {
            string name = GetTestCaseName(mapping);
            string prefix = GetTestCaseNamePrefix(file);
            var fullName = $"{prefix}.{name}";

            #if DEBUG
                int milli = (int)DateTime.Now.TimeOfDay.TotalMilliseconds;
                var extra = (milli % 10).ToString().Replace(".", "");
                fullName = $"{fullName} {extra}";
            #endif

            return fullName;
        }

        private static string GetTestCaseName(YamlMappingNode mapping)
        {
            var nameNode = mapping.Children?["name"] as YamlScalarNode;
            var nameValue = nameNode?.Value;
            return nameValue;
        }

        private static string GetTestCaseNamePrefix(FileInfo file)
        {
            var name = file.FullName.Remove(file.FullName.LastIndexOf(file.Extension)).Replace(" ", "").Trim('.');
            var parts = name.Split(":/\\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var tailParts = parts.Reverse().Take(5).Reverse();
            var prefix = string.Join(".", tailParts);
            return prefix;
        }

        private static void SetTestCaseProperty(TestCase test, string propertyName, YamlMappingNode mapping, string mappingName)
        {
            var ok = mapping.Children.ContainsKey(mappingName);
            if (!ok) return;

            var node = mapping.Children[mappingName] as YamlScalarNode;
            var value = node?.Value;

            TestProperties.Set(test, propertyName, value);
        }

        #endregion
    }
}