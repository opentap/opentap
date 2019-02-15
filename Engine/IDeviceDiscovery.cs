//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Specifies the device address used to establish a connection. Use inheritance to define a custom device address. />. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public abstract class DeviceAddressAttribute : Attribute
    { }

    /// <summary>   Interface for defining a custom device address discovery system. Implement this along with a specialization of <see cref="DeviceAddressAttribute"/>.  </summary>
    [Display("Device Discovery")]
    public interface IDeviceDiscovery : ITapPlugin
    {
        /// <summary> Returns true if this IDeviceDiscovery can look up addresses for the type of device address supplied.   </summary>
        /// <param name="DeviceAddress"> The device address kind. </param>
        /// <returns></returns>
        bool CanDetect(DeviceAddressAttribute DeviceAddress);

        /// <summary> Looks up all the device addresses available for a given device address type. </summary>
        /// <param name="AddressType">  Type of the address. </param>
        /// <returns>   A string[]. </returns>
        string[] DetectDeviceAddresses(DeviceAddressAttribute AddressType);
    }

    /// <summary> 
    /// Identifies a string as a VISA address and finds a list of discovered VISA addresses. 
    /// </summary>

    public class VisaAddressAttribute : DeviceAddressAttribute
    { }
}
