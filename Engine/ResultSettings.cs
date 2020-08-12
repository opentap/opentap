//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap
{
    /// <summary>
    /// A class that collects and manages the different <see cref="ResultListener"/> objects.
    /// </summary>
    [Display("Results", "Results Settings")]
    public class ResultSettings : ComponentSettingsList<ResultSettings, IResultListener>
    {
    }
}
