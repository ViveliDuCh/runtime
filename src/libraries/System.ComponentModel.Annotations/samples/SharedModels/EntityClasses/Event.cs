// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.DataAnnotations;
using SharedModels.ValidationClasses;

namespace SharedModels.EntityClasses;

/// <summary>
/// Reusable entity-level async attribute.
/// </summary>
[AsyncDateRangeValid(nameof(StartDate), nameof(EndDate))]
public class Event
{
    /// <summary>Gets or sets the event title.</summary>
    [Required]
    public string? Title { get; set; }

    /// <summary>Gets or sets the event start date.</summary>
    [Required]
    public DateTime? StartDate { get; set; }

    /// <summary>Gets or sets the event end date.</summary>
    [Required]
    public DateTime? EndDate { get; set; }

    /// <summary>Gets or sets the simulated I/O delay in milliseconds.</summary>
    [Required]
    public int? Delay { get; set; }
}
