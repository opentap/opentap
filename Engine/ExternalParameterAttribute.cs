using System;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    /// <summary>
    /// Indicates that a property on a <see cref="TestStep"/> (a step setting) should be a External Parameter by default when added into Test Plan editor UI.
    /// </summary>
    public class ExternalParameterAttribute : Attribute
    {
        /// <summary>
        /// The name of the parameter.
        /// </summary>
        public string Name { get; private set; }

        /// <summary> 
        /// Create a new instance of ExternalParameterAttribute. 
        /// To be used on TestStep properties to indicates that it will be automatically added into External Parameter list when TestStep added into the test plan editor.
        /// </summary>
        /// <param name="Name">The name of the parameter.</param>
        public ExternalParameterAttribute(string Name = null)
        {
            this.Name = Name;
        }
    }
}
