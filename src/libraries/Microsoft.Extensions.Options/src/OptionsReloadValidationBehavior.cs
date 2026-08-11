// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Options
{
    /// <summary>
    /// Specifies how an options monitor responds when validation of a reloaded options instance fails.
    /// </summary>
    public enum OptionsReloadValidationBehavior
    {
        /// <summary>
        /// Continues serving the most recently validated options instance.
        /// </summary>
        KeepLastGood = 0,

        /// <summary>
        /// Causes options monitor reads to throw the reload failure until a subsequent reload succeeds.
        /// </summary>
        FailReads = 1,
    }
}
