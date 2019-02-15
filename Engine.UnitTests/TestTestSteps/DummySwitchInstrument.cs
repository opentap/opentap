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

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [DisplayName("Connection Manager Tests\\Dummy Three-way RF Switch")]
    [Description("Dummy Instrument implementing a switch to test ConnectionManager")]
    public class DummySwitchIntrument : Instrument
    {
        public SwitchPosition Position1 { get; private set; }
        public SwitchPosition Position2 { get; private set; }
        public SwitchPosition Position3 { get; private set; }

        public List<SwitchPosition> Positions { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchIntrument"/> class.
        /// </summary>
        public DummySwitchIntrument()
        {
            Name = "Sw";
            Position1 = new SwitchPosition(this, "Pos1");
            Position2 = new SwitchPosition(this, "Pos2");
            Position3 = new SwitchPosition(this, "Pos3");
            Positions = new List<SwitchPosition> { new SwitchPosition(this, "PosN1"), new SwitchPosition(this, "PosN2"), new SwitchPosition(this, "PosN3") };
        }

        public void SetPosition(SwitchPosition pos)
        {
            Position1.IsActive = false;
            Position2.IsActive = false;
            Position3.IsActive = false;
            Positions.ForEach(x => x.IsActive = false);
            pos.IsActive = true;
        }
    }
}
