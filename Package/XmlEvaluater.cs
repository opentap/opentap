using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenTap.Package
{
    internal class XmlEvaluater
    {
        private static TraceSource log = Log.CreateSource(nameof(XmlEvaluater));
        private static Regex variableRegex = new Regex(@"\$\((.*?)\)", RegexOptions.Compiled);
        private XElement Root { get; }
        private IDictionary Variables { get; set; }

        public XmlEvaluater(XElement root)
        {
            // Create a deep copy of the source element
            Root = new XElement(root);
        }

        bool hasVariables(string s)
        {
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
                    Variables[variable.Name.LocalName] = ExpandVariables(variable.Value);
                }

                pgkVariables.Remove();
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
            if (hasVariables(Root.ToString()) == false) return Root;

            InitVariables();
            ExpandNodeRecursive(Root);

            return Root;
        }

        private void ExpandNodeRecursive(XElement ele)
        {
            foreach (var node in ele.Nodes())
            {
                if (node is XText t) t.Value = ExpandVariables(t.Value);
            }

            foreach (var attribute in ele.Attributes())
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

            foreach (var desc in ele.Elements())
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
                if (matchName == "GitVersion") continue;
                if (Variables.Contains(matchName)) sb.Append(Variables[matchName]);
            }

            sb.Append(str.Substring(currentIndex));

            return sb.ToString();
        }

        private bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return false;
            try
            {
                condition = condition.Trim();

                if (condition.Equals("True",  StringComparison.OrdinalIgnoreCase) || condition.Equals("1")) return true;
                if (condition.Equals("False", StringComparison.OrdinalIgnoreCase) || condition.Equals("0")) return false;

                var parts = condition.Split(new string[] { "==", "!=" }, StringSplitOptions.None)
                    .Select(p => p.Trim())
                    .ToArray();

                if (parts.Length != 2)
                {
                    log.Error($"Error in condition '{condition}'. Several checks detected. A condition must be either true, false, or of the form a==b.");
                    return false;
                }

                var isEquals = condition.IndexOf("==", StringComparison.Ordinal) >= 0;
                var areEqual = parts[0].Equals(parts[1], StringComparison.Ordinal);
                return isEquals == areEqual;
            }
            catch
            {
                log.Error($"Error during parsing of condition '{condition}'.");
                return false;
            }
        }
    }
}