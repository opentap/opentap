using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Read-only collection of parameters where parameters can be looked up by name.
    /// </summary>
    class ParameterCollection : ReadOnlyCollection<IParameter>, IParameters
    {
        IParameter GetParameterByObjectType(string name) => this.FirstOrDefault(x => x.ObjectType == name);
        IParameter GetParameter(string name) => this.FirstOrDefault(x => x.Name == name);

        /// <summary> Gets a parameter by name.</summary>
        /// <param name="name"></param>
        public IConvertible this[string name] => (GetParameterByObjectType(name) ?? GetParameter(name))?.Value; 

        /// <summary> Creates a new parameter collection from an existing list of parameters. </summary>
        public ParameterCollection(IParameter[] list) : base(list)
        {
            
        }

        internal new static readonly ParameterCollection Empty = new ParameterCollection(Array.Empty<IParameter>());
    }
}