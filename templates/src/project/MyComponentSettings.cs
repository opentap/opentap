using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using OpenTap;

namespace ProjectName
{
    [Display("MyComponentSettings", Description: "Insert a description here", Group: "ProjectName")]
    public class MyComponentSettings : ComponentSettings<MyComponentSettings>
    {
        // TODO: Add settings here as properties, use DisplayAttribute to indicate to the GUI
        //       how the setting should be displayed.
        //       Example:
        [Display("Example Setting", "Insert a description here", "Setting Group")]
        public bool ExampleSetting { get; set; }
    }
}
