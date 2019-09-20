//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
namespace OpenTap.Engine.UnitTests
{
    using SF = SyntaxFactory;

    class CodeGen
    {
        public class Result
        {
            public byte[] Bytes;
            public bool Success;

            public string Log;

            Assembly assembly;
            public Assembly GetAssembly(){
                if(assembly == null){
                    assembly = Assembly.Load(Bytes);
                }
                return assembly;
            }
        }

        public static Result BuildCode(string code, string moduleName){
            
            var metadataref = new List<MetadataReference> { };
            // Detect the file location for the library that defines the object type
            var systemRefLocation=typeof(object).GetTypeInfo().Assembly.Location;
            // Create a reference to the library
            var systemReference = MetadataReference.CreateFromFile(systemRefLocation);
            var md = new HashSet<Assembly> { typeof(int).Assembly, typeof(ITestStep).Assembly};
            try
            {
                var asm = Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("mscorlib");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("System");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("System.Core");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("System.Runtime");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("System.Collections");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("System.ComponentModel.TypeConverter");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("System.ObjectModel");
                md.Add(asm);
            }
            catch { }
            try
            {
                var asm = Assembly.Load("Microsoft.CSharp");
                md.Add(asm);
            }
            catch { }
            md.Add(Assembly.GetEntryAssembly());
            foreach (var path in md)
            {
                if(path == null) continue;
                var r = MetadataReference.CreateFromFile(path.Location);
                metadataref.Add(r);
            }

            CSharpCompilation compilation = CSharpCompilation.Create(moduleName,
                    syntaxTrees: new[] { SF.ParseSyntaxTree(code) },

                    references: metadataref,
                    
                    
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, true, platform: Platform.AnyCpu, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default)
                .WithReportSuppressedDiagnostics(true)
                );
            

            using(var ms = new MemoryStream()){
                var result= compilation.Emit(ms);
                return new Result{Success = result.Success, Bytes = ms.ToArray(), Log = string.Join("\n", result.Diagnostics)};
            }
        }
    }

}