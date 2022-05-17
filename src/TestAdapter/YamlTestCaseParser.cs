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

            var rootArea = GetRootArea(file);
            return TestCasesFromYamlSequence(source, file, sequence, rootArea, defaultClassName);
        }

        #region private methods

        private static YamlStream ParseYamlStream(string fullName)
        {
            var stream = new YamlStream();
            stream.Load(File.OpenText(fullName));
            return stream;
        }

        private static IEnumerable<TestCase> TestCasesFromYamlSequence(string source, FileInfo file, YamlSequenceNode sequence, string area, string @class)
        {
            var tests = new List<TestCase>();
            if (sequence == null) return tests;

            foreach (YamlMappingNode mapping in sequence.Children)
            {
                var test = GetTestFromNode(source, file, mapping, area, @class);
                if (test != null)
                {
                    tests.Add(test);
                    continue;
                }

                var children = CheckForChildren(source, file, mapping, area, @class);
                if (children != null)
                {
                    tests.AddRange(children);
                    continue;
                }
            }
            return tests;
        }

        private static TestCase GetTestFromNode(string source, FileInfo file, YamlMappingNode mapping, string area, string @class)
        {
            string simulate = GetScalarString(mapping, "simulate");
            var simulating = !string.IsNullOrEmpty(simulate);

            string command = GetScalarString(mapping, "command");
            string script = GetScalarString(mapping, "script");
            var bothOrNeither = (command == null) == (script == null);
            if (bothOrNeither && !simulating) return null;

            string fullyQualifiedName = GetFullyQualifiedName(mapping, area, @class)
                ?? GetFullyQualifiedName(area, @class, $"Expected YAML node ('name') at {file.FullName}({mapping.Start.Line})");

            Logger.Log($"YamlTestParser::GetTests(): new TestCase('{fullyQualifiedName}')");
            var test = new TestCase(fullyQualifiedName, new Uri(YamlTestAdapter.Executor), source)
            {
                CodeFilePath = file.FullName,
                LineNumber = mapping.Start.Line
            };

            SetTestCaseProperty(test, "command", command);
            SetTestCaseProperty(test, "script", script);
            SetTestCaseProperty(test, "simulate", simulate);
            SetTestCaseProperty(test, "working-directory", file.DirectoryName);

            SetTestCaseProperty(test, "expect", mapping, "expect");
            SetTestCaseProperty(test, "not-expect", mapping, "not-expect");
            SetTestCaseProperty(test, "log-expect", mapping, "log-expect");
            SetTestCaseProperty(test, "log-not-expect", mapping, "log-not-expect");    

            AddTestCaseTags(test, mapping);

            CheckInvalidTestCaseNodes(file, mapping, test);
            return test;
        }

        private static IEnumerable<TestCase> CheckForChildren(string source, FileInfo file, YamlMappingNode mapping, string area, string @class)
        {
            var sequence = mapping.Children.ContainsKey("tests")
                ? mapping.Children["tests"] as YamlSequenceNode
                : null;
            if (sequence == null) return null;

            @class = GetScalarString(mapping, "class", @class);
            area = UpdateArea(mapping, area);

            return TestCasesFromYamlSequence(source, file, sequence, area, @class);
        }

        private static void CheckInvalidTestCaseNodes(FileInfo file, YamlMappingNode mapping, TestCase test)
        {
            foreach (YamlScalarNode key in mapping.Children.Keys)
            {
                if (";area;class;name;command;script;expect;not-expect;log-expect;log-not-expect;simulate;tag;tags;".IndexOf($";{key.Value};") < 0)
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
                YamlTestProperties.Set(test, propertyName, value);
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

        private static string GetRootArea(FileInfo file)
        {
            return $"{file.Extension.TrimStart('.')}.{file.Name.Remove(file.Name.LastIndexOf(file.Extension))}";
        }

        private static string UpdateArea(YamlMappingNode mapping, string area)
        {
            var subArea = GetScalarString(mapping, "area");
            return string.IsNullOrEmpty(subArea)
                ? area
                : $"{area}.{subArea}";
        }

        private static string GetFullyQualifiedName(YamlMappingNode mapping, string area, string @class)
        {
            var name = GetScalarString(mapping, "name");
            if (name == null) return null;

            area = UpdateArea(mapping, area);
            @class = GetScalarString(mapping, "class", @class);

            return GetFullyQualifiedName(area, @class, name);
        }

        private static string GetFullyQualifiedName(string area, string @class, string name)
        {
            return $"{area}.{@class}.{name}";
        }

        private static void AddTestCaseTags(TestCase test, YamlMappingNode mapping)
        {
            var tagNode = mapping.Children.ContainsKey("tag") ? mapping.Children["tag"] : null;
            var tagsNode = mapping.Children.ContainsKey("tags") ? mapping.Children["tags"] : null;
            if (tagNode == null && tagsNode == null) return;

            var tags = new Dictionary<string, List<string>>();
            UpdateTags(tags, tagNode, tagsNode);
            SetTestCaseTagsAsTraits(test, tags);
        }

        private static void UpdateTags(Dictionary<string, List<string>> tags, YamlNode tagNode, YamlNode tagsNode)
        {
            var value = (tagNode as YamlScalarNode)?.Value;
            AddOptionalTag(tags, "tag", value);

            var values = (tagsNode as YamlScalarNode)?.Value;
            AddOptionalCommaSeparatedTags(tags, values);

            AddOptionalNameValueTags(tags, tagsNode as YamlMappingNode);
            AddOptionalTagsForEachChild(tags, tagsNode as YamlSequenceNode);
        }

        private static void AddOptionalTag(Dictionary<string, List<string>> tags, string name, string value)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
            {
                if (!tags.ContainsKey(name))
                {
                    tags.Add(name, new List<string>());
                }
                tags[name].Add(value);
            }
        }

        private static void AddOptionalCommaSeparatedTags(Dictionary<string, List<string>> tags, string values)
        {
            if (values != null)
            {
                foreach (var tag in values.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    AddOptionalTag(tags, "tag", tag);
                }
            }
        }

        private static void AddOptionalNameValueTags(Dictionary<string, List<string>> tags, YamlMappingNode mapping)
        {
            var children = mapping?.Children;
            if (children == null) return;

            foreach (var child in children)
            {
                var key = (child.Key as YamlScalarNode)?.Value;
                var value = (child.Value as YamlScalarNode)?.Value;
                AddOptionalTag(tags, key, value);
            }
        }

        private static void AddOptionalTagsForEachChild(Dictionary<string, List<string>> tags, YamlSequenceNode sequence)
        {
            var children = sequence?.Children;
            if (children == null) return;

            foreach (var child in children)
            {
                if (child is YamlScalarNode)
                {
                    AddOptionalTag(tags, "tag", (child as YamlScalarNode).Value);
                    continue;
                }

                if (child is YamlMappingNode)
                {
                    AddOptionalNameValueTags(tags, child as YamlMappingNode);
                    continue;
                }
            }
        }

        private static void SetTestCaseTagsAsTraits(TestCase test, Dictionary<string, List<string>> tags)
        {
            foreach (var tag in tags)
            {
                foreach (var value in tag.Value)
                {
                    test.Traits.Add(tag.Key, value);
                }
            }
        }

        private const string defaultClassName = "TestCases";

        #endregion
    }
}