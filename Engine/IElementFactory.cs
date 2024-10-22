namespace OpenTap
{
    /// <summary>
    /// Defines how the class can create list elements of a given type Or for a given member.
    /// If its the class having the list property, elementContext will be a IMemberData
    /// If its the list containing the elements, it will be an ITypeData.
    /// </summary>
    public interface IElementFactory
    {
        /// <summary> Creates a new element for the list. </summary>
        object NewElement(IMemberData member, ITypeData elementType);
    }
}
