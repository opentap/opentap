using System;

namespace OpenTap
{
    /// <summary>
    /// UserInputInterface implementation which returns immediately. Intended for non-interactive use.
    /// </summary>
    internal class NonInteractiveUserInputInterface : IUserInputInterface
    {
        void IUserInputInterface.RequestUserInput(object dataObject, TimeSpan timeout, bool modal)
        {
            
        }

        internal static bool IsSet() => UserInput.GetInterface() is NonInteractiveUserInputInterface;
    }
}
