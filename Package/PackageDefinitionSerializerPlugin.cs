//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// TapSerializerPlugin for <see cref="PackageDef"/>
    /// </summary>
    internal class PackageDefinitionSerializerPlugin : TapSerializerPlugin
    {
        /// <summary>
        /// Called as part for the deserialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if((node.Name.LocalName == "Package" && t.IsA(typeof(PackageDef))) || 
               (node.Name.LocalName == nameof(PackageIdentifier) && t.IsA(typeof(PackageIdentifier))))
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
                        case "Date":
                            if (DateTime.TryParse(attr.Value, out DateTime date))
                                pkg.Date = date;
                            else
                                pkg.Date = DateTime.MinValue;
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
                            pkg.Description = Regex.Match(elm.ToString().Replace("\r", ""), "^<Description.*?>((?:.|\\n)+)</Description>", RegexOptions.Multiline).Groups[1].Value.Trim();
                            break;
                        case "PackageRepositoryUrl":
#pragma warning disable 618
                            pkg.Location = elm.Value;
#pragma warning restore 618
                            break;
                        default:
                            var prop = pkg.GetType().GetProperty(elm.Name.LocalName);
                            if (prop != null)
                                Serializer.Deserialize(elm, o => prop.SetValue(pkg,o), prop.PropertyType);
                            else // If the property does not exist on the packagedef type, it is MetaData
                                pkg.MetaData.Add(elm.Name.LocalName, elm.Value);
                            break;
                    }
                }

                if (t.IsA(typeof(PackageIdentifier)))
                    setter.Invoke(new PackageIdentifier(pkg));
                else
                    setter.Invoke(pkg);

                // If the Version XML attribute is missing, default to same behavior as if Version="" was specified. We depend on packages having a version.
                if (pkg.Version is null && string.IsNullOrEmpty(pkg.RawVersion)) {
                    pkg.RawVersion = "";
                    if (SemanticVersion.TryParse("0.0.0", out var semver))
                        pkg.Version = semver;
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Called as part for the serialization chain. Returns false if it cannot serialize the XML element.
        /// Disables the 'WritePackageDependencies' feature of the <see cref="TestPlanPackageDependencySerializer"/>
        /// further up the chain when it runs.
        /// </summary>
        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            if (expectedType.IsA(typeof(PackageDef)) == false && expectedType.IsA(typeof(PackageIdentifier)) == false)
                return false;
            XNamespace ns = "http://opentap.io/schemas/package";
            node.Name = ns + (expectedType.IsA(typeof(PackageIdentifier)) ? nameof(PackageIdentifier) : "Package");
            node.SetAttributeValue("type", null);
            foreach (var prop in expectedType.GetMembers())
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
                    case "Date":
                        if(((DateTime)val) != DateTime.MinValue)
                            node.SetAttributeValue("Date", ((DateTime)val).ToString(CultureInfo.InvariantCulture));
                        break;
                    case "Description":
                        var mngr = new XmlNamespaceManager(new NameTable());
                        mngr.AddNamespace("", ns.NamespaceName); // or proper URL
                        var parserContext = new XmlParserContext(null, mngr, null, XmlSpace.None, null);
                        var txtReader = new XmlTextReader($"<Description>{val}</Description>", XmlNodeType.Element, parserContext);
                        var ele = XElement.Load(txtReader);
                        node.Add(ele);
                        break;
                    case nameof(PackageDef.MetaData):
                        var metadata = val as Dictionary<string, string>;
                        var packageProperties = typeof(PackageDef).GetProperties();
                        foreach (var key in metadata.Keys)
                        {
                            if (packageProperties.Any(p => p.Name == key))
                                throw new ExitCodeException((int)PackageExitCodes.PackageCreateError, $"PackageDef property '{key}' cannot be overridden by metadata.");
                            
                            var element = new XElement(key, metadata[key]);
                            SetAllNamespaces(element, ns);
                            node.Add(element);
                        }
                        break;
                    default:
                        var xmlAttr = prop.GetAttributes<XmlAttributeAttribute>().FirstOrDefault();
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
                                Serializer.Serialize(elm, val, expectedType: prop.TypeDescriptor);
                            }
                            SetAllNamespaces(elm, ns);
                            node.Add(elm);
                        }
                        break;
                }
            }
            node.SetAttributeValue("xmlns", ns);

            // ask the TestPlanPackageDependency serializer (the one that writes the 
            // <Package.Dependencies> tag in the bottom of e.g. TestPlan files) to
            // not write the tag for this file.
            var depSerializer = Serializer.GetSerializer<TestPlanPackageDependencySerializer>();
            if (depSerializer != null)
                depSerializer.WritePackageDependencies = false;

            return true;
        }

        void SetAllNamespaces(XElement e, XNamespace ns)
        {
            e.Name = ns + e.Name.LocalName;
            foreach (var n in e.Elements())
                SetAllNamespaces(n, ns);
        }
    }

    /// <summary>
    /// TapSerializerPlugin for <see cref="PackageDependency"/>
    /// </summary>
    internal class PackageDependencySerializerPlugin : OpenTap.TapSerializerPlugin
    {
        /// <summary>
        /// Called as part for the deserialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if (t.IsA(typeof(PackageDependency)))
            {
                string name = null;
                string version = null;
                foreach (XAttribute attr in node.Attributes())
                {
                    switch (attr.Name.LocalName)
                    {
                        case "Version":
                            version = attr.Value;
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
                setter.Invoke(new PackageDependency(name, ConvertVersion(version), version));
                return true;
            }
            return false;
        }

        private static VersionSpecifier ConvertVersion(string Version)
        {
            if (String.IsNullOrWhiteSpace(Version))
                return VersionSpecifier.Any;
            if (VersionSpecifier.TryParse(Version, out var semver))
            {
                return semver;
            }
            // For compatability (pre 9.0 packages may not have correctly formatted version numbers)
            var plugins = PluginManager.GetPlugins<IVersionTryConverter>().Concat(PluginManager.GetPlugins<IVersionConverter>());

            foreach (var plugin in plugins.OrderBy(p => p.GetDisplayAttribute().Order))
            {
                try
                {
                    object cvt = Activator.CreateInstance(plugin);
                    if (cvt is IVersionTryConverter vc2)
                    {
                        if (vc2.TryConvert(Version, out SemanticVersion sv))
                            return new VersionSpecifier(sv, VersionMatchBehavior.Compatible);
                    }
                    else if(cvt is IVersionConverter vc)
                    {
                        return new VersionSpecifier(vc.Convert(Version), VersionMatchBehavior.Compatible);
                    }
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
        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            if (expectedType.IsA(typeof(PackageDependency)) == false)
                return false;
            node.Name = "Package"; 
            node.Name = "PackageDependency"; // TODO: remove when server is updated (this is only here for support of the TAP 8.x Repository server that does not yet have a parser that can handle the new name)
            node.SetAttributeValue("type", null);
            foreach (var prop in expectedType.GetMembers())
            {
                object val = prop.GetValue(obj);
                string name = prop.Name;
                if (val == null)
                    continue;
                if (name == "Name")
                    name = "Package"; // TODO: remove when server is updated (this is only here for support of the TAP 8.x Repository server that does not yet have a parser that can handle the new name)
                node.SetAttributeValue(name, val);
            }
            return true;
        }
    }
}
