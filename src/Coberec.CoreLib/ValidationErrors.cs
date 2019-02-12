using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.CoreLib
{
    public class ValidationErrors
    {
        public readonly ImmutableArray<string> ErrorMessages;
        public readonly ImmutableArray<FieldValidationError> FieldErrors;

        public ValidationErrors(ImmutableArray<string> errorMessages, ImmutableArray<FieldValidationError> fieldErrors)
        {
            this.ErrorMessages = errorMessages;
            this.FieldErrors = fieldErrors;
        }
        public static ValidationErrors Valid = new ValidationErrors(ImmutableArray<string>.Empty, ImmutableArray<FieldValidationError>.Empty);

        public override string ToString() => this.ToErrorMessage();

        public static ValidationErrors Create(string message) =>
            new ValidationErrors(ImmutableArray.Create(message), ImmutableArray<FieldValidationError>.Empty);
        public static ValidationErrors CreateField(string[] field, string message)
        {
            var error = ValidationErrors.Create(message);
            for (int i = field.Length - 1; i >= 0 ; i--)
                error = error.Nest(field[i]);
            return error;
        }

        public static ValidationErrors Join(params ValidationErrors[] cases)
        {
            var msgCount = 0;
            var fieldCount = 0;
            foreach (var e in cases)
            {
                if (e == null) continue;

                msgCount += e.ErrorMessages.Length;
                fieldCount += e.FieldErrors.Length;
            }

            if (msgCount == 0 && fieldCount == 0) return null;

            var msgBuilder = ImmutableArray.CreateBuilder<string>(msgCount);
            var fieldBuilder = ImmutableArray.CreateBuilder<FieldValidationError>(fieldCount);

            foreach (var e in cases)
            {
                if (e == null) continue;

                msgBuilder.AddRange(e.ErrorMessages);
                fieldBuilder.AddRange(e.FieldErrors);
            }
            return new ValidationErrors(
                errorMessages: msgBuilder.MoveToImmutable(),
                fieldErrors: fieldBuilder.MoveToImmutable()
            );
        }
    }

    public struct FieldValidationError
    {
        public readonly string FieldName;
        public readonly ValidationErrors ValidationErrors;

        public FieldValidationError(string fieldName, ValidationErrors errors)
        {
            this.FieldName = fieldName;
            this.ValidationErrors = errors;
        }
    }
    public class ValidationErrorException : Exception
    {
        public ValidationErrors Validation { get; }
        public ValidationErrorException(ValidationErrors validation, string message) : base(validation.ToErrorMessage(message))
        {
            this.Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        }

        public ValidationErrorException(ValidationErrors validation, string message, Exception innerException) : base(validation.ToErrorMessage(message), innerException)
        {
            this.Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        }
    }
    public static class ValidationErrorsExtensionMethods
    {
        public static bool IsValid(this ValidationErrors errors) => errors == null || (errors.ErrorMessages.Length == 0 && errors.FieldErrors.Length == 0);
        public static IEnumerable<(string[] path, string error)> EnumerateErrors(this ValidationErrors errors, string[] objPath = null)
        {
            if (errors.IsValid()) yield break;
            objPath = objPath ?? Array.Empty<string>();

            foreach (var msg in errors.ErrorMessages)
            {
                if (objPath.Length > 0)
                    yield return (objPath, msg);
                else
                    yield return (objPath, msg);
            }
            foreach (var nested in errors.FieldErrors)
            {
                var path = new string[objPath.Length + 1];
                Array.Copy(objPath, path, objPath.Length);
                path[objPath.Length] = nested.FieldName;
                foreach (var msg in nested.ValidationErrors.EnumerateErrors(path))
                    yield return msg;
            }
        }
        public static IEnumerable<string> ToErrorMessages(this ValidationErrors errors, string objPath = "")
        {
            if (errors.IsValid()) yield break;

            foreach (var msg in errors.ErrorMessages)
            {
                if (objPath.Length > 0)
                    yield return $"at {objPath}: {msg}";
                else
                    yield return msg;
            }
            foreach (var nested in errors.FieldErrors)
            {
                var path = objPath.Length == 0 ? nested.FieldName : objPath + "." + nested.FieldName;
                foreach (var msg in nested.ValidationErrors.ToErrorMessages(path))
                    yield return msg;
            }
        }
        public static string ToErrorMessage(this ValidationErrors errors, string message = "Validation has failed", int softLengthLimit = 1024, string objPath = "")
        {
            if (errors.IsValid()) return objPath.Length == 0 ? "Object is valid" : $"Object {objPath} is valid";

            var result = new System.Text.StringBuilder();
            result.Append(message);
            result.Append(": ");

            var first = true;

            foreach (var msg in errors.ToErrorMessages(objPath))
            {
                if (softLengthLimit < result.Length && !first)
                {
                    result.Append(", ...");
                    break;
                }
                else if (!first)
                    result.Append(", ");
                else first = false;

                result.Append(msg);
            }
            return result.ToString();
        }
        public static void ThrowErrors(this ValidationErrors errors, string message = "Validation has failed")
        {
            if (!errors.IsValid())
                throw new ValidationErrorException(errors, message);
        }

        public static ValidationErrors Nest(this ValidationErrors errors, string field)
        {
            if (errors.IsValid()) return errors;
            return new ValidationErrors(
                errorMessages: ImmutableArray<string>.Empty,
                fieldErrors: ImmutableArray.Create(new FieldValidationError(field, errors))
            );
        }
    }

}
