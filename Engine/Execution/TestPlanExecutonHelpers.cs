//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenTap
{
    static class TestPlanExecutonHelpers
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor");

        internal static void PrintWaitingMessage(IEnumerable<IResource> resources)
        {
            Log.Info("Waiting for resources to open:");
            foreach (var resource in resources)
            {
                if (resource.IsConnected) continue;
                Log.Info(" - {0}", resource);
            }
        }
        /// <summary>
        /// Calls the PromptForDutMetadata delegate for all referenced DUTs.
        /// </summary>
        internal static void StartResourcePromptAsync(TestPlanRun planRun, IEnumerable<IResource> _resources)
        {
            var resources = _resources.Where(x => x != null).ToArray();

            List<Type> componentSettingsWithMetaData = new List<Type>();
            var componentSettings = PluginManager.GetPlugins<ComponentSettings>();
            bool AnyMetaData = false;
            planRun.PromptWaitHandle.Reset();

            try
            {
                foreach (var setting in componentSettings)
                {
                    foreach (var member in setting.GetMembers())
                    {
                        var attr = member.GetAttribute<MetaDataAttribute>();
                        if (attr != null && attr.PromptUser)
                        {
                            AnyMetaData = true;
                            componentSettingsWithMetaData.Add(setting);
                        }
                    }
                }

                foreach (var resource in resources)
                {
                    var type = TypeData.GetTypeData(resource);
                    foreach (var __prop in type.GetMembers())
                    {
                        IMemberData prop = __prop;
                        var attr = prop.GetAttribute<MetaDataAttribute>();
                        if (attr != null && attr.PromptUser)
                            AnyMetaData = true;
                    }
                }
            }
            catch
            {
                // this is just a defensive catch to make sure that the waithandle is not left unset (and we risk waiting for it indefinitely)
                planRun.PromptWaitHandle.Set();
                throw;
            }

            if (AnyMetaData && EngineSettings.Current.PromptForMetaData)
            {
                TapThread.Start(() =>
                {
                    try
                    {
                        List<object> objects = new List<object>();
                        objects.AddRange(componentSettingsWithMetaData.Select(ComponentSettings.GetCurrent));
                        objects.AddRange(resources);

                        planRun.PromptedResources = resources;
                        var obj = new MetadataPromptObject { Resources = objects };
                        UserInput.Request(obj, false);
                        if (obj.Response == MetadataPromptObject.PromptResponse.Abort)
                            planRun.MainThread.Abort();
                    }
                    catch (Exception e)
                    {
                        Log.Debug(e);
                        planRun.MainThread.Abort("Error occured while executing platform requests. Metadata prompt can be disabled from the Engine settings menu.");
                    }
                    finally
                    {
                        planRun.PromptWaitHandle.Set();
                    }
                }, name: "Request Metadata");
            }
            else
            {
                planRun.PromptWaitHandle.Set();
            }
        }


    }


    // This object has a special data annotator that embeds metadata properties
    // from Resources into it.
    class MetadataPromptObject
    {
        public string Name { get; private set; } = "Please enter test plan metadata.";
        [Browsable(false)]
        public IEnumerable<object> Resources { get; set; }

        public enum PromptResponse
        {
            OK,
            Abort
        }

        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        [Submit]
        public PromptResponse Response { get; set; }

    }
}
