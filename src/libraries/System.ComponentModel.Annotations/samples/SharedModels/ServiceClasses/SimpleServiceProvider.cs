// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace SharedModels.ServiceClasses;

/// <summary>
/// Minimal IServiceProvider for console demos. In real apps, use Microsoft.Extensions.DependencyInjection.
/// </summary>
public class SimpleServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();

    public SimpleServiceProvider Register<T>(T service) where T : notnull
    {
        _services[typeof(T)] = service;
        return this;
    }

    public object? GetService(Type serviceType) =>
        _services.TryGetValue(serviceType, out var service) ? service : null;
}
