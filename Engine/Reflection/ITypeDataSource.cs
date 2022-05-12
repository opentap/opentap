using System.Collections.Generic;

namespace OpenTap
{
    /// <summary>  Specifies that an object is the source of types. </summary>
    public interface ITypeDataSource
    {
        /// <summary> The location of the types. This can be a file location, URL or null. </summary>
        string Location { get; }
        
        /// <summary> The types which this type data source provides. </summary>
        IEnumerable<ITypeData> Types { get; }

        /// <summary> Attributes associated with this typed data source.</summary>
        IEnumerable<object> Attributes { get; }
        
        /// <summary> Which other type data sources this type data source references. </summary>
        IEnumerable<ITypeDataSource> References { get; }
    }
}