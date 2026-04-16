// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.InMemory;

/// <summary>
/// Internal implementation of the in-memory event sourcing builder.
/// </summary>
internal sealed class InMemoryEventSourcingBuilder : IInMemoryEventSourcingBuilder
{
	/// <summary>
	/// Gets the configured store name.
	/// </summary>
	internal string ConfiguredStoreName { get; private set; } = "inmemory";

	/// <inheritdoc/>
	public IInMemoryEventSourcingBuilder StoreName(string storeName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
		ConfiguredStoreName = storeName;
		return this;
	}
}
