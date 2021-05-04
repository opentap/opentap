using System;

namespace OpenTap.Package
{
    internal struct Either<TLeft, TRight>
    {
        private readonly bool isLeft;
        public Either(TLeft value)
        {
            isLeft = true;
            right = default;
            left = value;
        }

        public Either(TRight value)
        {
            isLeft = false;
            left = default;
            right = value;
        }

        readonly TLeft left;
        readonly TRight right;

        public TLeft Left
        {
            get
            {
                if (isLeft)
                    return left;
                throw new Exception($"Tried to get 'left', but type is 'right'.");
            }
        }
        
        TRight Right
        {
            get
            {
                if (isLeft)
                    throw new Exception($"Tried to get 'right', but type is 'left'.");
                return right;
            }
        }

        public TResult Match<TResult>(Func<TLeft, TResult> WhenLeft, Func<TRight, TResult> WhenRight) =>
            isLeft ? WhenLeft(Left) : WhenRight(Right);
    }
}
