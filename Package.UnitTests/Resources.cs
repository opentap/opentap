//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using System.Reflection;

namespace OpenTap.Package.UnitTests
{
    class Resources
    {
        public static Stream GetEmbeddedStream(string testTapPlan)
        {
            // Get TestPlan file for testing
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = String.Format("OpenTap.Package.UnitTests.{0}", testTapPlan.Replace('\\', '.'));
            return assembly.GetManifestResourceStream(resourceName);
        }

        public static string GetEmbedded(string path)
        {
            using (var r = GetEmbeddedStream(path))
            {
                using (var r2 = new StreamReader(r))
                {
                    return r2.ReadToEnd();
                }
            }
        }

    }
}
