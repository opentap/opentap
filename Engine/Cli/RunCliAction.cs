//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using static OpenTap.PlatformInteraction;

namespace OpenTap.Cli
{
    internal enum ExitStatus : int
    {
        Ok = 0,
        TestPlanInconclusive = 20,
        TestPlanFail = 30,
        RuntimeError = 50,
        ArgumentError = 60,
        PluginError = 80
    }
    /// <summary>
    /// Test plan run CLI action. Enables running test plans through 'tap.exe run'
    /// </summary>
    [Display("run", Description: "Runs a Test Plan.")]
    public class RunCliAction : ICliAction
    {
        /// <summary>
        /// Specify a bench settings profile from which to load\nsettings. The parameter given here should correspondto the name of a subdirectory of ./Settings/Bench. If not specified the settings from TAP GUI are used.
        /// </summary>
        [CommandLineArgument("settings", Description = "Specify a bench settings profile from which to load\nsettings. The parameter given here should correspond\nto the name of a subdirectory of ./Settings/Bench.\nIf not specified the settings from TAP GUI are used.")]
        public string Settings { get; set; } = "";

        /// <summary>
        /// Don't print the greeting/logo message containing version.
        /// </summary>
        [CommandLineArgument("no-logo", Description = "Don't print the greeting/logo message containing version.")]
        public bool NoLogo { get; set; } = false;

        /// <summary>
        /// Add directories to search for plugin dlls.
        /// </summary>
        [CommandLineArgument("search", Description = "Add directories to search for plugin dlls.")]
        public string[] Search { get; set; } = new string[0];

        /// <summary>
        /// Metadata can be added multiple times. For example the serial number for your DUT (usage: --metadata dut-id=5).
        /// </summary>
        [CommandLineArgument("metadata", Description = "Metadata can be added multiple times. For example the\nserial number for your DUT (usage: --metadata dut-id=5).")]
        public string[] Metadata { get; set; } = new string[0];

        /// <summary>
        /// Never wait for user input.
        /// </summary>
        [CommandLineArgument("non-interactive", Description = "Never wait for user input.")]
        public bool NonInteractive { get; set; } = false;

        /// <summary>
        /// Sets an external test plan parameter. Can be used multiple times. Use the syntax parameter=value, e.g. \"-e delay=1.0\".
        /// </summary>
        [CommandLineArgument("external", ShortName = "e", Description = "Sets an external test plan parameter. Can be used multiple\ntimes. Use the syntax parameter=value, e.g. \"-e delay=1.0\".")]
        public string[] External { get; set; } = new string[0];

        /// <summary>
        /// Try setting an external test plan parameter, ignoring if it does not exist in the test plan. Can be used multiple times. Use the syntax parameter=value, e.g. \"-t delay=1.0\".
        /// </summary>
        [CommandLineArgument("try-external", ShortName = "t", Description = "Try setting an external test plan parameter,\nignoring if it does not exist in the test plan.\nCan be used multiple times. Use the syntax parameter=value,\ne.g. \"-t delay=1.0\".")]
        public string[] TryExternal { get; set; } = new string[0];

        /// <summary>
        /// Lists the available external test plan parameters.
        /// </summary>
        [CommandLineArgument("list-external-parameters", Description = "Lists the available external test plan parameters.")]
        public bool ListExternal { get; set; } = false;

        /// <summary>
        /// Sets the enabled result listeners for this test plan execution as a comma separated list. An example could be --results SQLite,CSV. To disable all result listeners use --results \"\".
        /// </summary>
        [CommandLineArgument("results", Description = "Sets the enabled result listeners for this test plan execution as a comma separated list. An example could be --results SQLite,CSV. To disable all result listeners use --results \"\".")]
        public string Results { get; set; } = "";

        /// <summary>
        /// Location of test plan to be executed.
        /// </summary>
        [UnnamedCommandLineArgument("Test Plan", Required = true)]
        public string TestPlanPath { get; set; } = "";

        /// <summary>
        /// Log to write debug/trace messages to
        /// </summary>
        private static readonly TraceSource log = Log.CreateSource("Main");
        private static TestPlan Plan;
        private static Mutex PlatformDialogMutex = new Mutex(false);



