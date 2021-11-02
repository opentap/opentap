using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenTap.Package
{
    internal class XmlEvaluator
    {
        private static TraceSource log = Log.CreateSource(nameof(XmlEvaluator));
        private static Regex variableRegex = new Regex(@"\$\((.*?)\)", RegexOptions.Compiled);
        private XElement Root { get; }
        private IDictionary Variables { get; set; }

        public XmlEvaluator(XElement root)
        {
            // Create a deep copy of the source element
            Root = new XElement(root);
        }

        bool hasVariablesOrConditions(string s)
        {
            if (s.Contains("Condition")) return true;
            var matches = variableRegex.Matches(s);
            var usesVariables = false;
            foreach (Match match in matches)
            {
                var matchName = match.Groups[1].Value;
                if (matchName != "GitVersion")
                {
                    usesVariables = true;
                    break;
                }
            }

            return usesVariables;
        }

        void InitVariables()
        {
            Variables = Environment.GetEnvironmentVariables();
            var ns = Root.GetDefaultNamespace();
            if (Root.Element(ns.GetName("Variables")) is XElement pgkVariables)
            {
                foreach (var variable in pgkVariables.Descendants())
                {
                    var k = variable.Name.LocalName;
                    // Let environment variables override file local variables
                    if (Variables.Contains(k)) continue;
                    Variables[k] = ExpandVariables(variable.Value);
                }

                pgkVariables.Remove();
            }
        }

        private static void MergeDuplicateElements(XElement elem)
        {
            var remaining = elem.Elements().GroupBy(e => e.Name.LocalName);
            foreach (var rem in remaining)
            {
                var main = rem.First();
                var rest = rem.Skip(1).ToArray();

                foreach (var dup in rest)
                {
                    foreach (var child in dup.Elements().ToArray())
                    {
                        main.Add(new XElement(child));
                    }
                    dup.Remove();
                }
            }
        }
        /// <summary>
        /// Evaluate all variables of the from $(VarName) -> VarNameValue in the <see cref="Root"/> document.
        /// Evaluate all Conditions of the form 'Condition="Some-Condition-Expression"'
        /// Removes the node which contains the condition if it evaluates to false. Otherwise it removes the condition itself.
        /// </summary>
        public XElement Evaluate()
        {
            // Return immediately if there is nothing to expand to expand
            if (hasVariablesOrConditions(Root.ToString()) == false) return Root;

            InitVariables();
            ExpandNodeRecursive(Root);
            MergeDuplicateElements(Root);

            return Root;
        }

        private void ExpandNodeRecursive(XElement ele)
        {
            var nodes = ele.Nodes().ToArray();
            foreach (var node in nodes)
            {
                if (node is XText t) t.Value = ExpandVariables(t.Value);
            }

            var attrs = ele.Attributes().ToArray();

            foreach (var attribute in attrs)
            {
                attribute.Value = ExpandVariables(attribute.Value);
            }

            if (ele.Attribute("Condition") is XAttribute cond)
            {
                // Remove the element if the condition evaluates to false
                if (EvaluateCondition(cond.Value) == false)
                    ele.Remove();
                // Otherwise just remove the condition
                else
                    cond.Remove();
            }

            var elements = ele.Elements().ToArray();

            foreach (var desc in elements)
            {
                ExpandNodeRecursive(desc);
            }
        }

        private string ExpandVariables(string str)
        {
            var sb = new StringBuilder();

            var currentIndex = 0;
            foreach (Match match in variableRegex.Matches(str))
            {
                sb.Append(str.Substring(currentIndex, match.Index - currentIndex));
                currentIndex = match.Index + match.Length;

                if (match.Groups.Count < 2) continue;
                var matchName = match.Groups[1].Value;
                // $(GitVersion) has a special meaning in package.xml files
                if (matchName == "GitVersion")
                {
                    sb.Append("$(GitVersion)");
                    continue;
                }
                if (Variables.Contains(matchName)) sb.Append(Variables[matchName]);
            }

            sb.Append(str.Substring(currentIndex));

            return sb.ToString();
        }

        private bool EvaluateCondition(string condition)
        {
            string normalize(string str)
            {
                // Trim leading and trailing space
                str = str.Trim();

                // Remove one level of quotes if present
                if ((str[0] == '\'' || str[0] == '"') && str.First() == str.Last())
                    str = str.Substring(1, str.Length - 2);

                // Condition="true == 1" should evaluate to true
                if (str.Equals("True", StringComparison.OrdinalIgnoreCase)) return "1";
                // Condition="0 == false" should evaluate to true
                if (str.Equals("False", StringComparison.OrdinalIgnoreCase)) return "0";
                return str;
            }

            if (string.IsNullOrWhiteSpace(condition)) return false;
            try
            {
                { // Check if the condition is a literal
                    var norm = normalize(condition);

                    if (norm.Equals("1")) return true;
                    if (norm.Equals("0")) return false;
                }

                condition = condition.Trim();
                var parts = condition.Split(new string[] { "==", "!=" }, StringSplitOptions.None)
                    .Select(p => p.Trim())
                    .ToArray();

                if (parts.Length != 2)
                {
                    log.Error($"Error in condition '{condition}'. Several checks detected. A condition must be either true, false, or of the form a==b.");
                    return false;
                }

                var lhs = normalize(parts[0]);
                var rhs = normalize(parts[1]);

                var isEquals = condition.IndexOf("==", StringComparison.Ordinal) >= 0;
                var areEqual = lhs.Equals(rhs, StringComparison.Ordinal);
                return isEquals == areEqual;
            }
            catch
            {
                log.Error($"Error during parsing of condition '{condition}'.");
                return false;
            }
        }

        /// <summary>
        /// Expand the XML of the content of the file and return a file containing the evaluated content
        /// </summary>
        /// <param name="xmlFilePath"></param>
        /// <returns></returns>
        public static string FromFile(string xmlFilePath)
        {
            var elem = XElement.Load(xmlFilePath);
            var expanded = Path.GetTempFileName();
            new XmlEvaluator(elem).Evaluate().Save(expanded);
            return expanded;
        }
    }
}