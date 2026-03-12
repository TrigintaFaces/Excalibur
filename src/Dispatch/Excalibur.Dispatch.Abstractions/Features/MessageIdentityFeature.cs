// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Default implementation of <see cref="IMessageIdentityFeature"/>.
/// </summary>
public sealed class MessageIdentityFeature : IMessageIdentityFeature
{
	/// <inheritdoc />
	public string? UserId { get; set; }

	/// <inheritdoc />
	public string? TenantId { get; set; }

	/// <inheritdoc />
	public string? SessionId { get; set; }

	/// <inheritdoc />
	public string? WorkflowId { get; set; }

	/// <inheritdoc />
	public string? ExternalId { get; set; }

	/// <inheritdoc />
	public string? TraceParent { get; set; }
}
