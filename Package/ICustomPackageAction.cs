using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Package
{
    /// <summary>
    /// Defined stages of a package. Used by <see cref="ICustomPackageAction"/> to implement actions to a stage.
    /// </summary>
    public enum PackageActionStage
    {
        /// <summary>
        /// Package install stage (e.g. running tap package install)
        /// </summary>
        Install,
        /// <summary>
        /// Package uninstall stage (e.g. running tap package uninstall)
        /// </summary>
        Uninstall,
        /// <summary>
        /// Package create stage (e.g. running tap package create)
        /// </summary>
        Create
    }

    /// <summary>
    /// Custom data elements in package.xml inside <File> elements, to be used for custom actions by <see cref="ICustomPackageAction"/> at predefined stages (<see cref="PackageActionStage"/>)
    /// </summary>
    public interface ICustomPackageData : ITapPlugin
    {
    }

    public class CustomPackageActionArgs
    {
        public CustomPackageActionArgs(string temporaryDirectory, bool forceAction)
        {
            TemporaryDirectory = temporaryDirectory;
            ForceAction = forceAction;
        }
        public string TemporaryDirectory { get; }

        public bool ForceAction { get; } = false;
    }


    /// <summary>
    /// Custom actions for <see cref="ICustomPackageData"/> inside <File> element in package.xml files, to be executed at predefined stages (<see cref="PackageActionStage"/>)
    /// </summary>
    public interface ICustomPackageAction : ITapPlugin
    {
        /// <summary>
        /// The order of the action. Actions are executed in the order of lowest to highest.
        /// </summary>
        /// <returns></returns>
        int Order();

        /// <summary>
        /// At which stage the action should be executed
        /// </summary>
        PackageActionStage ActionStage { get; }

        /// <summary>
        /// Runs this custom action on a package. This is called after any normal operations associated with the given stage.
        /// </summary>
        bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs);
    }

    internal static class CustomPackageActionHelper
    {
        static TraceSource log =  OpenTap.Log.CreateSource("Package");
        private static List<ICustomPackageData> cachedPackageData;

        internal static void RunCustomActions(PackageDef package, PackageActionStage stage, CustomPackageActionArgs args)
        {
            log.Debug($"Running custom actions");
            foreach (ICustomPackageAction action in PluginManager.GetPlugins<ICustomPackageAction>()
                .Select(s => Activator.CreateInstance(s) as ICustomPackageAction)
                .Where(w => w.ActionStage == stage)
                .OrderBy(p => p.Order()))
            {
                Stopwatch timer = Stopwatch.StartNew();
                try
                {
                    if (action.Execute(package, args))
                    {
                        log.Info(timer, $"Package action {action.GetType().Name} completed");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(timer, $"Package action {action.ToString()} failed", ex);
                    throw;
                }
            }
        }

        internal static List<ICustomPackageData> GetAllData()
        {
            if(cachedPackageData == null)
            {
                cachedPackageData = new List<ICustomPackageData>();
                     
                var plugins = PluginManager.GetPlugins<ICustomPackageData>();
                foreach(var plugin in plugins)
                {
                    try
                    {
                        cachedPackageData.Add((ICustomPackageData)Activator.CreateInstance(plugin));
                    }
                    catch (Exception ex)
                    {
                        log.Warning($"Failed to instantiate {plugin}. Skipping plugin.");
                        log.Debug(ex);
                    }
                }
            }

            return cachedPackageData;
        }
    }
    
    public static class PackageFileExtensions
    {
        /// <summary>
        /// Returns if a specific custom data type is attached to the <see cref="PackageFile"/>.
        /// </summary>
        /// <typeparam name="T">The type that inherits from <see cref="ICustomPackageData"/></typeparam>
        /// <param name="file"></param>
        /// <returns>True if <see cref="PackageFile"/> has elements of specified custom types</returns>
        public static bool HasCustomData<T>(this PackageFile file) where T : ICustomPackageData
        {
            return file.CustomData.Any(s => s is T);
        }

        /// <summary>
        /// Returns all elements attached to the <see cref="PackageFile"/> of the specified custom data type.
        /// </summary>
        /// <typeparam name="T">The type that inherits from <see cref="ICustomPackageData"/></typeparam>
        /// <param name="file"></param>
        /// <returns>List of <see cref="ICustomPackageData"/></returns>
        public static T GetCustomData<T>(this PackageFile file) where T : ICustomPackageData
        {
            return (T)file.CustomData.FirstOrDefault(s => s is T);
        }

        /// <summary>
        /// Removes all elements of a specific custom type that are attached to the <see cref="PackageFile"/>.
        /// </summary>
        /// <typeparam name="T">The type that inherits from <see cref="ICustomPackageData"/></typeparam>
        /// <param name="file"></param>
        public static void RemoveCustomData<T>(this PackageFile file) where T : ICustomPackageData
        {
            file.CustomData.RemoveIf(s => s is T);
        }
    }

}