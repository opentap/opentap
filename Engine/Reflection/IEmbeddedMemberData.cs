namespace OpenTap;

/// <summary> This interface describes an Embedded member data.
/// When embedding an object with N properties, the embedee object will have
/// N of these virtual properties. Each with the same OwnerMember, but different InnerMember. </summary>
public interface IEmbeddedMemberData : IMemberData
{
    /// <summary> The owner member is the member of the object in which the member data is embedded. </summary>
    public IMemberData OwnerMember { get; }
    /// <summary> This is the member of the embedded object that this member data describes. </summary>
    public IMemberData InnerMember { get; }
}
