using System;
using System.Collections.Generic;
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
        void Execute(PackageDef package, bool force = false);

    }

    internal static class CustomPackageActionHelper
    {
        internal static void RunCustomActions(PackageDef package, PackageActionStage stage, bool force = false)
        {
            foreach (ICustomPackageAction action in PluginManager.GetPlugins<ICustomPackageAction>()
                .Select(s => Activator.CreateInstance(s) as ICustomPackageAction)
                .Where(w => w.ActionStage == stage)
                .OrderBy(p => p.Order()))
            {
                try
                {
                    action.Execute(package, force);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Package action {action.ToString()} at stage {stage.ToString()} failed", ex);
                }
            }
        }

        internal static List<ICustomPackageData> GetAllData()
        {
            return PluginManager.GetPlugins<ICustomPackageData>()
                .Select(s => (ICustomPackageData)Activator.CreateInstance(s)).ToList();
        }
    }

}