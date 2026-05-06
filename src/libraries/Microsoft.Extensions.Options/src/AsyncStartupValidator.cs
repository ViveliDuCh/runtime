// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Options
{
    internal sealed class AsyncStartupValidator : IAsyncStartupValidator
    {
        private readonly AsyncStartupValidatorOptions _validatorOptions;

        public AsyncStartupValidator(IOptions<AsyncStartupValidatorOptions> validators)
        {
            _validatorOptions = validators.Value;
        }

        public async Task ValidateAsync(CancellationToken cancellationToken = default)
        {
            List<Exception>? exceptions = null;

            foreach (Func<CancellationToken, Task> validator in _validatorOptions._validators.Values)
            {
                try
                {
                    await validator(cancellationToken).ConfigureAwait(false);
                }
                catch (OptionsValidationException ex)
                {
                    exceptions ??= new();
                    exceptions.Add(ex);
                }
            }

            if (exceptions is not null)
            {
                if (exceptions.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
                }

                if (exceptions.Count > 1)
                {
                    throw new AggregateException(exceptions);
                }
            }
        }
    }
}
