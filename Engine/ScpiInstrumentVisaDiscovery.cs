//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Text;

namespace OpenTap
{
    /// <summary> 
    ///Implements <see cref="IDeviceDiscovery"/> for VISA Keysight instruments and searches for device aliases. 
    /// </summary>
    [Browsable(false)]
    public class KeysightVisaDeviceDiscovery : IDeviceDiscovery
    {
        private IEnumerable<string> HarvestSpecificVisaAliases(string Key)
        {
            try
            {
                var BaseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? Microsoft.Win32.RegistryView.Registry64 : Microsoft.Win32.RegistryView.Registry32);
                try
                {
                    var subkey = BaseKey.OpenSubKey(Key, Microsoft.Win32.RegistryKeyPermissionCheck.ReadSubTree);

                    if (subkey != null)
                    {
                        var names = subkey.GetValueNames();

                        subkey.Close();

                        return names;
                    }
                    else
                        return new List<string>();
                }
                finally
                {
                    BaseKey.Close();
                }
            }
            catch
            {
                return new List<string>();
            }
        }

        private IEnumerable<string> HarvestVisaAliases(string Key)
        {
            return HarvestSpecificVisaAliases(Key + "\\Devices").Concat(
                HarvestSpecificVisaAliases(Key + "\\VisaDevices"));
        }

        private IEnumerable<string> HarvestVisaAddresses(string Key)
        {
            List<string> Addresses = new List<string>();

            try
            {
                var BaseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? Microsoft.Win32.RegistryView.Registry64 : Microsoft.Win32.RegistryView.Registry32);
                try
                {
                    var subkey = BaseKey.OpenSubKey(Key, Microsoft.Win32.RegistryKeyPermissionCheck.ReadSubTree);

                    if (subkey != null)
                    {
                        foreach (var intf in subkey.GetSubKeyNames().Where(sub => sub.StartsWith("INTF")))
                        {
                            var intfkey = subkey.OpenSubKey(intf, false);

                            if (intfkey != null)
                            {
                                string VisaName = (string)intfkey.GetValue("VisaName");

                                if (VisaName != null)
                                    Addresses.AddRange(intfkey.GetSubKeyNames().Select(name => VisaName + "::" + name));

                                intfkey.Close();
                            }
                        }

                        subkey.Close();
                    }
                }
                finally
                {
                    BaseKey.Close();
                }
            }
            catch
            {
            }

            return Addresses;
        }

        /// <summary>   Retrieves addresses already associated with IO Libraries. </summary>
        /// <param name="AddressType">  Type of the address. </param>
        /// <returns>   An array of all found device addresses. </returns>
        public string[] DetectDeviceAddresses(DeviceAddressAttribute AddressType)
        {
            List<string> Keys = new List<string> {
                "SOFTWARE\\Keysight\\IO Libraries Suite\\CurrentVersion",
                "SOFTWARE\\Wow6432Node\\Keysight\\IO Libraries Suite\\CurrentVersion",
                "SOFTWARE\\Keysight\\IO Libraries Suite\\CurrentVersion",
                "SOFTWARE\\Agilent\\IO Libraries\\CurrentVersion"
            };

            List<string> Addresses = new List<string>();

            foreach (var key in Keys)
                try
                {
                    Addresses.AddRange(HarvestVisaAliases(key));
                }
                catch
                {
                }

            foreach (var key in Keys)
                try
                {
                    Addresses.AddRange(HarvestVisaAddresses(key));
                }
                catch
                {
                }

            return Addresses.Distinct().ToArray();
        }

        /// <summary>  Returns true if DeviceAddress is a VISA address.</summary>
        /// <param name="DeviceAddress"></param>
        /// <returns></returns>
        public bool CanDetect(DeviceAddressAttribute DeviceAddress)
        {
            return (DeviceAddress is VisaAddressAttribute);
        }
    }
    /// <summary>  
    /// Implements device address discovery for VISA Keysight instruments and searches for device aliases. 
    /// </summary>
    [Browsable(false)]
    public class VisaDeviceDiscovery : IDeviceDiscovery
    {
        private string[] GetDeviceAddresses()
        {
            var rm = ScpiInstrument.GetResourceManager();
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

        private IEnumerable<string> GetAliases(int rm, string address)
        {
            var aliases = new StringBuilder(1024);

            short intfType = 0;
            short intfNum = 0;
            if (Visa.viParseRsrcEx(rm, address, ref intfType, ref intfNum, null, null, aliases) == Visa.VI_SUCCESS)
            {
                if (aliases.Length != 0) 
                    return new [] {aliases.ToString()};
            }
            
            return Enumerable.Empty<string>();
        }

        /// <summary> Finds all the available VISA instrument resources.</summary>
        /// <param name="AddressType">.  </param>
        /// <returns> An array of VISA addresses (strings). </returns>
        public string[] DetectDeviceAddresses(DeviceAddressAttribute AddressType)
        {
            var baseAddresses = GetDeviceAddresses();

            // Now expand aliases
            var rm = ScpiInstrument.GetResourceManager();
            if (rm == Visa.VI_NULL) return baseAddresses;

            return baseAddresses.SelectMany(addr => GetAliases(rm, addr)).Concat(baseAddresses).ToArray();
        }

        /// <summary>  Returns true if DeviceAddress is a VISA address. </summary>
        /// <param name="DeviceAddress">. </param>
        /// <returns> </returns>
        public bool CanDetect(DeviceAddressAttribute DeviceAddress)
        {
            return (DeviceAddress is VisaAddressAttribute);
        }
    }
}
