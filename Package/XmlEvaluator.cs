using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenTap.Package
{
    /// <summary>
    /// The <see cref="XmlEvaluator"/> class can expand variables of the form $(VarName) -> VarNameValue
    /// in an XML document. It reads the current environment variables, and optionally document-local variables set in
    /// a <Variables/> element as a child of the root element. A variable will be expanded exactly once either if it is
    /// an XMLText element, or if it appears as text in an attribute.
    /// (e.g. <SomeElement Attr1="$(abc)">abc $(def) ghi</SomeElement> will expand $(abc) and $(def))
    /// A variable will only be expanded once. If $(abc) -> "$(def)", then the expansion of $(abc) will not be expanded.
    /// Additionally, 'Conditions' are supported. Conditions are attributes on elements. If the condition evaluates to
    /// false, the element containing the condition is removed from the document. If the condition is true, the
    /// condition itself is removed. A condition takes the form Condition="$(abc)" or Condition="$(abc) == $(def)" or
    /// Condition="$(abc) != $(def)". A condition is true if it has the value '1' or 'true', or if the comparison operator evaluates to true.
    /// </summary>
    public class XmlEvaluator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XmlEvaluator"/> class from an <see cref="XElement"/> object.
        /// </summary>
        /// <param name="root"></param>
        public XmlEvaluator(XElement root)
        {
            // Create a deep copy of the source element
            Root = new XElement(root);
        }

        /// <summary>
        /// Evaluate all variables of the form $(VarName) -> VarNameValue in the <see cref="Root"/> document.
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

        static TraceSource log = Log.CreateSource(nameof(XmlEvaluator));
        static Regex variableRegex = new Regex(@"\$\((.*?)\)", RegexOptions.Compiled);
        XElement Root { get; }
        IDictionary Variables { get; set; }
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

        static void MergeDuplicateElements(XElement elem)
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

        void ExpandNodeRecursive(XElement ele)
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

        string ExpandVariables(string str)
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

        bool EvaluateCondition(string condition)
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

                    if (string.IsNullOrWhiteSpace(norm)) return false;
                    if (norm.Equals("1")) return true;
                    if (norm.Equals("0")) return false;
                }

                condition = condition.Trim();
                var parts = condition.Split(new string[] { "==", "!=" }, StringSplitOptions.None)
                    .Select(p => p.Trim())
                    .ToArray();

                if (parts.Length != 2)
                {
                    log.Error($"Error in condition '{condition}'. A condition must be either true, false, 1, 0, or of the form a==b.");
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
    }
}