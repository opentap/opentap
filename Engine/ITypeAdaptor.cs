namespace OpenTap
{
    /// <summary>
    /// Marker interface showing that a class is a type adaptor. This means that it can adapt one type to fit another type of object through this class.
    /// Whether or not the type can be used depends on the properties which it has.
    ///
    /// If it adapts a type X to type Y, it should derive or implement Y, while having a property of type X.
    ///
    /// To adapt two types X and Z to type Y, it should have two properties of type X and Z.
    /// </summary>
    public interface ITypeAdaptor
    {
        
    }
}