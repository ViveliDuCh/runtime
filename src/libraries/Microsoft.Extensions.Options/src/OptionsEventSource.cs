// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Tracing;

namespace Microsoft.Extensions.Options
{
#pragma warning disable ESGEN001 // EventSource class is not partial. It's blocked by https://github.com/dotnet/runtime/issues/121205
    [EventSource(Name = "Microsoft-Extensions-Options")]
    internal sealed class OptionsEventSource : EventSource
#pragma warning restore ESGEN001
    {
        internal static readonly OptionsEventSource Log = new OptionsEventSource();

        private OptionsEventSource()
        {
        }

        [Event(1, Level = EventLevel.Warning)]
        internal void ReloadValidationFailed(string optionsType, string optionsName, string exceptionType, int behavior) =>
            WriteEvent(1, optionsType, optionsName, exceptionType, behavior);

        [Event(2, Level = EventLevel.Error)]
        internal void ReloadErrorCallbackFailed(string optionsType, string optionsName, string exceptionType) =>
            WriteEvent(2, optionsType, optionsName, exceptionType);

        [Event(3, Level = EventLevel.Error)]
        internal void ChangeListenerFailed(string optionsType, string optionsName, string exceptionType) =>
            WriteEvent(3, optionsType, optionsName, exceptionType);

        [Event(4, Level = EventLevel.Error)]
        internal void ReloadWorkerFailed(string optionsType, string optionsName, string exceptionType) =>
            WriteEvent(4, optionsType, optionsName, exceptionType);
    }
}
