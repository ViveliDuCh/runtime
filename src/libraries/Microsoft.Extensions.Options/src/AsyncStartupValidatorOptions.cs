// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Options
{
    internal sealed class AsyncStartupValidatorOptions
    {
        // Maps each pair of a) options type and b) options name to an async method that validates it
        public Dictionary<(Type, string), Func<CancellationToken, Task>> _validators { get; } = new Dictionary<(Type, string), Func<CancellationToken, Task>>();
    }
}
