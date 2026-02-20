// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Concurrency;

/// <inheritdoc />
public sealed class ETag : IETag
{
	/// <inheritdoc />
	public string? IncomingValue { get; set; } = string.Empty;

	/// <inheritdoc />
	public string? OutgoingValue { get; set; } = string.Empty;
}
