//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>
    /// Interface used to talk to remote applications that run the TAP engine. Most of these methods are asynchronous. 
    /// Each call adds an item to the work queue.
    /// </summary>
    public interface IRemoteEngine : ITapPlugin
    {
        /// <summary>
        /// Opens a connection to the server. The address to connect to should be specified first by setting properties on this object.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        void Close();

        /// <summary>
        /// Loads a given TestPlan on the server.
        /// </summary>
        void LoadTestPlan(System.IO.Stream testPlanFile);

        /// <summary>
        /// Runs the loaded TestPlan. Not Blocking.
        /// This corresponds to the <see cref="TestPlan.Execute()"/>.
        /// </summary>
        void RunTestPlan();

        /// <summary>
        /// Opens all the resources needed by the loaded TestPlan on the server. Not Blocking.
        /// This corresponds to the <see cref="TestPlan.Open()"/>
        /// </summary>
        void OpenTestPlan();

        /// <summary>
        /// Closes all resources needed by the loaded TestPlan on the server. Not Blocking. Corresponds to the <see cref="TestPlan.Close"/>.
        /// </summary>
        void CloseTestPlan();

        /// <summary>
        /// Adds a ResultListener that will get called when the remote TestPlan is run.
        /// </summary>
        void AddResultListener(IResultListener listener);

        /// <summary>
        /// Adds a TraceListener that will get called when messages are logged on the remote server.
        /// </summary>
        void AddTraceListener(TraceListener listener);

        /// <summary>
        /// Changes the settings directory. The server will load <see cref="ComponentSettings"/> from the specified directory.
        /// These settings include <see cref="ResultSettings"/>, which will clear any ResultListeners added using <see cref="AddResultListener"/>
        /// </summary>
        void SetSettingsDirectory(string dir);

        /// <summary>
        /// Aborts the currently running test plan. Not done synchronously. Wait for <see cref="ResultListener.OnTestPlanRunStart(TestPlanRun)"/> to do this.  
        /// </summary>
        void AbortRun();

        /// <summary>
        /// Forces the test plan to abort as soon as possible. Use with caution. Not done synchronously. Wait for ResultListener.OnTestPlanRunStart to do this.
        /// </summary>
        void ForceAbortRun();

        /// <summary>
        /// Waits for the work queue to become empty. May throw an aggregate exception if one or more exception occurred remotely. since the last method was called. Will return true if the wait succeeded, false if it timed out.
        /// </summary>
        bool Wait(TimeSpan timeout);

        /// <summary>
        /// Sets a test plan mask variable in the loaded test plan. Does nothing if plan is not loaded.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void SetExternalParameter(string name, object value);

        /// <summary>
        /// Gets the test plan mask variable in the loaded test plan. Returns an empty list if no plan is loaded.
        /// </summary>
        /// <returns></returns>
        List<string> GetExternalParameters();

        /// <summary>
        /// Gets a test plan mask variable from the loaded test plan. Returns null if no plan is loaded.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        object GetExternalParameter(string name);


    }

    /// <summary>
    /// Interface implemented in conjunction with <see cref="IRemoteEngine"/> to allow easy start of new remote removable applications.
    /// </summary>
    public interface IRemoteProcess : ITapPlugin
    {
        /// <summary>
        /// Starts a new server process. The address to which the new server should listen to should be specified first by setting properties on this object.
        /// </summary>
        /// <returns>The process Id of the new process.</returns>
        int StartLocalServerProcess();

    }

    /// <summary>
    /// Helper extensions for IRemoteEngine.
    /// </summary>
    public static class RemoteEngineExtensions
    {
        /// <summary>
        /// Extension to make calling IRemoteEngine.Wait easier.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public static bool Wait(this IRemoteEngine engine, int milliseconds = 5000)
        {
            return engine.Wait(TimeSpan.FromMilliseconds(milliseconds));
        }

    }
}
