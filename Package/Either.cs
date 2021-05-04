using System;

namespace OpenTap.Package
{
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
