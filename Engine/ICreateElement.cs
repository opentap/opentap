namespace OpenTap
{
    /// <summary>
    /// This is a an interface that can be implemented by IList/ICollection types in order to specify how
    /// a new element can be created for insertion into that list.
    /// </summary>
    public interface ICreateElement
    {
        /// <summary> Creates an element for the given list. Note, that this should not add the element to the list.</summary>
        /// <returns> an object that can be inserted into the list. </returns>
        object CreateElement();
    }
}