using OpenTap.Package;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    internal class PackageFileSerializerPlugin : TapSerializerPlugin
    {
        List<ICustomPackageData> plugins = null;
        public PackageFileSerializerPlugin()
        {
            plugins = CustomPackageActionHelper.GetAllData();
        }

        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if (node.Name.LocalName == "File" && t.IsA(typeof(PackageFile)))
            {
                PackageFile packageFile = new PackageFile();

                foreach (XAttribute attr in node.Attributes())
                {
                    switch (attr.Name.LocalName)
                    {
                        case "Path":
                            packageFile.RelativeDestinationPath = attr.Value;
                            break;
                        case "SourcePath":
                            packageFile.SourcePath = attr.Value;
                            break;
                        case "LicenseRequired":
                            packageFile.LicenseRequired = attr.Value;
                            break;
                    }
                }

                foreach (XElement elm in node.Elements())
                {
                    if (elm.Name.LocalName == "Plugins")
                    {
                        Serializer.Deserialize(elm, o => packageFile.Plugins = o as List<PluginFile>, typeof(List<PluginFile>));
                        continue;
                    }

                    if (elm.Name.LocalName == "IgnoreDependency")
                    {
                        packageFile.IgnoredDependencies.Add(elm.Value);
                        continue;
                    }

                    var handlingPlugins = plugins.Where(s => s.GetType().GetDisplayAttribute().Name == elm.Name.LocalName).ToArray();


                    if (handlingPlugins.Length > 0)
                    {
                        if (handlingPlugins.Length > 1)
                            Log.Warning($"Detected multiple plugins able to handle XMl tag {elm.Name.LocalName}. Unexpected behavior may occur.");

                        ICustomPackageData p = handlingPlugins[0];
                        if(elm.HasAttributes || !elm.IsEmpty)
                            Serializer.Deserialize(elm, o => p = (ICustomPackageData)o, p.GetType());
                        packageFile.CustomData.Add(p);
                        continue;
                    }

                    packageFile.CustomData.Add(new MissingPackageData(elm));
                }

                setter.Invoke(packageFile);
                return true;
            }

            return false;
        }

        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            if (expectedType.IsA(typeof(PackageFile)) == false)
            {
                return false;
            }

            foreach (IMemberData prop in expectedType.GetMembers().Where(s => !s.HasAttribute<XmlIgnoreAttribute>()))
            {
                object val = prop.GetValue(obj);
                string name = prop.Name;
                var defaultValueAttr = prop.GetAttribute<DefaultValueAttribute>();
                if (defaultValueAttr != null)
                {
                    if (Object.Equals(defaultValueAttr.Value, val))
                        continue;
                    if (defaultValueAttr.Value == null)
                    {
                        var enu = val as IEnumerable;
                        if (enu != null && enu.GetEnumerator().MoveNext() == false) // the value is an empty IEnumerable
                        {
                            continue; // We take an empty IEnumerable to be the same as null
                        }
                    }
                }

                if (name == "RelativeDestinationPath")
                {
                    name = "Path";
                }

                if (name == "DoObfuscate")
                {
                    name = "Obfuscate";
                }

                if (name == "Plugins")
                {
                    XElement plugins = new XElement("Plugins");
                    Serializer.Serialize(plugins, val, prop.TypeDescriptor);
                    node.Add(plugins);
                    continue;
                }
                if (name == "IgnoredDependencies")
                {
                    if (val is List<string> igDeps)
                    {
                        foreach (string igDep in igDeps)
                        {
                            node.Add(new XElement("IgnoreDependency") { Value = igDep });
                        }
                        continue;
                    }
                }
                if (name == "CustomData")
                {
                    if (val is List<ICustomPackageData> packageActions)
                    {
                        foreach (ICustomPackageData action in packageActions)
                        {
                            if (action is MissingPackageData)
                                continue;

                            XElement xAction = new XElement(action.GetType().GetDisplayAttribute().Name);
                            Serializer.Serialize(xAction, action, TypeData.GetTypeData(action));
                            node.Add(xAction);
                        }
                    }
                    continue;
                }

                node.SetAttributeValue(name, val);
            }


            return true;
        }
    }
}
