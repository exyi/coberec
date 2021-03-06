using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.CoreLib
{
    public readonly struct ValidationResult<T>
    {
        public readonly ValidationErrors Errors;
        public readonly T ValueOrDefault;
        public T Expect(string message)
        {
            if (Errors == null) throw new InvalidOperationException("Invalid object created.");
            Errors.ThrowErrors($"Expected {message}, but validation has failed.");
            return ValueOrDefault;
        }

        public T Expect(Func<ValidationErrors, Exception> makeException)
        {
            if (Errors == null) throw new InvalidOperationException("Invalid object created.");
            if (!Errors.IsValid()) throw makeException(Errors);
            return ValueOrDefault;
        }

        public ValidationResult<U> Select<U>(Func<T, U> mapping, Func<ValidationErrors, ValidationErrors> errorMapping = null)
        {
            if (Errors == null) throw new InvalidOperationException("Invalid object created.");
            if (!Errors.IsValid())
                return new ValidationResult<U>(errorMapping == null ? Errors : errorMapping(Errors), default);
            else
                return new ValidationResult<U>(Errors, mapping(ValueOrDefault));
        }

        public ValidationResult<U> SelectMany<U>(Func<T, ValidationResult<U>> mapping, Func<ValidationErrors, ValidationErrors> errorMapping = null)
        {
            if (Errors == null) throw new InvalidOperationException("Invalid object created.");
            if (!Errors.IsValid())
                return new ValidationResult<U>(errorMapping == null ? Errors : errorMapping(Errors), default);
            else
                return mapping(ValueOrDefault);

        }

        public ValidationResult<TResult> SelectMany<U, TResult>(
            Func<T, ValidationResult<U>> mapping,
            Func<T, U, TResult> mapping2,
            Func<ValidationErrors, ValidationErrors> errorMapping = null)
        {
            if (Errors == null) throw new InvalidOperationException("Invalid object created.");
            if (!Errors.IsValid())
                return new ValidationResult<TResult>(errorMapping == null ? Errors : errorMapping(Errors), default);
            else
            {
                var x = mapping(ValueOrDefault);
                if (x.Errors == null) throw new InvalidOperationException("Invalid object created.");
                if (x.Errors.IsValid())
                    return new ValidationResult<TResult>(ValidationErrors.Valid, mapping2(ValueOrDefault, x.ValueOrDefault));
                else
                    return new ValidationResult<TResult>(x.Errors, default);
            }
        }

        public ValidationResult<T> NestErr(string field)
        {
            if (Errors == null) throw new InvalidOperationException("Invalid object created.");
            if (Errors.IsValid())
                return this;
            else
                return new ValidationResult<T>(Errors.Nest(field), default);
        }

        internal ValidationResult(ValidationErrors errors, T value)
        {
            this.Errors = errors;
            this.ValueOrDefault = value;
        }

        public ValidationResult<T2> Cast<T2>() where T2 : class
        {
            if (Errors == null) throw new InvalidOperationException("Invalid object created.");
            if (Errors.IsValid())
                return new ValidationResult<T2>(Errors, null);
            else
                return new ValidationResult<T2>(Errors, (T2)(object)this.ValueOrDefault);
        }
    }

    public static class ValidationResult
    {
        public static ValidationResult<T> Create<T>(T value) => new ValidationResult<T>(ValidationErrors.Valid, value);
        public static ValidationResult<T> CreateErrors<T>(ValidationErrors errors)
        {
            if (errors.IsValid()) throw new InvalidOperationException("Can not create ValidationResult.Invalid without validation errors.");
            return new ValidationResult<T>(errors, default);
        }

        public static ValidationResult<T> CreateErrorsOrValue<T>(ValidationErrors errors, T value)
        {
            if (errors.IsValid()) return Create(value);
            else return CreateErrors<T>(errors);
        }
    }
}
