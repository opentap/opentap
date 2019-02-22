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
using System.Runtime.Serialization;
using System.Threading;
using System.Xml.Linq;

namespace OpenTap.Package
{
    [Display("Test Plan Package Dependencies", Description: "Serializer plugin that inserts package dependencies for a test plan in the test plan itself.")]
    public class TestPlanPackageDependency : TapSerializerPlugin
    {
        const string PackageDependenciesName = "Package.Dependencies";
        const string PackageDependencyName = "Package";
        const string NameName = "Name";
        const string VersionName = "Version";

        public override double Order
        {
            get { return 100; }
        }

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
            UserInput.Request(req, TimeSpan.MaxValue, true);

            Log.Info("Installing package \"{0}\".", name);

            if (req.Response == UserRequest.Install)
            {
                var ins = new PackageInstallAction() { Packages = new[] { name }, ForceInstall = true, Version = version.ToString() };
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

        public override bool Deserialize(XElement element, ITypeInfo t, Action<object> setter)
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
                    errors.Add($"Package '{name}' is required to load the test plan, but it is not installed."); 
                    
                    if (UsePlatformInteraction)
                    {
                        interactiveInstallPackage(name, version);   
                    }

                }
                else if (!(version.IsCompatible(plugins[name].Version)))
                {
                    errors.Add($"Package '{name}' version {version} is required to load the test plan and the installed version ({plugins[name].Version}) is not compatible.");
                }
            }

            foreach (var error in errors)
            {
                Log.Error(error);
                Serializer.PushError(element, error);
            }

            return false;
        }

        static string getAssemblyLocation(ITypeInfo _x)
        {
            
            if(_x is CSharpTypeInfo xx && xx.Type is Type x && x.Assembly != null && x.Assembly.IsDynamic == false)
            {
                try
                {
                    // this can throw an exception, for example if the assembly is dynamic.
                    return x.Assembly.Location;
                }
                catch
                {

                }
            }
            return null;
        }

        readonly HashSet<ITypeInfo> allTypes = new HashSet<ITypeInfo>();
        XElement endnode;
        public override bool Serialize(XElement elem, object obj, ITypeInfo type)
        {
            //if (type == typeof(PackageDef))
            //    return false; // TODO: fix this in a less hacky way
            if (!allTypes.Add(type))
                return false;
            if (endnode == null)
            {
                endnode = elem;
                bool ok = Serializer.Serialize(elem, obj, type);

                if (WritePackageDependencies) // Allow a serializer futher down the stack to disable the <Package.Dependencies> tag
                {
                    var pluginsNode = new XElement(PackageDependenciesName);

                    var allassemblies = allTypes.Select(getAssemblyLocation).Where(x => x != null).ToHashSet();
                    var plugins = new Installation(Path.GetDirectoryName(Assembly.GetAssembly(typeof(PluginManager)).Location)).GetPackages();

                    List<PackageDef> packages = new List<PackageDef>();

                    foreach (var plugin in plugins)
                    {
                        foreach (var file in plugin.Files)
                        {
                            if (allassemblies.Contains(Path.GetFullPath(file.FileName)))
                                packages.Add(plugin);
                            break;
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
                return ok;
            }
            return false;
        }
    }
}