        internal static int Exit(ExitStatus status)
        {
            if (status == ExitStatus.RuntimeError || status == ExitStatus.ArgumentError)
            {
                log.Info("Unable to continue. Now exiting OpenTAP CLI.");
            }

            log.Flush();
            return (int)status;
        }

        /// <summary>
        /// Executes test plan
        /// </summary>
        /// <returns></returns>
        public int Execute(CancellationToken cancellationToken)
        {
            // Register handler when action is cancelled.
            cancellationToken.Register(() =>
            {
                if (Plan != null)
                {
                    log.Warning("Aborting...");
                    Plan.RequestAbort();
                }
            });
            
            List<ResultParameter> metaData = new List<ResultParameter>();
            HandleMetadata(metaData);

            string planToLoad = null;

            try
            {
                planToLoad = !string.IsNullOrWhiteSpace(TestPlanPath) ? Path.GetFullPath(TestPlanPath) : null;
            }
            catch (Exception)
            {
                Console.WriteLine("Invalid path: '{0}'", TestPlanPath);
                Console.WriteLine("The path only supports a valid file path.");
                return Exit(ExitStatus.ArgumentError);
            }

            try
            {
                HandleSearchDirectories();
            }
            catch (ArgumentException)
            {
                return Exit(ExitStatus.ArgumentError);
            }

            EngineSettings.LoadWorkingDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            Assembly assembly = Assembly.GetExecutingAssembly();

            if (!NoLogo)
            {
                Console.WriteLine("OpenTAP Command Line Interface (CLI)\n");
            }

            if (!string.IsNullOrWhiteSpace(Settings))
            {
                TestPlanRunner.SetSettingsDir(Settings);
            }

            SessionLogs.Rename(EngineSettings.Current.SessionLogPath.Expand(date: Process.GetCurrentProcess().StartTime));

            if (!NonInteractive)
            {
                WaitForInput += HandlePlatformInteractions;
            }


            if (!string.IsNullOrWhiteSpace(Results))
            {
                try
                {
                    HandleResultListeners();
                }
                catch (ArgumentException)
                {
                    return Exit(ExitStatus.ArgumentError);
                }
            }

            if (planToLoad == null)
            {
                Console.WriteLine("Please supply a valid test plan path as an argument.");
                return Exit(ExitStatus.ArgumentError);
            }

            // Load TestPlan:
            if (!File.Exists(planToLoad))
            {
                log.Error("File '{0}' does not exist.", planToLoad);
                log.Flush();
                Thread.Sleep(100);
                return Exit(ExitStatus.ArgumentError);
            }

            try
            {
                HandleExternalParametersAndLoadPlan(planToLoad);
            }
            catch (ArgumentException ex)
            {
                if(!string.IsNullOrWhiteSpace(ex.Message))
                    log.Error(ex.Message);
                return Exit(ExitStatus.ArgumentError);
            }
            catch (Exception e)
            {
                log.Error("Caught error while loading test plan: '{0}'", e.Message);
                log.Debug(e);
                return Exit(ExitStatus.RuntimeError);
            }

            log.Info("TestPlan: {0}", Plan.Name);

            if (ListExternal)
            {
                log.Info("Listing {0} external test plan parameters.", Plan.ExternalParameters.Entries.Count);
                int pad = 0;
                foreach (var entry in Plan.ExternalParameters.Entries)
                {
                    pad = Math.Max(pad, entry.Name.Length);
                }
                foreach (var entry in Plan.ExternalParameters.Entries)
                {
                    log.Info(" {2}{0} = {1}", entry.Name, StringConvertProvider.GetString(entry.Value), new String(' ', pad - entry.Name.Length));
                }
                log.Info("");
                return Exit(ExitStatus.Ok);
            }

            Verdict verdict = TestPlanRunner.RunPlanForDut(Plan, metaData);

            if (verdict == Verdict.Inconclusive)
                return Exit(ExitStatus.TestPlanInconclusive);
            if (verdict == Verdict.Fail)
                return Exit(ExitStatus.TestPlanFail);
            if (verdict > Verdict.Fail)
                return Exit(ExitStatus.RuntimeError);

            return Exit(ExitStatus.Ok);
        }

