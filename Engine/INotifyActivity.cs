//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap
{
    /// <summary>
    /// Notifies clients that the object is active.
    /// </summary>
    public interface INotifyActivity
    {
        /// <summary>
        /// Invoked on activity.
        /// </summary>
        event EventHandler<EventArgs> Activity;

        /// <summary>
        /// Triggers the ActivityStateChanged event.
        /// </summary>
        void OnActivity();
    }
}
