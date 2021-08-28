using OpenTap.Package;
using System;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace OpenTap.Package
{
    internal class PackageSpecifierSerializerPlugin : TapSerializerPlugin
    {
        public override double Order => 5000; 
        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if (node.Name.LocalName == nameof(PackageSpecifier) && t is TypeData typeData && typeData.Type == (typeof(PackageSpecifier)))
            {
                string name = "";
                VersionSpecifier version = null;
                CpuArchitecture architecture = CpuArchitecture.Unspecified;
                string os = null;

                foreach (XAttribute attr in node.Attributes())
                {
                    switch (attr.Name.LocalName)
                    {
                        case "Version":
                            if (VersionSpecifier.TryParse(attr.Value, out var versionSpecifier))
                                version = versionSpecifier;
                            break;
                        case "Architecture":
                            architecture = (CpuArchitecture)Enum.Parse(typeof(CpuArchitecture), attr.Value);
                            break;
                        case "Name":
                            name = attr.Value;
                            break;
                        case "OS":
                            os = attr.Value;
                            break;
                    }
                }

                setter.Invoke(new PackageSpecifier(name, version, architecture, os));

                return true;
            }

            return false;
        }

        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            return false;
        }
    }
}
