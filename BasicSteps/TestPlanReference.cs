//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Threading;
using System.Xml;
using OpenTap;
using OpenTap.Plugins;

namespace OpenTap.Plugins.BasicSteps
{
    using SF = SyntaxFactory;
    [Display("Test Plan Reference", "References a test plan from an external file directly, without having to store the test plan as steps in the current test plan.", "Flow Control")]
    [AllowAnyChild]
    public class TestPlanReference : TestStep, IDynamicStep
    {
        /// <summary>
        /// Mapping between step GUIDs. Oldversion -> New Version.
        /// </summary>
        public class GuidMapping
        {
            public Guid Guid1 { get; set; }
            public Guid Guid2 { get; set; }
        }

        /// <summary>
        /// For storing external parameter values between loads. This is important to support nested TestPlanReference steps
        /// that are configured with external parameters from the command line.
        /// </summary>
        public class ExternalParameterEntryData
        {
            public string Name { get; set; }
            public object Value { get; set; }
        }

        [Browsable(false)]
        [XmlIgnore] // it will be XML ignored, unless explicitly set in XML text.
        // For backwards compatibility between TAP 7.x and TAP 8.x. 
        // TODO: Legacy support, consider removing in TAP 9.0.
        public string DynamicDataContents { set;get; }

        /// <summary>
        /// This counter is used to prevent recursive TestPlan references.
        /// </summary>
        private static int LevelCounter = 0;

        ITestStepParent parent;
        // The PlanDir of 'this' should be ignored when calculating Filepath, so the MacroString context is set to the parent.
        [XmlIgnore]
        public override ITestStepParent Parent { get { return parent; } set { Filepath.Context = value; parent = value; } }

        MacroString filepath = new MacroString();

