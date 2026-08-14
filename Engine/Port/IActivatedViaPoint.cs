namespace OpenTap
{
    public interface IViaPoint : IConstResourceProperty
    {
        bool IsActive { get; }
        string Name { get; }
    }
    
    /// <summary> Represents a via point that can be activated.     </summary>
    public interface IActivatedViaPoint : IViaPoint
    {
        /// <summary> Activates a via point. </summary>
        void Activate();
    }

    /// <summary> A via point that can calculate dynamic loss. </summary>
    public interface IDynamicLossViapoint : IViaPoint
    {
        /// <summary> Gets the loss for a specific frequency.</summary>
        /// <param name="frequency"></param>
        /// <returns></returns>
        double GetLoss(double frequency);
    }
}
