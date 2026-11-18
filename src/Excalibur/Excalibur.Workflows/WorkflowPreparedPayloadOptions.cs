// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

namespace Excalibur.Workflows;

/// <summary>Provider-local, once-composed payload options shared by workflow executor scopes.</summary>
internal sealed class WorkflowPreparedPayloadOptions
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowPreparedPayloadOptions"/> class.</summary>
    /// <param name="options">The already-decided read-only options.</param>
    internal WorkflowPreparedPayloadOptions(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsReadOnly || options.TypeInfoResolver is null)
        {
            throw new ArgumentException("Workflow payload options must be read-only and have an explicit metadata resolver.", nameof(options));
        }

        Options = options;
    }

    /// <summary>Gets the frozen options for this service provider.</summary>
    internal JsonSerializerOptions Options { get; }
}
