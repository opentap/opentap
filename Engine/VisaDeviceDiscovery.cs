﻿//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Threading;
using System.Text;

namespace OpenTap
{
    /// <summary>  
    /// Implements device address discovery for VISA Keysight instruments and searches for device aliases. 
    /// </summary>
    class VisaDeviceDiscovery : IDeviceDiscovery
    {

        static bool IsVisaDiscoveryDisabled()
        {
            var env = Environment.GetEnvironmentVariable("OPENTAP_NO_VISA_DISCOVERY");
            if (env == null) 
                return false;

            return string.Equals(env, "True",StringComparison.OrdinalIgnoreCase) || string.Equals(env, "1");
        }
        
        static readonly bool isDisabled = IsVisaDiscoveryDisabled();
        
        private static string[] GetDeviceAddresses()
        {
            var rm = GetResourceManager();
            if (rm == Visa.VI_NULL) return new string[0];
            int search, cnt;
            StringBuilder sb = new StringBuilder(1024);

            if (Visa.viFindRsrc(rm, "?*", out search, out cnt, sb) >= Visa.VI_SUCCESS)
            {
                string[] res = new string[cnt];

                if (cnt > 0)
                    res[0] = sb.ToString();

                int i = 1;
                while ((cnt-- > 0) && (Visa.viFindNext(search, sb) == Visa.VI_SUCCESS))
                {
                    res[i++] = sb.ToString();
                }

                if (i < res.Length)
                    Array.Resize(ref res, i);

                Visa.viClose(search);

                return res;
            }

            return new string[0];
        }

        
        static bool getAliasesBroken = false;
        static string[] getAliases(int rm, string address)
        {
            if (getAliasesBroken) return Array.Empty<string>();
            using (var semaphore = new Semaphore(0, 1))
            {
                string[] result = Array.Empty<string>();
                
                TapThread.Start(() =>
                {
                    var aliases = new StringBuilder(1024);
                    short intfType = 0;
                    short intfNum = 0;
                    if (Visa.viParseRsrcEx(rm, address, ref intfType, ref intfNum, null, null, aliases) ==
                        Visa.VI_SUCCESS)
                    {
                        if (aliases.Length != 0)
                            result = new[] {aliases.ToString()};
                    }

                    semaphore.Release();
                });

                if (!semaphore.WaitOne(5000))
                {
                    getAliasesBroken = true;
                    log.Warning("VISA timed out when trying to get aliases. Skipping this from now on.");
                }

                return result;
            }
        }

        /// <summary> Ensures updating device addresses is run from the same thread.</summary>
        static WorkQueue detectAddressQueue = new WorkQueue(WorkQueue.Options.None, nameof(VisaDeviceDiscovery));
        static string[] deviceAddresses = null; // not null after first detect.
        static void updateDeviceAddresses()
        {
            try
            {
                var baseAddresses = GetDeviceAddresses();

                // Now expand aliases
                var rm = GetResourceManager();
                if (rm == Visa.VI_NULL)
                {
                    deviceAddresses = baseAddresses;
                }
                else
                {
                    deviceAddresses = baseAddresses.SelectMany(addr => getAliases(rm, addr)).Concat(baseAddresses).ToArray();
                }
            }
            catch
            {
                deviceAddresses = Array.Empty<string>();
            }
        }
        static TraceSource log = Log.CreateSource(nameof(VisaDeviceDiscovery));
        public string[] DetectDeviceAddresses(DeviceAddressAttribute AddressType)
        {
            if (isDisabled) return Array.Empty<string>();
            using (var wait = new ManualResetEvent(false))
            {
                bool doUpdate = detectAddressQueue.QueueSize == 0;
                detectAddressQueue.EnqueueWork(() =>
                {
                    if (doUpdate)
                        updateDeviceAddresses();
                    if (!wait.SafeWaitHandle.IsClosed)
                        wait.Set();
                });
                if (!wait.WaitOne(deviceAddresses == null ? 2000 : 100))
                {
                    // updateDeviceAddresses can hang forever in case of an error in the VISA installation.
                    // in this case wait for a bit and return no result. Consequent times wait a very short time.
                    Utils.ErrorOnce(log, log, "Detecting device addresses took longer than expected. This might be caused by a broken VISA installation.");
                }
                return deviceAddresses ?? Array.Empty<string>();
            }
        }

        /// <summary>  Returns true if DeviceAddress is a VISA address. </summary>
        /// <param name="DeviceAddress">. </param>
        /// <returns> </returns>
        public bool CanDetect(DeviceAddressAttribute DeviceAddress)
        {
            if (isDisabled) return false;
            
            return (DeviceAddress is VisaAddressAttribute);
        }

        static int visa_resource;
        static bool visa_tried_load;


        // Required by .NET to catch AccessViolationException.
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions] 
        internal static int GetResourceManager()
        {
            try
            {
                if (visa_resource == Visa.VI_NULL && !visa_tried_load)
                    RaiseError2(Visa.viOpenDefaultRM(out visa_resource));
            }
            catch (Exception)
            {
                visa_tried_load = true;
            }
            return visa_resource;
        }

        private static void RaiseError2(int error)
        {
            if (error < 0)
                throw new Exception("Visa failed with error " + error);
        }
    }
}
