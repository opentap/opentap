//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
[assembly:OpenTap.PluginAssembly(true)]

namespace OpenTap.Package
{
    [Display("Test Plan Package Dependencies", Description: "Serializer plugin that inserts package dependencies for a test plan in the test plan itself.")]
    internal class TestPlanPackageDependencySerializer : TapSerializerPlugin
    {
        static readonly XName PackageDependenciesName = "Package.Dependencies";
        static readonly XName PackageDependencyName = "Package";
        static readonly XName TypeDependencyName = "Type";
        static readonly XName FileDependencyName = "File";
        static readonly XName NameName = "Name";
        static readonly XName VersionName = "Version";

        public override double Order => 100; 

        public enum UserRequest
        {
            Install, Ignore
        }

        bool UsePlatformInteraction { get; set; }

        public bool WritePackageDependencies { get; set; } = true;

        class InstallNeedPackage
        {
            public string Name { get; private set; } = "Install needed package?";
            [Browsable(false)]
            public string Message { get; set; }
            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Msg => Message;
            [Submit]
            [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
            public UserRequest Response { get; set; }
        }

        // not used at the moment. Consider for TAP 8.6.
        void interactiveInstallPackage(string name, VersionSpecifier version)
        {
            InstallNeedPackage req = new InstallNeedPackage() {
                Message = $"Install package \"{name}\" version {version}?",
                Response = UserRequest.Ignore };
            UserInput.Request(req, true);

            Log.Info("Installing package \"{0}\".", name);

            if (req.Response == UserRequest.Install)
            {
                var ins = new PackageInstallAction() { Packages = new[] { name }, Force = true, Version = version.ToString() };
                try
                {
                    int ok = ins.Execute(new CancellationToken());
                    if (ok == 0)
                    {
                        Log.Info("Installed package. Searching for plugins.");
                        PluginManager.SearchAsync().Wait();
                    }
                    else
                    {
                        Log.Error("Unable to install package.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Unable to install package.");
                    Log.Debug(ex);
                }
            }
        }

        public override bool Deserialize(XElement element, ITypeData t, Action<object> setter)
        {
            var dep = element.Element(PackageDependenciesName);
            if (dep == null) return false;

            dep.Remove();
            if (element.IsEmpty)
                element.Value = ""; // little hack to ensure that the element is not created empty. (leading to null values).

            var plugins = Installation.Current.GetPackages().ToDictionary(x => x.Name);

            List<string> errors = new List<string>();
            var elements = dep.Elements(PackageDependencyName).ToArray();
            foreach (var pkg in elements)
            {
                var nameattr = pkg.Attribute(NameName);
                var versionattr = pkg.Attribute(VersionName);
                if (nameattr == null || versionattr == null)
                    continue;

                var packageName = nameattr.Value;
                var versionString = versionattr.Value;

                // for compatibility with xml files saved by OpenTAP <9.17, we have to treat all package dependencies as "this or compatible" requirements
                if (!versionString.StartsWith("^") && versionString.ToLower() != "any" && !String.IsNullOrEmpty(versionString))
                    versionString = "^" + versionString; 

                if(!VersionSpecifier.TryParse(versionString, out var version))
                {
                    errors.Add($"Version '{versionString}' of dependent package '{packageName}' could not be parsed.");
                    return false;
                }

                if (!plugins.ContainsKey(packageName))
                {
                    string legacyTapBasePackageName = "TAP Base";
                    if (packageName == legacyTapBasePackageName)
                    {
                        Log.Warning($"The saved data depends on an older, incompatible version of OpenTAP. Migrating from OpenTAP {version} to the installed version. Please verify the test plan settings.");
                        continue;
                    }
                    else
                    {
                        errors.Add($"Package '{packageName}' is required to load, but it is not installed.");
                    }

                    if (UsePlatformInteraction)
                    {
                        interactiveInstallPackage(packageName, version);   
                    }

                }
                else if (!(version.IsCompatible(plugins[packageName].Version)))
                {
                    string name = element.Document.Root.Name.ToString();
                    if(!String.IsNullOrWhiteSpace(element.BaseUri))
                        name = element.BaseUri;
                    errors.Add($"Package '{packageName}' version {version} is required to load '{name}' and the installed version ({plugins[packageName].Version}) is not compatible.");
                }
                
                foreach (var file in pkg.Elements(FileDependencyName))
                {
                    var fileNameAttr = file.Attribute(NameName);
                    if (fileNameAttr == null)
                        continue;

                    var fileName = fileNameAttr.Value;

                    if (!File.Exists(fileName))
                    {
                        errors.Add($"File '{fileName}' from package '{packageName}' is required by the test plan, but it could not be found.");
                    }
                }
            }

            foreach (var error in errors)
            {
                Log.Error(error);
                Serializer.PushError(element, error);
            }

            return false;
        }

        XElement endnode;

        public override bool Serialize(XElement elem, object obj, ITypeData type)
        {
            if (endnode == null)
            {
                endnode = elem;
                bool ok = Serializer.Serialize(elem, obj, type);

                // Allow a serializer further down the stack to disable the <Package.Dependencies> tag
                if (WritePackageDependencies)
                {
                    var pluginsNode = new XElement(PackageDependenciesName);

                    var usedTypes = Serializer.GetUsedTypes().Select(t => t.AsTypeData()).Distinct().ToArray();
                    var allFiles = Serializer.GetUsedFiles().ToArray();

                    var nodes = new Dictionary<PackageDef, XElement>();

                    XElement createValue(PackageDef p)
                    {
                        var ele = new XElement(PackageDependencyName);
                        ele.Add(new XAttribute(NameName, p.Name));
                        if (p.Version != null) ele.Add(new XAttribute(VersionName, $"^{p.Version}"));
                        
                        pluginsNode.Add(ele);

                        return ele;
                    }
                    
                    // Maybe enable this feature in a future release
                    bool addUsedTypes = false;
                    
                    foreach (var typeData in usedTypes)
                    {
                        var source = Installation.Current.FindPackageContainingType(typeData);
                        if (source != null)
                        {
                            var node = nodes.GetOrCreateValue(source, createValue);
                            if (addUsedTypes)
                            {
                                var typeNode = new XElement(TypeDependencyName);
                                typeNode.Add(new XAttribute(NameName, typeData.Name));
                                node.Add(typeNode);
                            }
                        }
                    }

                    foreach (var file in allFiles)
                    {
                        var source = Installation.Current.FindPackageContainingFile(file);
                        if (source != null)
                        {
                            var node = nodes.GetOrCreateValue(source, createValue);

                            var fileNode = new XElement(FileDependencyName);
                            fileNode.Add(new XAttribute(NameName, file));
                            node.Add(fileNode);
                        }
                    }

                    elem.Add(pluginsNode);
                }

                endnode = null;
                return ok;
            }

            return false;
        }
    }
}

