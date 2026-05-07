// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using SharedModels.ValidationClasses;

namespace SharedModels.EntityClasses;

/// <summary>
/// Reusable property-level attributes.
/// </summary>
public class User
{
    /// <summary>Gets or sets the user's name.</summary>
    [Required]
    [IsValidName]
    public string? Name { get; set; }

    /// <summary>Gets or sets the user's email address.</summary>
    [Required]
    [AsyncOnlyEmailDomain("contoso.com")]
    public string? Email { get; set; }

    /// <summary>Gets or sets the simulated I/O delay in milliseconds.</summary>
    [Required]
    public int? Delay { get; set; }
}
