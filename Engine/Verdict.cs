//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
namespace OpenTap
{
    /// <summary>
    /// Enumeration containing the verdict types used for <see cref="TestRun.Verdict"/> and <see cref="TestRun.Verdict"/> properties.
    /// </summary>
    public enum Verdict : int
    {

        /// <summary>
        /// No verdict has been set. This is the default value.
        /// </summary>
        [Display("Not Set")]
        NotSet = 0,
        /// <summary>
        /// Test passed. 
        /// </summary>
        Pass = 10,
        /// <summary>
        /// Test had an inconclusive result. 
        /// </summary>
        Inconclusive = 20,
        /// <summary>
        /// Test failed. 
        /// </summary>
        Fail = 30,
        /// <summary>
        /// Test was aborted. 
        /// </summary>
        Aborted = 40,
        /// <summary>
        /// Test failed due to an exception or another procedural error. Such as no instrument/DUT connection. 
        /// </summary>
        Error = 50
    }
}
