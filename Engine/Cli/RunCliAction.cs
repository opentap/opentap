//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Cli
{
    internal enum ExitStatus : int
    {
        TestPlanInconclusive = 20,
        TestPlanFail = 30,
        TestPlanError = 50,
        LoadError = 70,
    }
    /// <summary>
    /// Test plan run CLI action. Execute a test plan with 'tap.exe run test.TapPlan'
    /// </summary>
    [Display("run", Description: "Run a test plan.")]
    public class RunCliAction : ICliAction
    {
        /// <summary>
        /// Specify a bench settings profile from which to load the bench settings. The parameter given here should correspond to the name of a subdirectory of %TAP_PATH%/Settings/Bench. If not specified, %TAP_PATH%/Settings/Bench/Default is used.
        /// </summary>
        [CommandLineArgument("settings", Description = "Specify a bench settings profile from which to load\nthe bench settings. The parameter given here should correspond\nto the name of a subdirectory of <OpenTAP installation dir>/Settings/Bench.\nIf not specified, <OpenTAP installation dir>/Settings/Bench/Default is used.")]
        public string Settings { get; set; } = "";

        /// <summary>
        /// Additional directories to be searched for plugins. This option may be used multiple times, e.g., --search dir1 --search dir2.
        /// </summary>
        [CommandLineArgument("search", Description = "Additional directories to be searched for plugins.\nThis option may be used multiple times, e.g., --search dir1 --search dir2.")]
        [Browsable(false)]
        public string[] Search { get; set; } = new string[0];

        /// <summary>
        /// Set a resource metadata parameter. Use the syntax parameter=value, e.g., --metadata dut-id=5. This option may be used multiple times.
        /// </summary>
        [CommandLineArgument("metadata", Description = "Set a resource metadata parameter.\nUse the syntax parameter=value, e.g., --metadata dut-id=5.\nThis option may be used multiple times.")]
        public string[] Metadata { get; set; } = new string[0];

        /// <summary>
        /// Never prompt for user input.
        /// </summary>
        [CommandLineArgument("non-interactive", Description = "Never prompt for user input.")]
        public bool NonInteractive { get; set; } = false;

        /// <summary>
        /// Set an external test plan parameter. Use the syntax parameter=value, e.g., -e delay=1.0. This option may be used multiple times, or a .csv file containing a \"parameter, value\" pair on each line can be specified as -e file.csv.
        /// </summary>
        [CommandLineArgument("external", ShortName = "e", Description = "Set an external test plan parameter.\nUse the syntax parameter=value, e.g., -e delay=1.0.\nThis option may be used multiple times, or a .csv file containing a\n\"parameter, value\" pair on each line can be specified as -e file.csv.")]
        public string[] External { get; set; } = new string[0];

        /// <summary>
        /// Try setting an external test plan parameter, ignoring errors if it does not exist in the test plan. Use the syntax parameter=value, e.g., -t delay=1.0. This option may be used multiple times
        /// </summary>
        [CommandLineArgument("try-external", ShortName = "t", Description = "Try setting an external test plan parameter,\nignoring errors if it does not exist in the test plan.\nUse the syntax parameter=value, e.g., -t delay=1.0.\nThis option may be used multiple times.")]
        public string[] TryExternal { get; set; } = new string[0];

        /// <summary>
        /// List the available external test plan parameters.
        /// </summary>
        [CommandLineArgument("list-external-parameters", Description = "List the available external test plan parameters.")]
        public bool ListExternal { get; set; } = false;

        /// <summary>
        /// Enable a subset of the currently configured result listeners given as a comma-separated list, e.g., --results SQLite,CSV. To disable all result listeners use --results \"\".
        /// </summary>
        [CommandLineArgument("results", Description = "Enable a subset of the currently configured result listeners\ngiven as a comma-separated list, e.g., --results SQLite,CSV.\nTo disable all result listeners use --results \"\".")]
        public string Results { get; set; }

        /// <summary>
        /// Ignore the errors for deserialization of test plan
        /// </summary>
        [CommandLineArgument("ignore-load-errors", Description = "Ignore the errors during loading of test plan.")]
        public bool IgnoreLoadErrors { get; set; } = false;

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
        

        /// <summary>
        /// Executes test plan
        /// </summary>
        /// <returns></returns>
        public int Execute(CancellationToken cancellationToken)
        {   
            List<ResultParameter> metaData = new List<ResultParameter>();
            HandleMetadata(metaData);

            string planToLoad = null;

            // If the --search argument is used, add the --ignore-load-errors to fix any load issues.
            if (Search.Any())
            {
                // Warn
                log.Warning("Argument '--search' is deprecated. The '--ignore-load-errors' argument has been added to avoid potential test plan load issues.");
                IgnoreLoadErrors = true;
            }

            try
            {
                planToLoad = !string.IsNullOrWhiteSpace(TestPlanPath) ? Path.GetFullPath(TestPlanPath) : null;
            }
            catch (Exception)
            {
                Console.WriteLine("Invalid path: '{0}'", TestPlanPath);
                Console.WriteLine("The path only supports a valid file path.");
                log.Info("Unable to continue. Now exiting tap.exe.");
                return (int)ExitCodes.ArgumentError;
            }

            try
            {
                HandleSearchDirectories();
            }
            catch (ArgumentException)
            {
                log.Info("Unable to continue. Now exiting tap.exe.");
                return (int)ExitCodes.ArgumentError;
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
                    log.Info("Unable to continue. Now exiting tap.exe.");
                    return (int)ExitCodes.ArgumentError;
                }
            }

            if (planToLoad == null)
            {
                Console.WriteLine("Please supply a valid test plan path as an argument.");
                log.Info("Unable to continue. Now exiting tap.exe.");
                return (int)ExitCodes.ArgumentError;
            }

            // Load TestPlan:
            if (!File.Exists(planToLoad))
            {
                log.Error("File '{0}' does not exist.", planToLoad);
                log.Flush();
                Thread.Sleep(100);
                log.Info("Unable to continue. Now exiting tap.exe.");
                return (int)ExitCodes.ArgumentError;
            }

            try
            {
                HandleExternalParametersAndLoadPlan(planToLoad);
            }
            catch (TestPlan.PlanLoadException ex)
            {
                // at this point the log messages are already written out saying what went wrong.
                log.Error("Unable to load test plan.");
                log.Debug(ex);
                return (int)ExitStatus.LoadError;
            }
            catch (OperationCanceledException) when (TapThread.Current.AbortToken.IsCancellationRequested)
            {
                log.Info("tap.exe was exited due to an interrupt. (CTRL+C)");
                return (int)ExitCodes.UserCancelled;
            }
            catch (ArgumentException ex)
            {
                if(!string.IsNullOrWhiteSpace(ex.Message))
                    log.Error(ex.Message);
                return (int)ExitCodes.ArgumentError;
            }
            catch (Exception e)
            {
                log.Error("Caught error while loading test plan: '{0}'", e.Message);
                log.Debug(e);
                return (int)ExitStatus.LoadError;
            }

            log.Info("Test Plan: {0}", Plan.Name);

            if (ListExternal)
            {
                PrintExternalParameters(log);
                return (int)ExitCodes.Success;
            }

            Verdict verdict = TestPlanRunner.RunPlanForDut(Plan, metaData, cancellationToken);

            if (TapThread.Current.AbortToken.IsCancellationRequested)
            {
                log.Info("tap.exe was exited due to an interrupt. (CTRL+C)");
                return (int)ExitCodes.UserCancelled;
            }

            if (verdict == Verdict.Inconclusive)
                return (int)ExitStatus.TestPlanInconclusive;
            if (verdict == Verdict.Fail)
                return (int)ExitStatus.TestPlanFail;
            if (verdict > Verdict.Fail)
                return (int)ExitStatus.TestPlanError;

            return (int)ExitCodes.Success;
        }

        private void HandleExternalParametersAndLoadPlan(string planToLoad)
        {
            
            List<string> values = new List<string>();
            var serializer = new TapSerializer();
            var extparams = serializer.GetSerializer<Plugins.ExternalParameterSerializer>();
            if (External.Length > 0)
                values.AddRange(External);
            if (TryExternal.Length > 0)
                values.AddRange(TryExternal);
            Plan = new TestPlan();
            List<string> externalParameterFiles = new List<string>();
            foreach (var externalParam in values)
            {
                int equalIdx = externalParam.IndexOf('=');
                if (equalIdx == -1)
                {
                    externalParameterFiles.Add(externalParam);
                    continue;
                }
                var name = externalParam.Substring(0, equalIdx);
                var value = externalParam.Substring(equalIdx + 1);
                extparams.PreloadedValues[name] = value;
            }
            var log = Log.CreateSource("CLI");

            var timer = Stopwatch.StartNew();
            using (var fs = new FileStream(planToLoad, FileMode.Open, FileAccess.Read))
            {
                // only cache the XML if there are no external parameters.
                bool cacheXml = values.Any() == false && externalParameterFiles.Any() == false;
                
                
                Plan = TestPlan.Load(fs, planToLoad, cacheXml, serializer, IgnoreLoadErrors);
                log.Info(timer, "Loaded test plan from {0}", planToLoad);
            }

            if (externalParameterFiles.Count > 0)
            {
                var importers = CreateInstances<IExternalTestPlanParameterImport>();
                var CurDir = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(EngineSettings.StartupDir);
                    foreach (var file in externalParameterFiles)
                    {
                        var ext = Path.GetExtension(file);
                        log.Info($"Loading external parameters from '{file}'.");
                        var importer = importers
                                // find importer that accepts that extension, case-insensitively. 
                            .FirstOrDefault(i => string.Compare(i.Extension, ext, StringComparison.InvariantCultureIgnoreCase) == 0);
                        if (importer != null)
                        {
                            importer.ImportExternalParameters(Plan, file);
                        }
                        else
                        {
                            throw new ArgumentException($"No installed plugins provide loading of external parameters from '{ext}' files. No external parameters loaded from '{file}'.");
                        }
                    }
                }
                finally
                {
                    Directory.SetCurrentDirectory(CurDir);
                }
            }

            if (External.Length > 0)
            {   // Print warnings if an --external parameter was not in the test plan. 

                foreach (var externalParam in External)
                {
                    var equalIdx = externalParam.IndexOf('=');
                    if (equalIdx == -1) continue;

                    var name = externalParam.Substring(0, equalIdx);
                    if (Plan.ExternalParameters.Get(name) != null) continue;

                    log.Warning("External parameter '{0}' does not exist in the test plan.", name);
                    log.Warning("Statement '{0}' has no effect.", externalParam);
                    throw new ArgumentException("");
                }
            }
        }

        private void PrintExternalParameters(TraceSource log)
        {
            var annotation = AnnotationCollection.Annotate(Plan).Get<IMembersAnnotation>();
            log.Info("Listing {0} External Test Plan Parameters:", Plan.ExternalParameters.Entries.Count);
            foreach (var member in annotation.Members)
            {
                if (member.Get<IMemberAnnotation>()?.Member is ParameterMemberData param)
                {
                    var multiValues = member.Get<IMultiSelectAnnotationProxy>()?.SelectedValues;
                    string printStr = "";
                    if (multiValues != null)
                    {
                        foreach (var val in multiValues)
                            printStr += string.Format("{0} | ", val.Get<IStringReadOnlyValueAnnotation>()?.Value ?? val.Get<IObjectValueAnnotation>()?.Value?.ToString() ?? "");
                        printStr = printStr.Remove(printStr.Length - 3);    // Remove trailing delimiter
                    }
                    else
                        printStr = member.Get<IStringReadOnlyValueAnnotation>()?.Value ?? member.Get<IObjectValueAnnotation>()?.Value?.ToString();

                    log.Info("  {0} = {1}", param.Name, printStr);

                    if (member.Get<IAvailableValuesAnnotationProxy>() is IAvailableValuesAnnotationProxy avail)
                    {
                        log.Info("    Available Values:");
                        foreach (var val in avail.AvailableValues ?? new AnnotationCollection[0])
                            log.Info("      {0}", val.Get<IStringReadOnlyValueAnnotation>()?.Value ?? val.Get<IObjectValueAnnotation>()?.Value?.ToString() ?? "");
                    }
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
