using System;
using System.Linq;

namespace OpenTap
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
    internal struct Either<TLeft, TRight>
    {
        readonly TLeft lValue;
        readonly TRight rValue;

        /// <summary>
        /// Bool indicating whether a Left or Right value is wrapped.
        /// </summary>
        public bool IsLeft { get; }

        /// <summary>
        /// Construct an instance containing a value of type TLeft.
        /// </summary>
        /// <param name="value"></param>
        public Either(TLeft value)
        {
            IsLeft = true;
            lValue = value;
            rValue = default;
        }

        /// <summary>
        /// Construct an instance containing a value of type TRight.
        /// </summary>
        /// <param name="value"></param>
        public Either(TRight value)
        {
            IsLeft = false;
            rValue = value;
            lValue = default;
        }

        /// <summary>
        /// Tries to unpack the Left value.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public TLeft Left
        {
            get
            {
                if (IsLeft)
                    return lValue;
                throw new Exception($"Tried to get 'left', but type is 'right'.");
            }
        }

        /// <summary>
        /// Tries to unpack the Right value.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public TRight Right
        {
            get
            {
                if (IsLeft)
                    throw new Exception($"Tried to get 'right', but type is 'left'.");
                return rValue;
            }
        }

        /// <summary>
        /// Unpacks the wrapped value.
        /// </summary>
        /// <returns></returns>
        public object Unpack()
        {
            if (IsLeft)
                return Left;
            return Right;
        }

        /// <summary>
        /// Unpacks the wrapped value and casts it to type T.
        /// </summary>
        /// <returns></returns>
        public T Unpack<T>()
        {
            return (T) Unpack();
        }

        /// <summary>
        /// Calls the provided method on the wrapped value.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <typeparam name="TResult"></typeparam>
        /// <returns></returns>
        public TResult Match<TResult>(Func<TLeft, TResult> left, Func<TRight, TResult> right) =>
            IsLeft ? left(Left) : right(Right);
    }
}
