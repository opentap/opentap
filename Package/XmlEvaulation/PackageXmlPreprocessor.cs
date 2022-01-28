using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Package
{
    /// <summary>
    /// The <see cref="PackageXmlPreprocessor"/> class can expand variables of the form $(VarName) -> VarNameValue
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
    internal class PackageXmlPreprocessor
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PackageXmlPreprocessor"/> from an <see cref="XElement"/> object.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="projectPath"></param>
        public PackageXmlPreprocessor(XElement root, string projectPath = null)
        {
            // Create a deep copy of the source element
            Root = new XElement(root);
            InitExpander(projectPath);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="PackageXmlPreprocessor"/> from a file path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="projectPath"></param>
        public PackageXmlPreprocessor(string path, string projectPath = null)
        {
            if (File.Exists(path) == false) throw new FileNotFoundException($"The file '{path}' does not exist.");
            try
            {
                Root = XElement.Load(path);
            }
            catch (Exception)
            {
                log.Debug($"Error loading XML Document from file '{path}'.");
                throw;
            }
            InitExpander(projectPath);
        }

        private void InitExpander(string projectPath = null)
        {
            // The project directory is required to compute the gitversion. If it is not set, GitVersion will not be computed.
            Expander.AddProvider(new GitVersionExpander(projectPath));

            Expander.AddProvider(new DefaultVariableExpander());
            Expander.AddProvider(new EnvironmentVariableExpander());
            Expander.AddProvider(new ConditionExpander());


            // Evaluate all '<PropertyGroup/> elements and remove them from the document
            var properties = Root.Elements(Root.GetDefaultNamespace().GetName("PropertyGroup")).ToArray();

            var propertyProvider = new PropertyExpander(Expander);
            Expander.AddProvider(propertyProvider);
            propertyProvider.InitVariables(properties);

            foreach (var propElem in properties)
            {
                // The property could have been removed due to a Condition.
                if (propElem.Parent != null)
                    propElem.Remove();
            }
        }

        private ElementExpander Expander { get; } = new ElementExpander();

        /// <summary>
        /// Evaluate all variables of the form $(VarName) -> VarNameValue in the <see cref="Root"/> document.
        /// Evaluate all Conditions of the form 'Condition="Some-Condition-Expression"'
        /// Removes the node which contains the condition if it evaluates to false. Otherwise it removes the condition itself.
        /// </summary>
        public XElement Evaluate()
        {
            ExpandNodeRecursive(Root);

            // OpenTAP only supports a single <Files/>, <Dependencies/> and <PackageActionExtensions/> element,
            // but with conditions it makes sense to specify these elements multiple times with different conditions.
            // Their children should be merged in a single parent element so the XML is still valid according to the schema.
            MergeDuplicateElements(Root);

            return Root;
        }

        static TraceSource log = Log.CreateSource(nameof(PackageXmlPreprocessor));
        XElement Root { get; }

        /// <summary>
        /// Merge all top-level elements in <see cref="XElement"/> elem by adding all of the children of duplicate elements to
        /// the first element
        /// </summary>
        /// <param name="elem"></param>
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

        /// <summary>
        /// Recursively expand the children of ele until a terminal node is reached.
        /// </summary>
        /// <param name="ele"></param>
        void ExpandNodeRecursive(XElement ele)
        {
            Expander.ExpandElement(ele);
            foreach (var desc in ele.Elements().ToArray())
            {
                ExpandNodeRecursive(desc);
            }
        }
    }
}