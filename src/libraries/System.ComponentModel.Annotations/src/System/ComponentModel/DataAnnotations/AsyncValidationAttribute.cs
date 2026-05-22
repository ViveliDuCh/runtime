// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    ///     Base class for validation attributes that require asynchronous operations, such as database lookups or API calls.
    /// </summary>
    public abstract class AsyncValidationAttribute : ValidationAttribute
    {
        /// <summary>
        ///     Default constructor for any async validation attribute.
        /// </summary>
        protected AsyncValidationAttribute()
        {
        }

        /// <summary>
        ///     Constructor that accepts a fixed validation error message.
        /// </summary>
        /// <param name="errorMessage">A non-localized error message to use in <see cref="ValidationAttribute.ErrorMessageString" />.</param>
        protected AsyncValidationAttribute(string errorMessage)
            : base(errorMessage)
        {
        }

        /// <summary>
        ///     Allows for providing a resource accessor function that will be used by the <see cref="ValidationAttribute.ErrorMessageString" />
        ///     property to retrieve the error message.
        /// </summary>
        /// <param name="errorMessageAccessor">The <see cref="Func{T}" /> that will return an error message.</param>
        protected AsyncValidationAttribute(Func<string> errorMessageAccessor)
            : base(errorMessageAccessor)
        {
        }

        /// <summary>
        ///     Override of the base class <see cref="ValidationAttribute.IsValid(object?, ValidationContext)" /> method.
        ///     By default, throws <see cref="InvalidOperationException" /> to indicate that this attribute requires
        ///     asynchronous validation. Subclasses may override to provide a synchronous fallback.
        /// </summary>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            throw new InvalidOperationException(
                SR.Format(SR.AsyncValidationAttribute_RequiresAsync, GetType().Name));
        }

        /// <summary>
        ///     Override this method in subclasses to implement asynchronous validation logic.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">
        ///     A <see cref="ValidationContext" /> instance that provides context about the validation operation,
        ///     such as the object and member being validated.
        /// </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns>
        ///     A <see cref="ValueTask{TResult}" /> representing the asynchronous validation operation.
        ///     When validation is valid, <see cref="ValidationResult.Success" />.
        ///     When validation is invalid, an instance of <see cref="ValidationResult" />.
        /// </returns>
        /// <remarks>
        ///     <see cref="ValueTask{TResult}" /> is used instead of <see cref="System.Threading.Tasks.Task{TResult}" />
        ///     because this method is a leaf API called once per attribute per value within the validation
        ///     pipeline. Validators that complete synchronously (e.g., cached lookups) benefit from the
        ///     zero-allocation path. Callers that need to compose results via <c>Task.WhenAll</c>
        ///     should use <see cref="ValueTask{TResult}.AsTask" />.
        /// </remarks>
        protected abstract ValueTask<ValidationResult?> IsValidAsync(
            object? value,
            ValidationContext validationContext,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Tests whether the given <paramref name="value" /> is valid asynchronously with respect to the current
        ///     validation attribute without throwing a <see cref="ValidationException" />.
        /// </summary>
        /// <remarks>
        ///     Returns <see cref="ValueTask{TResult}" /> for consistency with <see cref="IsValidAsync" />.
        ///     This is a leaf API consumed via a single <c>await</c> within the <see cref="Validator" />
        ///     pipeline. Orchestration layers that aggregate results across attributes use
        ///     <see cref="ValueTask{TResult}.AsTask" /> internally.
        /// </remarks>
        public ValueTask<ValidationResult?> GetValidationResultAsync(
            object? value,
            ValidationContext validationContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(validationContext);

            ValueTask<ValidationResult?> task = IsValidAsync(value, validationContext, cancellationToken);

            if (task.IsCompletedSuccessfully)
            {
                return new ValueTask<ValidationResult?>(EnsureValidationResultErrorMessage(task.Result, validationContext));
            }

            return CompleteAsync(task, validationContext);

            async ValueTask<ValidationResult?> CompleteAsync(
                ValueTask<ValidationResult?> innerTask, ValidationContext ctx)
            {
                ValidationResult? result = await innerTask.ConfigureAwait(false);

                return EnsureValidationResultErrorMessage(result, ctx);
            }
        }

        private ValidationResult? EnsureValidationResultErrorMessage(
            ValidationResult? result,
            ValidationContext validationContext)
        {
            if (result is not null && string.IsNullOrEmpty(result.ErrorMessage))
            {
                string errorMessage = FormatErrorMessage(validationContext.DisplayName);

                return new ValidationResult(errorMessage, result.MemberNames);
            }

            return result;
        }
    }
}
