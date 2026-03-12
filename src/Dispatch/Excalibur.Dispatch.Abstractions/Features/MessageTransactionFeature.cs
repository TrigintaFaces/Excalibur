// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Default implementation of <see cref="IMessageTransactionFeature"/>.
/// </summary>
public sealed class MessageTransactionFeature : IMessageTransactionFeature
{
	/// <inheritdoc />
	public object? Transaction { get; set; }

	/// <inheritdoc />
	public string? TransactionId { get; set; }
}