        [Display("Referenced Plan", Order: 0, Description: "A file path pointing to a test plan which will be imported as readonly test steps.")]
        [Browsable(true)]
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "TapPlan")]
        public MacroString Filepath { get { return filepath; } set { filepath = value; } }
        
        public string Path
        {
            get { return Filepath.Expand(); }
        }

        bool isExpandingPlanDir = false;
        [MetaData(macroName: "TestPlanDir")]
        public string PlanDir
        {
            get
            {
                if (isExpandingPlanDir) return null;
                isExpandingPlanDir = true;
                try
                {
                    var exp = new MacroString(this) { Text = Filepath.Text }.Expand();
                    if (string.IsNullOrWhiteSpace(exp))
                        return "";
                    var path = System.IO.Path.GetFullPath(exp);
                    return string.IsNullOrWhiteSpace(path) ? "" : System.IO.Path.GetDirectoryName(path);
                }
                catch
                {
                    return "";
                }
                finally
                {
                    isExpandingPlanDir = false;
                }
            }
        }

        [XmlIgnore]
        [Browsable(false)]
        public new TestStepList ChildTestSteps
        {
            get { return base.ChildTestSteps; }
            set { base.ChildTestSteps = value; }
        }

        readonly List<ExternalParameter> maskEntries = new List<ExternalParameter>();

        [Browsable(false)]
        public List<ExternalParameterEntryData> MaskEntries { get; set; }

        public TestPlanReference()
        {
            ChildTestSteps.IsReadOnly = true;
            Filepath = new MacroString(this);
            Rules.Add(() => (string.IsNullOrWhiteSpace(Filepath) || File.Exists(Filepath)), "File does not exist.", "Filepath");
            StepMapping = new List<GuidMapping>();
            MaskEntries = new List<ExternalParameterEntryData>();
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();

            if (string.IsNullOrWhiteSpace(Filepath))
                throw new TestPlan.AbortException(string.Format("Execution aborted by {0}. No test plan configured.", Name));
        }

        public override void Run()
        {
            foreach (var run in RunChildSteps())
                UpgradeVerdict(run.Verdict);
        }
        

        protected object getValue(int idx)
        {
            if (maskEntries.Count <= idx) return null;
            return maskEntries[idx].Value;
        }

        protected void setValue(int idx, object value)
        {
            if (maskEntries.Count <= idx) return;
            maskEntries[idx].Value = value;
            MaskEntries[idx].Value = value;
        }
        
        static LiteralExpressionSyntax getLiteralExpression(object value)
        {
            return SF.LiteralExpression(SyntaxKind.NumericLiteralExpression, SF.Literal((dynamic)value));
        }

        static AttributeArgumentSyntax getAttributeArgument(Type type, object value)
        {
            return SF.AttributeArgument(getExpression(type, value));
        }

        static ExpressionSyntax getExpression(Type type, object value)
        {
            if(value == null)
                return SF.LiteralExpression(SyntaxKind.NullLiteralExpression);
            var lst = value as IEnumerable<CustomAttributeTypedArgument>;
            if (lst == null || value is string)
            {
                if (type.IsEnum)
                    return SF.CastExpression( SF.ParseTypeName(GetCSharpRepresentation(type)), SF.LiteralExpression(SyntaxKind.NumericLiteralExpression, SF.Literal((int)value)));

                if (type.IsNumeric())
                    return getLiteralExpression(value);
                if (type == typeof(string))
                    return SF.LiteralExpression(SyntaxKind.StringLiteralExpression, SF.Literal((string)value));
                if(type == typeof(Type))
                {
                    return SF.TypeOfExpression(SF.ParseTypeName(((Type)value).FullName));
                }
                if(type == typeof(Boolean))
                {
                    if((bool)value == true)
                        return SF.LiteralExpression(SyntaxKind.TrueLiteralExpression);
                    else
                        return SF.LiteralExpression(SyntaxKind.FalseLiteralExpression);
                }
            }
            return SF.ArrayCreationExpression(SF.ArrayType(SF.ParseTypeName(type.GetElementType().FullName)),
                SF.InitializerExpression(SyntaxKind.ArrayInitializerExpression,new SeparatedSyntaxList<ExpressionSyntax>().AddRange(lst.Select(x => getExpression(x.ArgumentType, x.Value)))));
        }

        static string GetCSharpRepresentation(Type t)
        {
            string baseName;
            if (t.IsNested)
            {
                baseName = GetCSharpRepresentation(t.DeclaringType);
            }
            else
            {
                baseName = t.Namespace;
            }

            string name = baseName + "." + t.Name;

            if (t.IsGenericType)
            {
                // 'List`1' -> 'List'
                var _name = t.Name.Substring(0, t.Name.IndexOf('`'));
                name = baseName + "." + _name;
                var genericArgs = t.GetGenericArguments().ToList();

                // Recursively get generic arguments.
                string genericName = name + "<" + string.Join(",", genericArgs.Select(GetCSharpRepresentation)) + ">";
                name = genericName;
            }
            if (t.IsArray)
            {
                // if t is an array, the []'s are in t.Name, so nothing needs to be done.
            }
            
            return name;
        }

        static string typeMemorizerKey(List<ExternalParameter> parameters)
        {
            var key = string.Join(",", parameters.Select(x => x.Name + "|" + string.Join(",", x.PropertyInfos.Select(x2 => x2.ReflectedType.FullName + "." + x2.Name))));
            return key;
        }


        static Memorizer<List<ExternalParameter>, Type, string> typeMemorizer = new Memorizer<List<ExternalParameter>, Type, string>(typeMemorizerKey, _fromProperties);

        public static Type _fromProperties(List<ExternalParameter> properties) { 
            string basiccode = @"using System.Reflection;
using OpenTap.Plugins.BasicSteps;
using OpenTap;
using System;
[assembly: AssemblyVersion(""__VERSION__"")]
[assembly: AssemblyFileVersion(""__VERSION__"")]
[assembly: AssemblyInformationalVersion(""__INFOVERSION__"")]
namespace Dynamic{
class ReferenceTestStep : OpenTap.Plugins.BasicSteps.TestPlanReference{


}
}".Replace("__VERSION__", typeof(TestPlanReference).Assembly.GetName().Version.ToString())
  .Replace("__INFOVERSION__", typeof(TestPlanReference).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);

            var tree = CSharpSyntaxTree.ParseText(SourceText.From(basiccode));
            var root = tree.GetRoot();
            var ns = root.ChildNodes().LastOrDefault();
            var cls = (ClassDeclarationSyntax) ns.ChildNodes().LastOrDefault();
            int idx = 0;

            
            HashSet<Type> propertyTypes = new HashSet<Type>();
            foreach (var prop in properties)
            {
                string propertyName = prop.Name;
                if (char.IsDigit(prop.Name[0]) || false == prop.Name.All(x => char.IsLetterOrDigit(x) || x == '_'))
                    propertyName = "prop" + idx;

                var typestr = GetCSharpRepresentation(prop.PropertyInfos.First().PropertyType);
                var typename = SF.ParseTypeName(typestr);
                PropertyDeclarationSyntax property = SF.PropertyDeclaration(typename, SF.Identifier(propertyName))
                    .WithModifiers(new SyntaxTokenList().Add(SF.Token(SyntaxKind.PublicKeyword)));
                var get = SF.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken))
                    .WithExpressionBody(SF.ArrowExpressionClause(SF.CastExpression(typename, SF.InvocationExpression(SF.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SF.ThisExpression(), SF.IdentifierName("getValue"))
                    , SF.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().Add(SF.Argument(SF.LiteralExpression(SyntaxKind.NumericLiteralExpression, SF.Literal(idx)))))))));
                var set = SF.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken))
                    .WithExpressionBody(SF.ArrowExpressionClause(SF.InvocationExpression(SF.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SF.ThisExpression(), SF.IdentifierName("setValue"))
                    , SF.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().Add(SF.Argument(SF.LiteralExpression(SyntaxKind.NumericLiteralExpression, SF.Literal(idx)))).Add(SF.Argument(SF.IdentifierName("value")))))));

                var description = prop.PropertyInfos.First().GetDisplayAttribute().Description ?? "";

                var displayattr = SF.Attribute(SF.ParseName("DisplayAttribute"))
                    .WithArgumentList(
                    SF.AttributeArgumentList(new SeparatedSyntaxList<AttributeArgumentSyntax>()
                    .Add(SF.AttributeArgument(SF.LiteralExpression(SyntaxKind.StringLiteralExpression, SF.Literal(prop.Name)))) // Name
                    .Add(SF.AttributeArgument(SF.LiteralExpression(SyntaxKind.StringLiteralExpression, SF.Literal(description)))) // Description
                    .Add(SF.AttributeArgument(SF.LiteralExpression(SyntaxKind.StringLiteralExpression, SF.Literal("Test Plan Parameters"))))// Group
                    .Add(SF.AttributeArgument(SF.LiteralExpression(SyntaxKind.NumericLiteralExpression, SF.Literal(1))))
                        ));// Order
                propertyTypes.Add(prop.PropertyInfos.First().PropertyType);
                var sep = new SeparatedSyntaxList<AttributeSyntax>().Add( displayattr);
                var attrs2 = SF.AttributeList(sep);
                
                var attrs = prop.PropertyInfos.First().GetCustomAttributesData();
                foreach (var attr in attrs)
                {

                    if (attr.AttributeType == typeof(DisplayNameAttribute) || attr.AttributeType == typeof(DisplayAttribute))
                        continue;
                    if (attr.AttributeType == typeof(EnabledIfAttribute) || attr.AttributeType == typeof(AvailableValuesAttribute))
                        continue; // We cannot handle enabledIf/AvailableValues.
                    if (!propertyTypes.Contains(attr.AttributeType))
                        propertyTypes.Add(attr.AttributeType);
                    var decl = SF.Attribute(SF.ParseName(attr.AttributeType.FullName));
                    decl = decl.WithArgumentList(SF.AttributeArgumentList(
                        new SeparatedSyntaxList<AttributeArgumentSyntax>()
                        .AddRange(attr.ConstructorArguments.Select(x => getAttributeArgument(x.ArgumentType, x.Value)))
                        .AddRange(attr.NamedArguments.Select(x => getAttributeArgument(x.TypedValue.ArgumentType, x.TypedValue.Value).WithNameColon(SF.NameColon(SF.IdentifierName(x.MemberName)))))
                        ));
                    attrs2 = attrs2.AddAttributes(decl);
                }

                property = property.AddAttributeLists(attrs2);
                
                property = property.AddAccessorListAccessors(get, set);
                cls = cls.AddMembers(property);

                idx += 1;
            }
            root = root.ReplaceNode(root.ChildNodes().Last(), ns.ReplaceNode(ns.ChildNodes().LastOrDefault(), cls));
            
            
            var metadataref = new List<MetadataReference> { };
            var md = new HashSet<string>{ typeof(int).Assembly.Location, typeof(TestStep).Assembly.Location, typeof(TestPlanReference).Assembly.Location};
            try
            {
                var asm = Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
                md.Add(asm.Location);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("mscorlib");
                md.Add(asm.Location);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("System.Runtime");
                md.Add(asm.Location);
            }
            catch { }

            foreach (var t in propertyTypes)
            {
                md.Add(t.Assembly.Location);
            }
            foreach (var path in md)
            {
                var r = MetadataReference.CreateFromFile(path);
                metadataref.Add(r);
            }

            

            CSharpCompilation compilation = CSharpCompilation.Create(
                "Dynamic.ReferenceTestStep.dll",
                syntaxTrees: new[] { SF.SyntaxTree(root)},
                references: metadataref,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            
            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                if (failures.Any())
                {
                    var errors = string.Join("\n", failures.Select(x => x.GetMessage()));
                    throw new Exception(errors);
                }
                ms.Seek(0, SeekOrigin.Begin);
                Assembly assembly = Assembly.Load(ms.ToArray());
                var tp = assembly.GetType("Dynamic.ReferenceTestStep",true,true);
                System.Diagnostics.Debug.Assert(tp.Name == "ReferenceTestStep");
                return tp;
            }
        }
        
        /// <summary>
        /// Creates a new runtime assembly and type to be able to add new properties to the test step.
        /// </summary>   
        /// <param name="properties"></param>
        /// <returns></returns>
        TestPlanReference fromProperties(List<ExternalParameter> properties)
        {
            return (TestPlanReference)typeMemorizer.Invoke(properties).CreateInstance();

        }
        
        static HashSet<string> DynamicAssemblies = new HashSet<string>();

        public Type GetStepFactoryType()
        {
            return typeof(TestPlanReference);
        }
        
        public ITestStep GetStep()
        {

            // Load GUID mappings which is every two GUIDS between <Guids/> and <Guids/>
            var mapping = new Dictionary<Guid, Guid>();
            if (DynamicDataContents != null) // Legacy
            {
                // Remove this when we decide not to be backwards compatible.


                if (DynamicDataContents.StartsWith("<Guids>"))
                {
                    int skiplen = "<Guids>".Length;
                    int idx = DynamicDataContents.IndexOf("</Guids>");
                    var guidstext = DynamicDataContents.Substring(skiplen, idx - skiplen);
                    var guids = guidstext.Split(',').Select(x => Guid.Parse(x.Trim())).ToArray();
                    for (int i = 0; i < guids.Length; i += 2)
                    {
                        mapping.Add(guids[i], guids[i + 1]);
                    }
                    DynamicDataContents = DynamicDataContents.Substring(idx + "</Guids>".Length);
                }
                Filepath.Text = DynamicDataContents;
                DynamicDataContents = null;
            }
            
            foreach(var mapitem in this.mapping)
            {
                mapping[mapitem.Guid1] = mapitem.Guid2;
            }
            
            TestPlanReference resultStep = this;
            maskEntries.Clear();
            

            object testplandir = null;
            var ser = TapSerializer.GetObjectDeserializer(this);
            if (ser != null && ser.ReadPath != null)
                testplandir = System.IO.Path.GetDirectoryName(ser.ReadPath);
            
            var Data = Filepath.Expand(testPlanDir: testplandir as string);
            
            if (string.IsNullOrEmpty(Data))
            {
                ChildTestSteps.Clear();
            }
            else if (File.Exists(Data))
            {
                ChildTestSteps.Clear();

                try
                {
                    LevelCounter++;
                    try
                    {
                        if (LevelCounter > 16)
                            throw new Exception("Test plan reference level is too high. You might be trying to load a recursive test plan.");

                        var newSerializer = new TapSerializer();
                        var serializer = TapSerializer.GetObjectDeserializer(this);
                        if(serializer != null)
                            newSerializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues.MergeInto(serializer.GetSerializer<ExternalParameterSerializer>().PreloadedValues);
                        var ext = newSerializer.GetSerializer<ExternalParameterSerializer>();
                        MaskEntries.ForEach(e =>
                        {
                            ext.PreloadedValues[e.Name] = StringConvertProvider.GetString(e.Value);
                        });
                        TestPlan tp = (TestPlan)newSerializer.DeserializeFromFile(Data);
                        
                        maskEntries.AddRange(tp.ExternalParameters.Entries);
                        if (maskEntries.Count > 0)
                        {
                            resultStep = fromProperties(maskEntries);
                        }else if(GetType() != typeof(TestPlanReference))
                        {   // Old plan had external parameters, new plan did not.
                            resultStep = new TestPlanReference { };
                        }

                        if (resultStep != this)
                        {
                            resultStep.Id = Id;
                            resultStep.Filepath = Filepath;
                            resultStep.Version = Version;
                            resultStep.Name = Name;
                        }

                        resultStep.ChildTestSteps.AddRange(tp.ChildTestSteps);
                        resultStep.maskEntries.AddRange(maskEntries);
                        foreach(var item in maskEntries)
                        {
                            resultStep.MaskEntries.Add(new ExternalParameterEntryData { Name = item.Name, Value = item.Value });
                        }
                        
                        var flatSteps = Utils.FlattenHeirarchy(tp.ChildTestSteps, x => x.ChildTestSteps);

                        resultStep.StepIdMapping = flatSteps.ToDictionary(x => x, x => x.Id);
                        
                        foreach (var step in flatSteps)
                        {
                            Guid id;
                            if (mapping.TryGetValue(step.Id, out id))
                                step.Id = id;
                        }

                        var plan = GetParent<TestPlan>();

                        // 'resultStep == this' happens when there are no
                        // external properties in the loaded plan.
                        if (plan != null && resultStep != this)
                        {
                            // Swap external parameters for the previous step and the new step.
                            foreach (var entry in plan.ExternalParameters.Entries.ToArray())
                            {
                                var props = entry.GetProperties(this);
                                if (props == null) continue;
                                foreach (var prop in props)
                                {
                                    var _prop = resultStep.GetType().GetProperty(prop.Name);
                                    if(_prop != null)
                                        entry.Add(resultStep, _prop);
                                }
                                entry.Remove(this);
                            }
                        }

                        foreach (var step in resultStep.RecursivelyGetChildSteps(TestStepSearch.All))
                        {
                            step.IsReadOnly = true;
                            step.ChildTestSteps.IsReadOnly = true;
                            step.OnPropertyChanged("");
                        }
                    }
                    finally
                    {
                        LevelCounter--;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Unable to read '{0}'.", Filepath.Text);
                    Log.Error(ex);
                }
            }
            else
                Log.Warning("File does not exist: \"{0}\"", Data);

            return resultStep;
        }
        
        /// <summary> Used to determine if a step ID has changed from the time it was loaded to the time it is saved. </summary>
        Dictionary<ITestStep, Guid> StepIdMapping { get; set; }
        public List<GuidMapping> mapping;

        [Browsable(false)]
        public List<GuidMapping> StepMapping
        {
            get {
                if (StepIdMapping == null)
                    return new List<GuidMapping>();
                return StepIdMapping.Select(x => new GuidMapping { Guid2 = x.Key.Id, Guid1 = x.Value }).Where(x => x.Guid1 != x.Guid2).ToList(); }
            set { mapping = value; }
        }


        [Browsable(true)]
        [Display("Load Test Plan", Order: 1, Description: "Load the selected test plan.")]
        public void LoadTestPlan()
        {
            if (string.IsNullOrWhiteSpace(Filepath))
            {
                Log.Warning("No test plan configured.");
                return;
            }

            ChildTestSteps.Clear();

            var OldParent = Parent;
            int index = OldParent.ChildTestSteps.IndexOf(this);
            OldParent.ChildTestSteps.RemoveAt(index);
            OldParent.ChildTestSteps.Insert(index, GetStep());
        }
    }
}
