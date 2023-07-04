using System;
namespace OpenTap.Engine.UnitTests
{
    class UserInputTestImpl : IUserInputInterface
    {
        public Action<object> Func { get; set; } = _ => { };

        public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
        {
            Func(dataObject);
        }
    }
}
