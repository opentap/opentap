using System;

namespace OpenTap.Plugins.BasicSteps.Tap.Shared
{
    /// <summary>
    /// An 'Either' instance always holds either a TLeft type or a TRight type, but never both.
    /// The 'match' method can be called with function arguments:
    /// If the 'either' is a left either, the left method is invoked on the left value.
    /// Otherwise, the right method is invoked on the right value.
    /// This allows for type-safe polymorphism between e.g. enum types as used in the Install package action.
    /// </summary>
    /// <typeparam name="TLeft"></typeparam>
    /// <typeparam name="TRight"></typeparam>
    internal class Either<TLeft, TRight>
    {
        public bool isLeft { get; }
        public Either(TLeft value)
        {
            isLeft = true;
            lValue = value;
        }

        public Either(TRight value)
        {
            isLeft = false;
            rValue = value;
        }

        readonly TLeft lValue;
        readonly TRight rValue;

        public TLeft Left
        {
            get
            {
                if (isLeft)
                    return lValue;
                throw new Exception($"Tried to get 'left', but type is 'right'.");
            }
        }
        
        TRight Right
        {
            get
            {
                if (isLeft)
                    throw new Exception($"Tried to get 'right', but type is 'left'.");
                return rValue;
            }
        }

        public TResult Match<TResult>(Func<TLeft, TResult> left, Func<TRight, TResult> right) =>
            isLeft ? left(Left) : right(Right);
    }
}
