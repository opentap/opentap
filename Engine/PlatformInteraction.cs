//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;

namespace OpenTap
{
    /// <summary> Platform request interface. For implementing custom platform interations. </summary>
    public interface IPlatformRequest
    {
        /// <summary>
        /// Response from the user. Note, this property has to be implemented _explicitly_ and 
        /// another property must exist with the 'Response' name and type same as ResponseType.
        /// </summary>
        object Response { get; set; }

        /// <summary>
        /// Message or query from the platform.
        /// </summary>
        string Message { get; set; }

        /// <summary>
        /// Expected response type.
        /// </summary>
        Type ResponseType { get; }
    }

    /// <summary>
    /// Easy type safe around IPlatformRequest.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PlatformRequest<T> : IPlatformRequest
    {
        /// <summary>
        /// The response.
        /// </summary>
        public T Response { get; set; }

        /// <summary>
        /// For supporting IPlatformRequest.
        /// </summary>
        object IPlatformRequest.Response
        {
            get { return Response; }
            set { Response = (T)value; }
        }

        /// <summary>
        /// Message to user.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// typeof(T).
        /// </summary>
        public Type ResponseType
        {
            get { return typeof(T); }
        }
    }

    /// <summary>
    /// Class for getting user input without using GUI.
    /// </summary>
    public class PlatformInteraction
    {
        /// <summary>
        /// Type of input request.
        /// </summary>
        public enum RequestType
        {
            /// <summary>
            ///  Custom input request.
            /// </summary>
            Custom,
            /// <summary>
            ///  Custom input request that should block a GUI from doing anything else.
            /// </summary>
            CustomModal,
            /// <summary>
            /// Platform metadata input request type.
            /// </summary>
            Metadata
        }
        /// <summary>
        /// Delegate for waiting for input.
        /// </summary>
        /// <param name="Requests"></param>
        /// <param name="Timeout"></param>
        /// <param name="requestType"></param>
        /// <param name="Title"></param>
        /// <returns></returns>
        public delegate List<IPlatformRequest> WaitForInputDelegate(List<IPlatformRequest> Requests, TimeSpan Timeout, RequestType requestType = RequestType.Custom, string Title = "");

        /// <summary>
        /// A function that will wait for user input.
        /// Default behavior is to return immediately.
        /// </summary>
        public static WaitForInputDelegate WaitForInput = (Requests, Timeout, requestType, title) => Requests;
    }
}
