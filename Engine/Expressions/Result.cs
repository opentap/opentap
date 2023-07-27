using System;
namespace OpenTap.Expressions
{
    readonly struct Result<T>
    {
        public static implicit operator Result<T>(T d) => Success(d);

        public string Error { get; }
        public T Value { get; }
        public bool Ok => Error == null;

        Result(T value, string error)
        {
            Value = value;
            Error = error;
        }

        public static Result<T> Success(T value) =>  new Result<T>(value, null);
        public static Result<T> NewError(string error) =>  new Result<T>(default(T), error);
        
        public T Unwrap()
        {
            if (Error == null) return Value;
            throw new Exception($"{Error}");
        }
        
        public T2 IfOK<T2>(Func<T, T2> func) =>  Error == null ? func(Value) : default(T2);
        
        public Result<T> IfThen(Action<T> func)
        {
            if (Error == null)
                func(Value);
            return this;
        }
        
        public Result<T2> IfThen<T2>(Func<T, Result<T2>> func) => Error == null ? func(Value) : Result.Error<T2>(Error);
        
        
        public bool Then(Action<T> add)
        {
            if (!Ok)
                return false;
            add(Unwrap());
            return true;
        }
    }
    
    static class Result
    {
        public static Result<T> Success<T>(T value) => Result<T>.Success(value);

        public static Result<T> Error<T>(string error) => Result<T>.NewError(error);
    }
}
