//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Linq;
using NUnit.Framework;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ScpiAttributeTest
    {
        public enum A
        {
            [Scpi("Bee")]
            B,
            [Scpi("Cee")]
            C,
            D
        }

        public enum E
        {
            [Scpi("B21")]
            B2,
            [Scpi("Ce2e")]
            C2,
            D2
        }

        [Test]
        public void ScpiAttrTest1()
        {
            var inv = Scpi.Parse<A>("Cee");
            var result = Scpi.Format("ASD:BDS {0} {1} {2} {3} {4} {5} {6}", inv, true, false, E.B2, E.C2, E.D2, 3.14);
            var expected = "ASD:BDS Cee ON OFF B21 Ce2e D2 3.14";
            Assert.AreEqual(A.C, inv);
            Assert.AreEqual(expected, result);
        }

        class ScpiPropCls
        {
            [Scpi("ASD:BDS %")]
            public A Val1 { get; set; }

            [Scpi("ASD:BDS C|D")]
            [Scpi("SCP:IST ON")]
            public bool Val2 { get; set; }
        }

        [Test]
        public void ScpiAttrTest2()
        {
            var testInstance = new ScpiPropCls { Val1 = A.C };
            var prop = testInstance.GetType().GetProperty("Val1");

            string str = Scpi.GetUnescapedScpi(testInstance, prop)[0];
            Assert.AreEqual("ASD:BDS Cee", str);
            var prop2 = testInstance.GetType().GetProperty("Val2");
            string str2 = Scpi.GetUnescapedScpi(testInstance, prop2).OrderBy(s => s).First();
            Assert.AreEqual("ASD:BDS D", str2);
            testInstance.Val2 = true;
            str2 = Scpi.GetUnescapedScpi(testInstance, prop2).OrderBy(s => s).First();
            Assert.AreEqual("ASD:BDS C", str2);
            string str3 = Scpi.GetUnescapedScpi(testInstance, prop2).OrderBy(s => s).Skip(1).First();
            Assert.AreEqual("SCP:IST ON", str3);
        }

        [Test]
        public void ScpiAttrTest3()
        {
            object[] args = new object[] { true };
            Scpi.Format("A {0}", args);
            Assert.AreEqual(typeof(bool), args[0].GetType());
        }

        [Test]
        public void ScpiArrayTest1()
        {
            string[] result = Scpi.Parse<string[]>("1,33,\"test\",\"test,with,comma\",\"with,\"\"comma,and,inserted,quote\"\"\"");
            Assert.AreEqual(5, result.Length);

            Assert.AreEqual("1", result[0]);
            Assert.AreEqual("33", result[1]);
            Assert.AreEqual("\"test\"", result[2]);
            Assert.AreEqual("\"test,with,comma\"", result[3]);
            Assert.AreEqual("\"with,\"\"comma,and,inserted,quote\"\"\"", result[4]);
        }

        [Test]
        public void ScpiArrayTest2()
        {
            string[] result = Scpi.Parse<string[]>(",\"\", , 3 ");
            Assert.AreEqual(4, result.Length);

            Assert.AreEqual("", result[0]);
            Assert.AreEqual("\"\"", result[1]);
            Assert.AreEqual("", result[2]);
            Assert.AreEqual("3", result[3]);
        }

        [Test]
        public void ScpiArrayTest3()
        {
            string[] result = Scpi.Parse<string[]>("100,200,\"test.bin,1,2\",\"test2.bin,13,37\"");
            Assert.AreEqual(4, result.Length);

            Assert.AreEqual("100", result[0]);
            Assert.AreEqual("200", result[1]);
            Assert.AreEqual("\"test.bin,1,2\"", result[2]);
            Assert.AreEqual("\"test2.bin,13,37\"", result[3]);
        }

        [Test]
        public void ScpiArrayTest4()
        {
            string[] result = Scpi.Parse<string[]>("\"\"\"\",\"test");
            Assert.AreEqual(2, result.Length);

            Assert.AreEqual("\"\"\"\"", result[0]);
            Assert.AreEqual("\"test", result[1]);
        }

        [Test]
        public void ScpiArrayTest5()
        {
            double[] values = new double[] { 1, 2, 3, 4, 5 };
            double[] result = Scpi.Parse<double[]>(string.Join(",", values));
            Assert.IsTrue(Enumerable.SequenceEqual(values, result));   
        }

    }

    [TestFixture]
    public class DeviceDiscoveryTests
    {
        [Test]
        public void TestDiscovery()
        {
            //new KeysightVisaDeviceDiscovery().DetectDeviceAddresses(null);
            //new VisaDeviceDiscovery().DetectDeviceAddresses(null);
        }
    }
}
