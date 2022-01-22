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

            var rootNamespace = GetRootNamespace(file);
            return TestCasesFromYamlSequence(source, file, sequence, rootNamespace, defaultClassName);
        }

        #region private methods

        private static YamlStream ParseYamlStream(string fullName)
        {
            var stream = new YamlStream();
            stream.Load(File.OpenText(fullName));
            return stream;
        }

        private static IEnumerable<TestCase> TestCasesFromYamlSequence(string source, FileInfo file, YamlSequenceNode sequence, string @namespace, string @class)
        {
            var tests = new List<TestCase>();
            foreach (YamlMappingNode mapping in sequence?.Children)
            {
                var test = GetTestFromNode(source, file, mapping, @namespace, @class);
                if (test != null)
                {
                    tests.Add(test);
                    continue;
                }

                var children = CheckForChildren(source, file, mapping, @namespace, @class);
                if (children != null)
                {
                    tests.AddRange(children);
                    continue;
                }
            }
            return tests;
        }

        private static TestCase GetTestFromNode(string source, FileInfo file, YamlMappingNode mapping, string @namespace, string @class)
        {
            string command = GetScalarString(mapping, "command");
            string script = GetScalarString(mapping, "script");
            var bothOrNeither = (command == null) == (script == null);
            if (bothOrNeither) return null;

            string fullyQualifiedName = GetFullyQualifiedName(mapping, @namespace, @class);
            if (fullyQualifiedName == null) return null;

            Logger.Log($"YamlTestParser::GetTests(): new TestCase('{fullyQualifiedName}')");
            var test = new TestCase(fullyQualifiedName, new Uri(TestAdapter.Executor), source)
            {
                CodeFilePath = file.FullName,
                LineNumber = mapping.Start.Line
            };

            SetTestCaseProperty(test, "command", command);
            SetTestCaseProperty(test, "script", script);

            SetTestCaseProperty(test, "expect", mapping, "expect");
            SetTestCaseProperty(test, "not-expect", mapping, "not-expect");
            SetTestCaseProperty(test, "log-expect", mapping, "log-expect");
            SetTestCaseProperty(test, "log-not-expect", mapping, "log-not-expect");
            SetTestCaseProperty(test, "simulate", mapping, "simulate");

            CheckInvalidTestCaseNodes(file, mapping, test);
            return test;
        }

        private static IEnumerable<TestCase> CheckForChildren(string source, FileInfo file, YamlMappingNode mapping, string @namespace, string @class)
        {
            var sequence = mapping.Children.ContainsKey("tests")
                ? mapping.Children["tests"] as YamlSequenceNode
                : null;
            if (sequence == null) return null;

            @class = GetScalarString(mapping, "class", @class);
            @namespace = UpdateNamespace(mapping, @namespace);

            return TestCasesFromYamlSequence(source, file, sequence, @namespace, @class);
        }

        private static void CheckInvalidTestCaseNodes(FileInfo file, YamlMappingNode mapping, TestCase test)
        {
            foreach (YamlScalarNode key in mapping.Children.Keys)
            {
                if (";namespace;class;name;command;script;expect;not-expect;log-expect;log-not-expect;simulate;".IndexOf($";{key.Value};") < 0)
                {
                    var error = $"**** Unexpected YAML node ('{key.Value}') in {file.FullName}({mapping[key].Start.Line})";
                    test.DisplayName = error;
                    Logger.Log(error);
                }
            }
        }

        private static void SetTestCaseProperty(TestCase test, string propertyName, YamlMappingNode mapping, string mappingName)
        {
            string value = GetScalarString(mapping, mappingName);
            SetTestCaseProperty(test, propertyName, value);
        }

        private static void SetTestCaseProperty(TestCase test, string propertyName, string value)
        {
            if (value != null)
            {
                TestProperties.Set(test, propertyName, value);
            }
        }

        private static string GetScalarString(YamlMappingNode mapping, string mappingName, string defaultValue = null)
        {
            var ok = mapping.Children.ContainsKey(mappingName);
            if (!ok) return defaultValue;

            var node = mapping.Children[mappingName] as YamlScalarNode;
            var value = node?.Value;

            return value ?? defaultValue;
        }

        private static string GetRootNamespace(FileInfo file)
        {
            return $"{file.Extension.TrimStart('.')}.{file.Name.Remove(file.Name.LastIndexOf(file.Extension))}";
        }

        private static string UpdateNamespace(YamlMappingNode mapping, string @namespace)
        {
            var subNamespace = GetScalarString(mapping, "namespace");
            return string.IsNullOrEmpty(subNamespace)
                ? @namespace
                : $"{@namespace}.{subNamespace}";
        }

        private static string GetFullyQualifiedName(YamlMappingNode mapping, string @namespace, string @class)
        {
            var name = GetScalarString(mapping, "name");
            if (name == null) return null;

            @namespace = UpdateNamespace(mapping, @namespace);
            @class = GetScalarString(mapping, "class", @class);

            return GetFullyQualifiedName(@namespace, @class, name);
        }

        private static string GetFullyQualifiedName(string @namespace, string @class, string name)
        {
            return $"{@namespace}.{@class}.{name}";
        }

        private const string defaultClassName = "TestCases";

        #endregion
    }
}