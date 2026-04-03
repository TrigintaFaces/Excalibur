// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.OpenSearch;

/// <summary>
/// Validates <see cref="OpenSearchAuditSinkOptions"/> cross-property constraints.
/// </summary>
internal sealed class OpenSearchAuditSinkOptionsValidator : IValidateOptions<OpenSearchAuditSinkOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenSearchAuditSinkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        // At least one of NodeUrls or OpenSearchUrl must be provided
        var hasNodeUrls = options.NodeUrls is { Count: > 0 };
        var hasSingleUrl = !string.IsNullOrWhiteSpace(options.OpenSearchUrl);

        if (!hasNodeUrls && !hasSingleUrl)
        {
            failures.Add("Either NodeUrls (for cluster) or OpenSearchUrl (for single node) must be provided.");
        }

        // Validate all node URLs are valid URIs
        if (options.NodeUrls is { Count: > 0 })
        {
            for (var i = 0; i < options.NodeUrls.Count; i++)
            {
                var url = options.NodeUrls[i];
                if (string.IsNullOrWhiteSpace(url))
                {
                    failures.Add($"NodeUrls[{i}] is empty or whitespace.");
                }
                else if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    failures.Add($"NodeUrls[{i}] '{url}' is not a valid absolute URI.");
                }
            }
        }

        if (hasSingleUrl && !Uri.TryCreate(options.OpenSearchUrl, UriKind.Absolute, out _))
        {
            failures.Add($"OpenSearchUrl '{options.OpenSearchUrl}' is not a valid absolute URI.");
        }

        if (options.MaxRetryAttempts < 0)
        {
            failures.Add("MaxRetryAttempts must be >= 0.");
        }

        if (options.RetryBaseDelay < TimeSpan.Zero)
        {
            failures.Add("RetryBaseDelay must be >= 0.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add("Timeout must be > 0.");
        }

        if (string.IsNullOrWhiteSpace(options.IndexPrefix))
        {
            failures.Add("IndexPrefix is required.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
