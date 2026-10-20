// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>Preserves the Excalibur hosting entry point for the Dispatch HTTP exception handler.</summary>
public partial class GlobalExceptionHandler : Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics.GlobalExceptionHandler
{
    /// <summary>Initializes the legacy handler with its existing configuration and logger category.</summary>
    /// <param name="env">The hosting environment.</param>
    /// <param name="options">The legacy problem-details options.</param>
    /// <param name="logger">The legacy diagnostic logger.</param>
    public GlobalExceptionHandler(IHostEnvironment env, IOptions<ProblemDetailsOptions> options,
        ILogger<GlobalExceptionHandler> logger) : base(env ?? throw new ArgumentNullException(nameof(env)), options, logger)
    {
    }

    /// <inheritdoc />
    public new ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) =>
        base.TryHandleAsync(httpContext, exception, cancellationToken);

    /// <inheritdoc />
    protected override string FormatDiagnosticStack(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.Demystify().ToString();
    }

    /// <inheritdoc />
    protected override IDictionary<string, string[]>? GetValidationErrors(Exception exception)
    {
        var dispatchErrors = base.GetValidationErrors(exception);
        if (dispatchErrors is not null)
        {
            return dispatchErrors;
        }

        return exception is FluentValidation.ValidationException validation && validation.Errors?.Any() == true
            ? validation.Errors.GroupBy(failure => failure.PropertyName ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray(), StringComparer.Ordinal)
            : null;
    }
}
