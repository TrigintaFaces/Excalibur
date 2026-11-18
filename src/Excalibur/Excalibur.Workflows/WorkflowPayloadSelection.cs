// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Workflows;

/// <summary>Immutable explicit registration choice; later service collections cannot mutate an existing provider.</summary>
/// <param name="resolver">The explicitly selected resolver.</param>
internal sealed class WorkflowPayloadSelection(IJsonTypeInfoResolver resolver)
{
    /// <summary>Gets the explicit payload resolver selected at registration.</summary>
    internal IJsonTypeInfoResolver Resolver { get; } = resolver;
}
