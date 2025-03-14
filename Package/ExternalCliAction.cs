using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    internal class ExternalCommandTypeDataSearcher : ITypeDataSearcher
    {
        private ITypeData[] _types = null;

        public IEnumerable<ITypeData> Types
        {
            get
            {
                if (_types == null)
                    Search();
                return _types;
            }
        }

        public void Search()
        {
            var types = new List<ITypeData>();
            var installation = Installation.Current;
            var packages = installation.GetPackages();

            foreach (var package in packages)
            {
                foreach (var action in package.ExternalCliActions)
                {
                    types.Add(ExternalCommandTypeData.FromAction(action));
                }
            }

            _types = types.ToArray();
        }
    }

    internal class ExternalCommandTypeData : ITypeData
    {
        private static readonly ConcurrentDictionary<ExternalCliAction, ExternalCommandTypeData> CommandMap =
            new ConcurrentDictionary<ExternalCliAction, ExternalCommandTypeData>();

        public static ExternalCommandTypeData FromAction(ExternalCliAction action)
        {
            return CommandMap.GetOrAdd(action, _ => new ExternalCommandTypeData(action));
        }

        private readonly ExternalCliAction Action;

        private ExternalCommandTypeData(ExternalCliAction action)
        {
            Action = action;
            var groups = (action.Groups ?? "").Split('\\')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            BaseType = TypeData.FromType(typeof(ExternalCommandCliAction));
            _attributes.Add(new DisplayAttribute(Name: action.Name, Description: action.Description,
                Groups: groups.ToArray()));
        }

        private readonly List<Attribute> _attributes = new List<Attribute>();

        public ITypeData BaseType { get; }

        public bool CanCreateInstance => true;

        public IEnumerable<object> Attributes => BaseType.Attributes.Concat(_attributes);

        public string Name => BaseType.Name;

        public object CreateInstance(object[] arguments)
        {
            return new ExternalCommandCliAction(Action);
        }

        public IMemberData GetMember(string name)
        {
            return BaseType.GetMember(name);
        }

        public IEnumerable<IMemberData> GetMembers()
        {
            return BaseType.GetMembers();
        }
    }

    internal class ExternalCommandCliAction : ICliAction
    {
        private readonly ExternalCliAction Action;

        public ExternalCommandCliAction(ExternalCliAction action)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public int Execute(CancellationToken cancellationToken)
        {
            var filename = Path.GetFileName(Action.ExeFile);
            var cmdArgs = Environment.GetCommandLineArgs()
                // Get all the arguments that were provided after the action name
                .SkipWhile(word => word != Action.Name)
                .Skip(1).ToArray();

            // We need to re-quote arguments with spaces because StartInfo only supports raw string input..
            for (var i = 0; i < cmdArgs.Length; i++)
            {
                if (cmdArgs[i].Contains(" "))
                {
                    // If the argument contains a double quote, we need to escape it
                    if (cmdArgs[i].Contains("\""))
                        cmdArgs[i] = cmdArgs[i].Replace("\"", "\\\"");
                    cmdArgs[i] = $"\"{cmdArgs[i]}\"";
                }
            }

            var startInfo = new ProcessStartInfo(Action.ExeFile)
            {
                WorkingDirectory = ExecutorClient.ExeDir,
                Arguments = (Action.Arguments ?? "") + " " + String.Join(" ", cmdArgs),
                UseShellExecute = false,
            };

            var p = Process.Start(startInfo);
            p.WaitForExit();
            return p.ExitCode;
        }
    }
}
