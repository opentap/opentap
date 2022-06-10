using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using OpenTap;

namespace ProjectName
{
    [Display("ProjectNameDUT")]
    public class ProjectNameDUT : Dut
    {
        #region Settings
        // ToDo: Add property here for each parameter the end user should be able to change.
        #endregion

        /// <summary>
        /// Initializes a new instance of this DUT class.
        /// </summary>
        public ProjectNameDUT()
        {
            // ToDo: Set default values for properties / settings.
            Name = "ProjectNameDUT";
        }

        /// <summary>
        /// Opens a connection to the DUT represented by this class
        /// </summary>
        public override void Open()
        {
            base.Open();
            // TODO: establish connection to DUT here
        }

        /// <summary>
        /// Closes the connection made to the DUT represented by this class
        /// </summary>
        public override void Close()
        {
            // TODO: close connection to DUT
            base.Close();
        }
    }
}