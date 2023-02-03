using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Package
{
    internal class PackageVersionSerializerPlugin : TapSerializerPlugin
    {
        public override double Order { get { return 5; } }

        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if (t.IsA(typeof(PackageVersion)) == false) return false;

            var version = new PackageVersion();

            void setProp(string propertyName, XObject obj)
            {
                // CPU was removed in OpenTAP 9.0. This is to support packages created by TAP 8x
                if (propertyName == "CPU") 
                    propertyName = "Architecture";

                var prop = typeof(PackageVersion).GetProperty(propertyName);
                if (prop == null) return;
                
                // Deserialize using a default serializer when possible
                if (obj is XElement elem)
                {
                    Serializer.Deserialize(elem, o => prop.SetValue(version, o), prop.PropertyType);
                    return;
                }

                // Obj is always either an XElement or an XAttribute
                var value = (obj as XAttribute)!.Value;
                if (prop.PropertyType.IsEnum)
                    prop.SetValue(version, Enum.Parse(prop.PropertyType, value));
                else if (prop.PropertyType == typeof(SemanticVersion))
                {
                    if (SemanticVersion.TryParse(value, out var semver))
                        prop.SetValue(version, semver);
                    else
                        Log.Warning($"Cannot parse version '{value}' of package '{version.Name ?? "Unknown"}'.");
                }
                else if (prop.PropertyType == typeof(DateTime))
                {
                    if (DateTime.TryParse(value, out var date))
                        prop.SetValue(version, date);
                }
                else
                {
                    prop.SetValue(version, value);
                }
            }

            var elements = node.Elements().ToList();
            var attributes = node.Attributes().ToList();

            foreach (var element in elements)
            {
                if (element.IsEmpty) continue;
                setProp(element.Name.LocalName, element);
            }

            foreach (var attribute in attributes)
            {
                setProp(attribute.Name.LocalName, attribute);
            }

            setter(version);
            return true;
        }

        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            if (expectedType.IsA(typeof(PackageVersion)) == false)
                return false;

            // We want to disable dependency writing either if:
            // 1: we are serializing a single PackageVersion
            // 2: we are serializing a single collection of PackageVersions
            // In any other case, we do want to write package dependencies.
            bool shouldDisableDependencyWriter()
            {
                if (node.Parent == null)
                    return true;
                if (node.Parent.Name.LocalName == "ArrayOfPackageVersion" ||
                    node.Parent.Name.LocalName == "ListOfPackageVersion")
                    return node.Parent.Parent == null;
                return false;
            }

            if (shouldDisableDependencyWriter() == false)
                return false;

            // ask the TestPlanPackageDependency serializer (the one that writes the 
            // <Package.Dependencies> tag in the bottom of e.g. TestPlan files) to
            // not write the tag for this file.
            var depSerializer = Serializer.GetSerializer<TestPlanPackageDependencySerializer>();
            if (depSerializer != null)
                depSerializer.WritePackageDependencies = false;
            
            // The serialization of this element should be handled by a generic serializer
            return false;
        }
    }
}