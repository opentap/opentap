//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using OpenTap;

namespace OpenTap.EngineUnitTestUtils
{
    public class ResourceTest
    {
        public static void TestConformance(IResource nonOpenResource)
        {
            bool isConnected = false;
            nonOpenResource.PropertyChanged += (s, e) => isConnected = nonOpenResource.IsConnected;


            for (int i = 0; i < 5; i++)
            {
                nonOpenResource.Open();
                Assert.IsTrue(isConnected);
                nonOpenResource.Close();
                Assert.IsFalse(isConnected);
            }
            nonOpenResource.Open();
            Assert.IsTrue(isConnected);
            nonOpenResource.Open();
            Assert.IsTrue(isConnected);
            nonOpenResource.Close();
            Assert.IsFalse(isConnected);
            nonOpenResource.Close();
            Assert.IsFalse(isConnected);
            nonOpenResource.Close();
            Assert.IsFalse(isConnected);
        }
    }
}