        private void HandleExternalParametersAndLoadPlan(string planToLoad)
        {
            var serializer = new TapSerializer();
            var extparams = serializer.GetSerializer<Plugins.ExternalParameterSerializer>();
            List<string> values = new List<string>();

            if (External.Length > 0)
                values.AddRange(External);
            if (TryExternal.Length > 0)
                values.AddRange(TryExternal);

            foreach (var externalParam in values)
            {
                int equalIdx = externalParam.IndexOf("=");
                if (equalIdx == -1)
                {
                    //try "Import External Parameters File"
                    var importers = CreateInstances<IExternalTestPlanParameterImport>();
                    try
                    {
                        importers.FirstOrDefault(importer => importer.Extension == Path.GetExtension(externalParam)).ImportExternalParameters(Plan, externalParam);
                        break;
                    }
                    catch
                    {
                        throw new ArgumentException("Unable to read external test plan parameter {0}. Expected '=' or ExternalParameters file.", externalParam);
                    }
                }
                var name = externalParam.Substring(0, equalIdx);
                var value = externalParam.Substring(equalIdx + 1);
                extparams.PreloadedValues[name] = value;
            }
            Plan = (TestPlan)serializer.DeserializeFromFile(planToLoad, type: typeof(TestPlan));

            if (External.Length > 0)
            {   // Print warnings if an --external parameter was not in the test plan. 

                foreach (var externalParam in External)
                {
                    var equalIdx = externalParam.IndexOf("=");
                    if (equalIdx == -1) continue;

                    var name = externalParam.Substring(0, equalIdx);
                    if (Plan.ExternalParameters.Get(name) != null) continue;

                    log.Warning("External parameter '{0}' does not exist in the test plan.", name);
                    log.Warning("Statement '{0}' has no effect.", externalParam);
                    throw new ArgumentException("");
                }
            }
        }

        private static List<T> CreateInstances<T>()
        {
            var externalParameterExportPlugins = PluginManager.GetPlugins<T>();
            var fileHandlers = new List<T>();

            foreach (var plugin in externalParameterExportPlugins)
            {
                fileHandlers.Add((T)Activator.CreateInstance(plugin));
            }

            return fileHandlers;
        }

        private void HandleResultListeners()
        {
            foreach (var r in ResultSettings.Current.OfType<IEnabledResource>())
                r.IsEnabled = false;
            var rs = Results.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var name in rs.ToList())
            {
                bool foundOne = false;
                foreach (var r in ResultSettings.Current.OfType<IEnabledResource>())
                {
                    if (string.Compare(r.Name, name, true) == 0)
                    {
                        r.IsEnabled = true;
                        foundOne = true;
                    }
                }
                if (foundOne)
                    rs.Remove(name);
            }
            if (rs.Count > 0)
            {
                Console.Error.WriteLine("Unknown result listeners: {0}", string.Join(",", rs));

                var knownResultListeners = ResultSettings.Current.OfType<IEnabledResource>().ToList();

                if (knownResultListeners.Count != 0)
                {
                    Console.WriteLine("Known result listeners are:");
                    foreach (var r in knownResultListeners)
                        Console.WriteLine("[{2}] {0}:  {1}", r.Name, r.ToString(), r.IsEnabled ? "x" : " ");
                }
            }

            if (rs.Count > 0)
                throw new ArgumentException();
        }

