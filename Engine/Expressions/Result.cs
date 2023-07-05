using System;
namespace OpenTap.Expressions
{
    readonly struct Result<T>
    {
        public static implicit operator Result<T>(T d) => Success(d);
        
        readonly string error;
        readonly T value;
        Result(T value, string error)
        {
            this.value = value;
            this.error = error;
        }

        public static Result<T> Success(T value) =>  new Result<T>(value, null);
        public static Result<T> Error(string error) => new Result<T>(default(T), error);
        
        public T Unwrap()
        {
            if (error == null) return value;
            throw new Exception($"{error}");
        }

        public string Error() => error;
        public T2 IfOK<T2>(Func<T, T2> func)
        {
            if (error == null)
            {
                return func(value);
            }
            return default(T2);
        }
        
        public Result<T2> IfThen<T2>(Func<T, T2> func)
        {
            if (error == null)
            {
                return Result<T2>.Success(func(value));
            }
            return Result<T2>.Error(error);
        }
        public bool Ok() => error == null;
    }
    
    static class Result
    {
        public static Result<T> Success<T>(T value) => Result<T>.Success(value);

        public static Result<T> Error<T>(string error) => Result<T>.Error(error);
    }
}
