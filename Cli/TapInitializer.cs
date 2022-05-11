﻿//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenTap.Diagnostic;

namespace OpenTap
{
    internal static class TapInitializer
    {

        public class InitTraceListener : ILogListener {
            public readonly List<Event> AllEvents = new List<Event>();
            public void EventsLogged(IEnumerable<Event> events)
            {
                lock(AllEvents)
                    AllEvents.AddRange(events);
            }
            public void Flush(){

            }
            public static readonly InitTraceListener Instance = new InitTraceListener();  
        }

        internal static void Initialize()
        {
            ContinueInitialization();
        }

        internal static void ContinueInitialization()
        {
            // We only needed the resolver to get into this method (requires OpenTAP, which requires netstandard)
            // Remove so we avoid race condition with OpenTap AssemblyResolver.
            OpenTap.Log.AddListener(InitTraceListener.Instance);
            PluginManager.Search();
            OpenTap.Log.RemoveListener(InitTraceListener.Instance);
        }
    }

}
