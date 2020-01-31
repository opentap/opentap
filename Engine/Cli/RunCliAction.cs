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
        /// Specify a bench settings profile from which to load\nsettings. The parameter given here should correspond to the name of a subdirectory of %TAP_PATH%/Settings/Bench. If not specified the settings from OpenTAP GUI are used.
        /// </summary>
        [CommandLineArgument("settings", Description = "Specify a bench settings profile from which to load\nsettings. The parameter given here should correspond\nto the name of a subdirectory of %TAP_PATH%/Settings/Bench.\nIf not specified, %TAP_PATH%/Settings/Bench/CurrentProfile is used.")]
        public string Settings { get; set; } = "";

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
        [CommandLineArgument("external", ShortName = "e", Description = "Sets an external test plan parameter. \nUse the syntax parameter=value, e.g. \"-e delay=1.0\". The argument can be used multiple times \nor a .csv file containing sets of parameters can be specified \"-e file.csv\".")]
        public string[] External { get; set; } = new string[0];

        /// <summary>
        /// Try setting an external test plan parameter, ignoring errors if it does not exist in the test plan. Can be used multiple times. Use the syntax parameter=value, e.g. \"-t delay=1.0\".
        /// </summary>
        [CommandLineArgument("try-external", ShortName = "t", Description = "Try setting an external test plan parameter,\nignoring errors if it does not exist in the test plan.\nCan be used multiple times. Use the syntax parameter=value,\ne.g. \"-t delay=1.0\".")]
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
        public string Results { get; set; }

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

            Console.WriteLine($"OpenTAP Command Line Interface {FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion}\n");

            if (!string.IsNullOrWhiteSpace(Settings))
            {
                TestPlanRunner.SetSettingsDir(Settings);
            }


            if (!NonInteractive && UserInput.Interface == null)
            {
                CliUserInputInterface.Load();
            }


            if (Results != null)
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

            Verdict verdict = TestPlanRunner.RunPlanForDut(Plan, metaData, cancellationToken);

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
            Plan = new TestPlan();
            List<string> externalParameterFiles = new List<string>();
            foreach (var externalParam in values)
            {
                int equalIdx = externalParam.IndexOf("=");
                if (equalIdx == -1)
                {
                    //try "Import External Parameters File"
                    try
                    {
                        externalParameterFiles.Add(externalParam);
                        continue;
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
            var log = Log.CreateSource("CLI");
            Plan = (TestPlan)serializer.DeserializeFromFile(planToLoad, type: TypeData.FromType(typeof(TestPlan)));
            if (externalParameterFiles.Count > 0)
            {
                var importers = CreateInstances<IExternalTestPlanParameterImport>();
                foreach (var file in externalParameterFiles)
                {
                    log.Info("Loading external parameters from '{0}'.", file);
                    var importer = importers.FirstOrDefault(i => i.Extension == Path.GetExtension(file)); 
                    importer?.ImportExternalParameters(Plan, file);
                }
            }
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
                Console.WriteLine("Known result listeners are:");
            }

            foreach (var r in ResultSettings.Current.OfType<IEnabledResource>().ToList())
                Console.WriteLine("[{2}] {0}:  {1}", r.Name, r.ToString(), r.IsEnabled ? "x" : " ");

            if (rs.Count > 0)
                throw new ArgumentException();
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
                    TapThread.Sleep(20);
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
