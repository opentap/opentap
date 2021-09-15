//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OpenTap.Package.SetAsmInfo
{
    internal class SetAsmInfo
    {
        public static void SetInfo(string filename, Version version, Version fileVersion, SemanticVersion infoVersion)
        {
            var resolver = new TapMonoResolver();
            var asm = AssemblyDefinition.ReadAssembly(filename,
                new ReaderParameters
                    {AssemblyResolver = resolver, InMemory = true, ReadingMode = ReadingMode.Immediate});

            if (version != null)
                asm.Name.Version = version;

            var fileVersionSet = false;
            var infoVersionSet = false;

            foreach (var attr in asm.CustomAttributes)
            {
                if (fileVersion != null && attr.AttributeType.FullName == typeof(AssemblyFileVersionAttribute).FullName)
                {
                    attr.ConstructorArguments[0] =
                        new CustomAttributeArgument(attr.ConstructorArguments[0].Type, fileVersion.ToString());
                    fileVersionSet = true;
                }

                if (infoVersion != null &&
                    attr.AttributeType.FullName == typeof(AssemblyInformationalVersionAttribute).FullName)
                {
                    attr.ConstructorArguments[0] =
                        new CustomAttributeArgument(attr.ConstructorArguments[0].Type, infoVersion.ToString());
                    infoVersionSet = true;
                }
            }

            if (!fileVersionSet && fileVersion != null)
            {
                // This action is never invoked; it is used to compute a reference to this particular constructor invocation
                Expression<Action> expr = () => new AssemblyFileVersionAttribute(version.ToString());
                var body = expr.Body as NewExpression;

                var ctorMethod = asm.MainModule.ImportReference(body.Constructor);
                var versionAttr = new CustomAttribute(asm.MainModule.ImportReference(ctorMethod));
                versionAttr.ConstructorArguments.Add(
                    new CustomAttributeArgument(ctorMethod.Parameters.First().ParameterType, version.ToString()));
                asm.CustomAttributes.Add(versionAttr);
            }
            if (!infoVersionSet && infoVersion != null)
            {
                // This action is never invoked; it is used to compute a reference to this particular constructor invocation
                Expression<Action> expr = () => new AssemblyInformationalVersionAttribute(version.ToString());
                var body = expr.Body as NewExpression;

                var ctorMethod = asm.MainModule.ImportReference(body.Constructor);
                var versionAttr = new CustomAttribute(asm.MainModule.ImportReference(ctorMethod));
                versionAttr.ConstructorArguments.Add(
                    new CustomAttributeArgument(ctorMethod.Parameters.First().ParameterType, version.ToString()));
                asm.CustomAttributes.Add(versionAttr);
            }
            
            using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                asm.Write(stream);
        }

        private class TapMonoResolver : BaseAssemblyResolver
        {
            ILookup<string, AssemblyData> searchedAssemblies = PluginManager.GetSearcher().Assemblies.ToLookup(asm => asm.Name);

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                var subset = searchedAssemblies[name.Name];

                var found = subset.FirstOrDefault(asm => asm.Version == name.Version) ??
                            subset.FirstOrDefault(asm => OpenTap.Utils.Compatible(asm.Version, name.Version));

                ReaderParameters customParameters = new ReaderParameters() { AssemblyResolver = new TapMonoResolver() };

                if (found == null) // Try find dependency from already loaded assemblies
                {
                    var neededAssembly = new AssemblyName(name.ToString());
                    var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(s => s.GetName().Name == neededAssembly.Name);
                    if (loadedAssembly != null)
                        return AssemblyDefinition.ReadAssembly(loadedAssembly.Location, customParameters);
                }

                if (found != null)
                    return AssemblyDefinition.ReadAssembly(found.Location, customParameters);
                return base.Resolve(name, parameters);
            }
        }
    }
}
