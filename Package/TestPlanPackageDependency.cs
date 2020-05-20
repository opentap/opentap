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
    internal class TestPlanPackageDependency : TapSerializerPlugin
    {
        static readonly XName PackageDependenciesName = "Package.Dependencies";
        static readonly XName PackageDependencyName = "Package";
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
        void interactiveInstallPackage(string name, SemanticVersion version)
        {
            InstallNeedPackage req = new InstallNeedPackage() {
                Message = string.Format("Install package \"{0}\" version {1}?", name, version.ToString()),
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

            var plugins = new Installation(Path.GetDirectoryName(Assembly.GetAssembly(typeof(PluginManager)).Location)).GetPackages().ToDictionary(x => x.Name);

            List<string> errors = new List<string>();
            foreach (var pkg in dep.Elements(PackageDependencyName))
            {
                var nameattr = pkg.Attribute(NameName);
                var versionattr = pkg.Attribute(VersionName);
                if (nameattr == null || versionattr == null)
                    continue;

                var name = nameattr.Value;
                var versionString = versionattr.Value;
                if(!SemanticVersion.TryParse(versionString, out SemanticVersion version))
                {
                    errors.Add($"Version '{versionString}' of dependent package '{name}' could not be parsed.");
                    return false;
                }

                if (!plugins.ContainsKey(name))
                {
                    string legacyTapBasePackageName = "TAP Base";
                    if (name == legacyTapBasePackageName)
                    {
                        Log.Warning($"The saved data depends on an older, incompatible version of OpenTAP. Migrating from OpenTAP {version} to the installed version. Please verify the test plan settings.");
                        continue;
                    }
                    else
                    {
                        errors.Add($"Package '{name}' is required to load the test plan, but it is not installed.");
                    }

                    if (UsePlatformInteraction)
                    {
                        interactiveInstallPackage(name, version);   
                    }

                }
                else if (!(version.IsCompatible(plugins[name].Version)))
                {
                    errors.Add($"Package '{name}' version {version} is required to load the saved data and the installed version ({plugins[name].Version}) is not compatible.");
                }
            }

            foreach (var error in errors)
            {
                Log.Error(error);
                Serializer.PushError(element, error);
            }

            return false;
        }

        static string getAssemblyName(ITypeData _x)
        {
            var asm = _x.AsTypeData()?.Type?.Assembly;

            if (asm == null)
            {
                Log.Warning("Unable to find source of type {0}. No package dependency will be recorded for this type in the xml file.", _x.Name);
                return null;
            }

            // dynamic or assemblies loaded from bytes cannot be located.
            if (asm.IsDynamic || string.IsNullOrWhiteSpace(asm.Location))
                return null;

            try
            {
                return Path.GetFileName(asm.Location.Replace("\\", "/"));
            }
            catch
            {
                return null;
            }
        }

        XElement endnode;
        public override bool Serialize(XElement elem, object obj, ITypeData type)
        {
            if (endnode == null)
            {
                endnode = elem;
                bool ok = Serializer.Serialize(elem, obj, type);

                if (WritePackageDependencies) // Allow a serializer futher down the stack to disable the <Package.Dependencies> tag
                {
                    var pluginsNode = new XElement(PackageDependenciesName);
                    var allAssemblies = Serializer.GetUsedTypes().Select(getAssemblyName).Where(x => x != null).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                    var plugins = new Installation(Path.GetDirectoryName(PluginManager.GetOpenTapAssembly().Location)).GetPackages();
                    
                    List<PackageDef> packages = new List<PackageDef>();

                    foreach (var plugin in plugins)
                    {
                        foreach (var file in plugin.Files)
                        {
                            var filename = Path.GetFileName(file.FileName.Replace("\\", "/"));

                            if (allAssemblies.Contains(filename))
                            {
                                packages.Add(plugin);
                                break;
                            }
                        }
                    }

                    foreach (var pkg in packages)
                    {
                        var newnode = new XElement(PackageDependencyName);
                        newnode.Add(new XAttribute(NameName, pkg.Name));
                        if (pkg.Version != null)
                            newnode.Add(new XAttribute(VersionName, pkg.Version));
                        pluginsNode.Add(newnode);
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

