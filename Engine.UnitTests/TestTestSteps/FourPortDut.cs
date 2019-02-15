//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [DisplayName("Connection Manager Tests\\Dummy Four Port DUT")]
    [Description("Dummy Dut with four Ports to test ConnectionManager")]
    public class FourPortDut : Dut
    {
        [XmlIgnore]
        public Port PortA { get; set; }
        [XmlIgnore]
        public Port PortB { get; set; }
        [XmlIgnore]
        public Port PortC { get; set; }
        [XmlIgnore]
        public Port PortD { get; set; }

        public FourPortDut()
        {
            PortA = new Port(this, "PortA");
            PortB = new Port(this, "PortB");
            PortC = new Port(this, "PortC");
            PortD = new Port(this, "PortD");
        }
    }
}
