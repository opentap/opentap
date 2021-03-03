using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Read-only collection of parameters where parameters can be looked up by name.
    /// </summary>
    public class ParameterCollection : ReadOnlyCollection<IParameter>, IParameters
    {
        /// <summary> Gets a parameter by name.</summary>
        /// <param name="Name"></param>
        public IConvertible this[string Name] => this.FirstOrDefault(x => x.Name == Name)?.Value;

        /// <summary> Creates a new parameter collection from an existing list of parameters. </summary>
        public ParameterCollection(IEnumerable<IParameter> list) : base(list.ToArray())
        {
            
        }

        internal static readonly ParameterCollection Empty = new ParameterCollection(Enumerable.Empty<IParameter>());
    }
}