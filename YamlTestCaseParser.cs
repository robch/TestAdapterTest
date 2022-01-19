using System;
using System.Collections.Generic;
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

            TestAdapter.Log($"YamlTestParser::GetTests(): new TestCase('{fullName}')");
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

            return test;
        }

        private static string GetTestCaseFullName(string source, FileInfo file, YamlMappingNode mapping)
        {
            string name = GetTestCaseName(mapping);
            string prefix = GetTestCaseNamePrefix(file);
            var fullName = $"{prefix}.{name}";
            
            #if DEBUG
            fullName = $"{fullName} {DateTime.Now.TimeOfDay.TotalMilliseconds.ToString().Replace(".", "")}";
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

            if (!string.IsNullOrEmpty(value))
            {
                var property = properties[propertyName];
                test.SetPropertyValue(property, value);
                TestAdapter.Log($"SetTestCaseProperty('{propertyName}'='{value.Replace("\n", "\\n")}')");
            }
        }

        private static TestProperty RegisterTestCaseProperty(string name)
        {
            return TestProperty.Register($"YamlTestCase.{name}", name, typeof(string), TestPropertyAttributes.Hidden, typeof(TestCase));
        }

        private static readonly Dictionary<string, TestProperty> properties = new Dictionary<string, TestProperty>() {
            { "command", RegisterTestCaseProperty("Command") },
            { "script", RegisterTestCaseProperty("Script") },
            { "expect", RegisterTestCaseProperty("Expect") },
            { "not-expect", RegisterTestCaseProperty("NotExpect") },
            { "log-expect", RegisterTestCaseProperty("LogExpect") },
            { "log-not-expect", RegisterTestCaseProperty("LogNotExpect") }
        };
    }
}