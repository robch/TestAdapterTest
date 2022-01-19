﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using YamlDotNet.RepresentationModel;

namespace TestAdapterTest
{
    public class TestCaseProperties
    {
        public static void Set(TestCase test, string name, string value)
        {
            TestAdapter.Log($"TestCaseProperties.Set('{name}'='{value.Replace("\n", "\\n")}')");
            if (!string.IsNullOrEmpty(value))
            {
                var property = properties[name];
                test.SetPropertyValue(property, value);
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