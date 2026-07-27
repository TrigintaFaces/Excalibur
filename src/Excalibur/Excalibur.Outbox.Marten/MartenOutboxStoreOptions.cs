// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.Marten;

/// <summary>
/// Configuration options for the Marten-based outbox store.
/// </summary>
/// <remarks>
/// The Marten outbox composes an <c>IDocumentSession</c> per operation from the consumer-supplied
/// <c>IDocumentStore</c>. Connection, schema, and serialization are configured on the Marten store
/// itself (via <c>services.AddMarten(...)</c>); these options cover only outbox-specific behavior.
/// </remarks>
public sealed class MartenOutboxStoreOptions
{
	/// <summary>
	/// Gets or sets the default retention period after which sent messages are eligible for cleanup.
	/// </summary>
	/// <value>The retention period. Must be positive. Defaults to 7 days.</value>
	public TimeSpan DefaultRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets the maximum number of messages returned by a single cleanup pass.
	/// </summary>
	/// <value>The cleanup batch size. Must be greater than zero. Defaults to 500.</value>
	[Range(1, int.MaxValue)]
	public int CleanupBatchSize { get; set; } = 500;
}