        private static List<IPlatformRequest> HandlePlatformInteractions(List<IPlatformRequest> requests, TimeSpan timeout, RequestType requestType = RequestType.Custom, string title = "")
        {
            if (requests == null)
            {
                throw new ArgumentNullException("reqs");
            }

            if (requests.Count == 0)
            {
                return requests;
            }

            if (requests.Any(x => x == null))
            {
                throw new InvalidOperationException("A platform request cannot be null");
            }

            if (timeout == TimeSpan.Zero)
            {
                if (!PlatformDialogMutex.WaitOne())
                {
                    return requests;
                }
            }
            else
            {
                if (!PlatformDialogMutex.WaitOne(timeout))
                {
                    return requests;
                }
            }

            try
            {
                Thread.Sleep(500);
                log.Flush();
                if (string.IsNullOrWhiteSpace(title) == false)
                {
                    Console.WriteLine();
                    Console.WriteLine("-- {0} --", title);
                }
                foreach (IPlatformRequest message in requests)
                {
                    start:
                    string[] split = (message.Message ?? "").Split('\\');
                    Console.WriteLine("Waiting for input: '{0}'", string.Join(" ", split.Select(s => s.Trim())));
                    if (message.ResponseType.IsEnum)
                    {
                        Console.WriteLine("Expects one of: {0}", string.Join(", ", Enum.GetNames(message.ResponseType)));
                    }
                    DateTime TimeoutTime = DateTime.Now + (timeout == TimeSpan.Zero ? TimeSpan.FromDays(1000) : timeout);
                    string read = AwaitReadline(TimeoutTime).Trim();
                    try
                    {
                        if (message.ResponseType.IsEnum)
                        {
                            Enum response;
                            bool ok = tryParseEnumString(read, message.ResponseType, out response);
                            if (ok)
                            {
                                Console.WriteLine("{0}", response);
                                message.Response = response;
                            }
                            else
                            {
                                throw new FormatException(string.Format("Unable to parse '{0}'", read));
                            }
                        }
                        else
                        {
                            message.Response = StringConvertProvider.FromString(read, message.ResponseType, null);
                            Console.WriteLine("{0}", message.Response);
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Unable to parse '{0}' as a '{1}'", read, message.ResponseType);
                        if (message.ResponseType.IsEnum)
                        {
                            Console.WriteLine("Please write one of the following:");
                            string[] names = Enum.GetNames(message.ResponseType);
                            Array values = Enum.GetValues(message.ResponseType);
                            for (int i = 0; i < names.Length; i++)
                            {
                                Console.WriteLine("* {0} ({1})", names[i], (int)values.GetValue(i));
                            }

                            Console.WriteLine();
                        }
                        goto start;
                    }
                }

            }
            finally
            {
                PlatformDialogMutex.ReleaseMutex();
            }

            return requests;
        }

        private static string AwaitReadline(DateTime TimeOut)
        {
            string Result = "";

            while (DateTime.Now <= TimeOut)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo Key = Console.ReadKey();

                    if (Key.Key == ConsoleKey.Enter)
                    {
                        return Result;
                    }
                    else
                    {
                        Result += Key.KeyChar;
                    }
                }
                else
                {
                    TestPlan.Sleep(20);
                }
            }

            Console.WriteLine();
            log.Info("Timed out while waiting for user input. Returning default answer.");
            throw new TimeoutException();
        }

        private static bool tryParseEnumString(string str, Type type, out Enum result)
        {
            try
            {   // Look for an exact match.
                result = (Enum)Enum.Parse(type, str);
                Array values = Enum.GetValues(type);
                if (Array.IndexOf(values, result) == -1)
                {
                    return false;
                }

                return true;
            }
            catch (ArgumentException)
            {
                // try a more robust parse method. (tolower, trim, '_'=' ')
                str = str.Trim().ToLower();
                string[] fixedNames = Enum.GetNames(type).Select(name => name.Trim().ToLower()).ToArray();
                for (int i = 0; i < fixedNames.Length; i++)
                {
                    if (fixedNames[i] == str || fixedNames[i].Replace('_', ' ') == str)
                    {
                        result = (Enum)Enum.GetValues(type).GetValue(i);
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }

        private void HandleSearchDirectories()
        {
            if (Search.Length > 0)
            {
                for (int i = 0; i < Search.Length; i++)
                {
                    string dir = Search[i];

                    try
                    {
                        string fullDir = Path.GetFullPath(dir);

                        if (!Directory.Exists(fullDir))
                        {
                            Console.WriteLine("Invalid plugin search path: '{0}'", fullDir);
                            throw new ArgumentException();
                        }
                        Search[i] = fullDir;
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine("Invalid plugin search path: '{0}'", dir);
                        throw;
                    }
                }
                PluginManager.DirectoriesToSearch.AddRange(Search);
                PluginManager.SearchAsync();
            }
        }

        private void HandleMetadata(List<ResultParameter> metaData)
        {
            if (Metadata.Length > 0)
            {
                foreach (string data in Metadata)
                {
                    string[] eql = data.Split('=');
                    if (eql.Length != 2)
                    {
                        log.Warning("Unable to parse metadata parameter '{0}'", data);
                        continue;
                    }
                    metaData.Add(new ResultParameter("", eql[0], eql[1], metadata: new MetaDataAttribute(false, eql[0])));
                }
            }
        }
    }
}
