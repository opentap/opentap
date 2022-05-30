//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace OpenTap
{

    /// <summary> Interface for classes that can be used for cache optimizations. </summary>
    internal interface ICacheOptimizer
    {
        /// <summary> Loads / heats up the cache.</summary>
        void LoadCache();
        /// <summary> Unload / cool down the cache.</summary>
        void UnloadCache();
    }   
}
