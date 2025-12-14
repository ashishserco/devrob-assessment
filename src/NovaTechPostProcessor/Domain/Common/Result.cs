using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaTechPostProcessor.Domain.Common
{
    /// <summary>
    /// Result pattern implementation for railway-oriented programming.
    /// Enables functional error handling without throwing exceptions for business logic failures.
    /// Follows DDD principles for domain error modeling.
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        protected Result(bool isSuccess, string error)
        {
            if (isSuccess && !string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Success result cannot have an error message");
            if (!isSuccess && string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Failure result must have an error message");

            IsSuccess = isSuccess;
            Error = error ?? string.Empty;
        }

        public static Result Success() => new(true, string.Empty);
        public static Result Failure(string error) => new(false, error);
        public static Result<T> Success<T>(T value) => new(value, true, string.Empty);
        public static Result<T> Failure<T>(string error) => new(default(T)!, false, error);

        /// <summary>
        /// Combines multiple results using AND logic.
        /// If any result fails, returns the first failure.
        /// </summary>
        public static Result Combine(params Result[] results)
        {
            var failures = results.Where(r => r.IsFailure).ToArray();
            if (failures.Any())
            {
                return Failure(string.Join(Environment.NewLine, failures.Select(f => f.Error)));
            }
            return Success();
        }
    }

    /// <summary>
    /// Generic result for operations that return a value.
    /// </summary>
    public class Result<T> : Result
    {
        public T Value { get; }

        internal Result(T value, bool isSuccess, string error) : base(isSuccess, error)
        {
            Value = value;
        }

        public static implicit operator Result<T>(T value) => Success(value);

        /// <summary>
        /// Monadic bind operation for chaining result operations.
        /// Enables railway-oriented programming patterns.
        /// </summary>
        public Result<TNew> Map<TNew>(Func<T, TNew> func)
        {
            if (IsFailure)
                return Result.Failure<TNew>(Error);

            try
            {
                return Result.Success(func(Value));
            }
            catch (Exception ex)
            {
                return Result.Failure<TNew>($"Mapping operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Monadic bind operation for chaining result-returning operations.
        /// </summary>
        public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> func)
        {
            if (IsFailure)
                return Result.Failure<TNew>(Error);

            try
            {
                return func(Value);
            }
            catch (Exception ex)
            {
                return Result.Failure<TNew>($"Bind operation failed: {ex.Message}");
            }
        }
    }
}