using System;
using System.IO;

namespace OpenTap
{
    /// <summary>Immutable configuration for one hosted application. Construct using <see cref="Builder"/>.</summary>
    public sealed class ApplicationHostOptions
    {
        /// <summary>Exact file-backed entry assembly; it need not be loaded in the caller.</summary>
        public string AssemblyPath { get; }
        /// <summary>Full type name within the entry assembly.</summary>
        public string TypeName { get; }
        /// <summary>Public or internal static, non-generic object method accepting exactly object.</summary>
        public string MethodName { get; }
        /// <summary>Unmarshalled cross-context argument. Prefer shared runtime types or serialized strings.</summary>
        public object Argument { get; }
        /// <summary>Installation root used for engine and plugin resolution.</summary>
        public string BaseDirectory { get; }

        private ApplicationHostOptions(Builder builder)
        {
            AssemblyPath = Path.GetFullPath(builder.assemblyPath);
            TypeName = builder.typeName;
            MethodName = builder.methodName;
            Argument = builder.argument;
            BaseDirectory = Path.GetFullPath(builder.baseDirectory);
        }

        /// <summary>Builds reusable options without loading the entry assembly.</summary>
        public sealed class Builder
        {
            internal string assemblyPath, typeName, methodName;
            internal object argument;
            internal string baseDirectory = Path.GetDirectoryName(typeof(OpenTap.ApplicationContext).Assembly.Location);

            /// <summary>Locates an entry point using an already-loaded type; the original type is not invoked.</summary>
            public Builder WithEntryPoint(Type type, string methodName)
            {
                if (type == null) throw new ArgumentNullException(nameof(type));
                if (type.IsGenericType) throw new ArgumentException("The entry point type must not be generic.", nameof(type));
                if (string.IsNullOrEmpty(type.Assembly.Location))
                    throw new NotSupportedException("ApplicationHost requires a file-backed entry assembly.");
                return WithEntryPoint(type.Assembly.Location, type.FullName, methodName);
            }

            /// <summary>Locates a type inside a specific assembly file, loaded only into the child context.</summary>
            public Builder WithEntryPoint(string assemblyPath, string typeName, string methodName)
            {
                RequireText(assemblyPath, nameof(assemblyPath));
                RequireText(typeName, nameof(typeName));
                RequireText(methodName, nameof(methodName));
                this.assemblyPath = assemblyPath;
                this.typeName = typeName;
                this.methodName = methodName;
                return this;
            }

            /// <summary>Sets the argument passed unchanged to the entry method; null is supported.</summary>
            public Builder WithArgument(object argument) { this.argument = argument; return this; }

            /// <summary>Sets the installation root. Relative paths are resolved when Build is called.</summary>
            public Builder WithBaseDirectory(string baseDirectory)
            {
                RequireText(baseDirectory, nameof(baseDirectory));
                this.baseDirectory = baseDirectory;
                return this;
            }

            /// <summary>Creates an immutable snapshot. File existence is checked when the host starts.</summary>
            public ApplicationHostOptions Build()
            {
                if (assemblyPath == null) throw new InvalidOperationException("An entry point is required.");
                return new ApplicationHostOptions(this);
            }

            static void RequireText(string value, string name)
            {
                if (value == null) throw new ArgumentNullException(name);
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", name);
            }
        }
    }
}
