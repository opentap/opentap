using System;
using System.Collections.Generic;
using System.Linq;
namespace OpenTap.Expressions
{
    /// <summary>Data structure for storing the result of an operation. If the operation was success full the value can be used, otherwise the error can be accessed. </summary>
    /// <typeparam name="T"></typeparam>
    readonly struct Result<T>
    {
        public static implicit operator Result<T>(T d) => Success(d);
        
        /// <summary> If the result is an error, this value will be non-null. </summary>
        public string Error { get; }
        public T Value { get; }
        public bool Ok => Error == null;

        Result(T value, string error)
        {
            Value = value;
            Error = error;
        }

        public static Result<T> Success(T value) => new Result<T>(value, null);
        public static Result<T> Fail(string error) => new Result<T>(default(T), error);
        
        /// <summary> Takes the value, but throws an exception if an error had occured. </summary>
        public T Unwrap()
        {
            if (Error == null) return Value;
            throw new Exception($"{Error}");
        }
        
        /// <summary> Continues the operation with another operation if the first went well. Otherwise it transforms the error into the new object. </summary>
        public Result<T2> Then<T2>(Func<T, Result<T2>> func) => Error == null ? func(Value) : Result.Fail<T2>(Error);
        
        /// <summary> Fails as a new object type. </summary>
        public Result<T1> FailAs<T1>()
        {
            if (Ok) throw new Exception("Cannot transform value type, only errors.");
            return Result.Fail<T1>(Error);
        }
    }
    
    static class Result
    {
        public static Result<T> Success<T>(T value) => Result<T>.Success(value);

        public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);
        public static Result<T[]> Extract<T>(this IEnumerable<Result<T>> self)
        {
            List<T> x = new List<T>();
            foreach (var elem in self)
            {
                if (!elem.Ok)
                {
                    return Fail<T[]>(elem.Error);
                }
                x.Add(elem.Value);
            }
            return x.ToArray();

        } 
    }
}
