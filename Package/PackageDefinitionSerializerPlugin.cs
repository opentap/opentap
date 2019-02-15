//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    /// <summary>
    /// TapSerializerPlugin for <see cref="PackageDef"/>
    /// </summary>
    public class PackageDefinitionSerializerPlugin : OpenTap.TapSerializerPlugin
    {
        /// <summary>
        /// Called as part for the deserialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        public override bool Deserialize(XElement node, Type t, Action<object> setter)
        {
            if(node.Name.LocalName == "Package" && t == typeof(PackageDef))
            {
                var pkg = new PackageDef();
                foreach (XAttribute attr in node.Attributes())
                {
                    switch(attr.Name.LocalName)
                    {
                        case "Version":
                            pkg.RawVersion = attr.Value;
                            if (SemanticVersion.TryParse(attr.Value, out var semver))
                                pkg.Version = semver;
                            break;
                        case "Architecture":
                            pkg.Architecture = (CpuArchitecture)Enum.Parse(typeof(CpuArchitecture), attr.Value);
                            break;
                        default:
                            var prop = pkg.GetType().GetProperty(attr.Name.LocalName);
                            if (prop != null)
                                prop.SetValue(pkg, attr.Value);
                            break;
                    }
                }
                foreach (var elm in node.Elements())
                {
                    switch (elm.Name.LocalName)
                    {
                        case "Description":
                            pkg.Description = Regex.Match(elm.ToString().Replace('\r', ' ').Replace('\n', ' '), "^<Description.*?>(.+)</Description>", RegexOptions.Multiline).Groups[1].Value.Trim();
                            break;
                        case "PackageRepositoryUrl":
                            pkg.Location = elm.Value;
                            break;
                        default:
                            var prop = pkg.GetType().GetProperty(elm.Name.LocalName);
                            if (prop != null)
                                Serializer.Deserialize(elm, o => prop.SetValue(pkg,o), prop.PropertyType);
                            break;
                    }
                }
                setter.Invoke(pkg);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called as part for the serialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        public override bool Serialize(XElement node, object obj, Type expectedType)
        {
            if (expectedType != typeof(PackageDef))
                return false;
            XNamespace ns = "http://opentap.io/schemas/package";
            node.Name = ns + "Package";
            node.SetAttributeValue("type", null);
            foreach (var prop in typeof(PackageDef).GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public))
            {
                object val = prop.GetValue(obj);
                if (false == val is string && val is IEnumerable && (val as IEnumerable).GetEnumerator().MoveNext() == false)
                    continue;  // don't write empty enumerables
                var defaultAttr = prop.GetAttribute<DefaultValueAttribute>();
                if (defaultAttr != null && object.Equals(defaultAttr.Value, val))
                    continue;
                switch (prop.Name)
                {
                    case "RawVersion":
                        continue;
                    case "Description":
                        var mngr = new XmlNamespaceManager(new NameTable());
                        mngr.AddNamespace("", ns.NamespaceName); // or proper URL
                        var parserContext = new XmlParserContext(null, mngr, null, XmlSpace.None, null);
                        var txtReader = new XmlTextReader($"<Description>{val}</Description>", XmlNodeType.Element, parserContext);
                        var ele = XElement.Load(txtReader);
                        node.Add(ele);
                        break;
                    default:
                        var xmlAttr = prop.GetCustomAttributes<XmlAttributeAttribute>().FirstOrDefault();
                        if (xmlAttr != null)
                        {
                            string name = prop.Name;
                            if (!String.IsNullOrWhiteSpace(xmlAttr.AttributeName))
                                name = xmlAttr.AttributeName;
                            node.SetAttributeValue(name, val);
                        }
                        else
                        {
                            var elm = new XElement(prop.Name);
                            if (obj != null)
                            {
                                Serializer.Serialize(elm, val, expectedType: prop.PropertyType);
                            }
                            void SetNs(XElement e)
                            {
                                e.Name = ns + e.Name.LocalName;
                                foreach (var n in e.Elements())
                                    SetNs(n);
                            }
                            SetNs(elm);
                            node.Add(elm);
                        }
                        break;
                }
            }
            node.SetAttributeValue("xmlns", ns);

            // ask the TestPlanPackageDependency serializer (the one that writes the 
            // <Package.Dependencies> tag in the bottom of e.g. TestPlan files) to
            // not write the tag for this file.
            var depSerializer = Serializer.GetSerializer<TestPlanPackageDependency>();
            if (depSerializer != null)
                depSerializer.WritePackageDependencies = false;

            return true;
        }
    }

    /// <summary>
    /// TapSerializerPlugin for <see cref="PackageDependency"/>
    /// </summary>
    public class PackageDependencySerializerPlugin : OpenTap.TapSerializerPlugin
    {
        /// <summary>
        /// Called as part for the deserialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        public override bool Deserialize(XElement node, Type t, Action<object> setter)
        {
            if (t == typeof(PackageDependency))
            {
                string name = null;
                string rawVersion = null;
                foreach (XAttribute attr in node.Attributes())
                {
                    switch (attr.Name.LocalName)
                    {
                        case "Version":
                            rawVersion = attr.Value;
                            break;
                        case "Package": // TAP 8.0 support
                        case "Name":
                            name = attr.Value;
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                }
                setter.Invoke(new PackageDependency(name,ConvertVersion(rawVersion)));
                return true;
            }
            return false;
        }

        internal static VersionSpecifier ConvertVersion(string Version)
        {
            if (String.IsNullOrWhiteSpace(Version))
                return VersionSpecifier.Any;
            if (VersionSpecifier.TryParse(Version, out var semver))
            {
                return semver;
            }
            // For compatability (pre 9.0 packages may not have correctly formatted version numbers)
            var plugins = PluginManager.GetPlugins<IVersionConverter>();
            foreach (var plugin in plugins.OrderBy(p => p.GetDisplayAttribute().Order))
            {
                try
                {
                    IVersionConverter cvt = (IVersionConverter)Activator.CreateInstance(plugin);
                    return new VersionSpecifier(cvt.Convert(Version), VersionMatchBehavior.Compatible);
                }
                catch
                {

                }
            }
            return VersionSpecifier.Any;
        }

        /// <summary>
        /// Called as part for the serialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        public override bool Serialize(XElement node, object obj, Type expectedType)
        {
            if (expectedType != typeof(PackageDependency))
                return false;
            node.Name = "Package"; 
            node.Name = "PackageDependency"; // TODO: remove when server is updated (this is only here for support of the TAP 8.x Repository server that does not yet have a parser that can handle the new name)
            node.SetAttributeValue("type", null);
            foreach (var prop in typeof(PackageDependency).GetProperties())
            {
                object val = prop.GetValue(obj);
                string name = prop.Name;
                if (val == null)
                    continue;
                if (name == "RawVersion")
                    continue;
                if (name == "Name")
                    name = "Package"; // TODO: remove when server is updated (this is only here for support of the TAP 8.x Repository server that does not yet have a parser that can handle the new name)
                node.SetAttributeValue(name, val);
            }
            return true;
        }
    }
}
