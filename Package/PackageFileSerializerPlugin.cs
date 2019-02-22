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
    public class PackageFileSerializerPlugin : TapSerializerPlugin
    {
        public override bool Deserialize(XElement node, ITypeInfo t, Action<object> setter)
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

                List<ICustomPackageData> plugins = CustomPackageActionHelper.GetAllData();

                foreach (XElement elm in node.Elements())
                {
                    if (elm.Name.LocalName == "Plugins")
                    {
                        List<PluginFile> pluginFiles = new List<PluginFile>();
                        Serializer.Deserialize(elm, o => pluginFiles = o as List<PluginFile>, pluginFiles.GetType());
                        packageFile.Plugins = pluginFiles;
                        continue;
                    }

                    if (elm.Name.LocalName == "IgnoreDependency")
                    {
                        packageFile.IgnoredDependencies.Add(elm.Value);
                        continue;
                    }

                    IEnumerable<ICustomPackageData> handlingPlugins = plugins.Where(s => s.GetType().GetDisplayAttribute().Name == elm.Name.LocalName);


                    if (handlingPlugins != null && handlingPlugins.Count() > 0)
                    {
                        if (handlingPlugins.Count() > 1)
                            Log.Warning($"Detected multiple plugins able to handle XMl tag {elm.Name.LocalName}. Unexpected behavior may occur.");

                        ICustomPackageData p = handlingPlugins.FirstOrDefault();
                        if(elm.HasAttributes || !elm.IsEmpty)
                            Serializer.Deserialize(elm, o => p = (ICustomPackageData)o, p.GetType());
                        packageFile.CustomData.Add(p);
                        continue;
                    }

                    Log.Error($"Could not find type for XML element {elm.Name.LocalName}, are you missing a plugin?");
                }

                setter.Invoke(packageFile);
                return true;
            }

            return false;
        }

        public override bool Serialize(XElement node, object obj, ITypeInfo expectedType)
        {
            if (expectedType.IsA(typeof(PackageFile)) == false)
            {
                return false;
            }

            foreach (IMemberInfo prop in expectedType.GetMembers().Where(s => !s.HasAttribute<XmlIgnoreAttribute>()))
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
                            XElement xAction = new XElement(action.GetType().GetDisplayAttribute().Name);
                            Serializer.Serialize(xAction, action, TypeInfo.GetTypeInfo(action));
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
