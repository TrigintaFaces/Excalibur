// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox;

/// <summary>Configures the same metadata-only registration from the outbox builder.</summary>
public static class OutboxMetadataSerializationBuilderExtensions
{
	/// <summary>Selects explicit generated metadata configuration for outbox stores.</summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="serialization">The metadata configuration.</param>
	/// <returns>The builder.</returns>
	/// <exception cref="ArgumentNullException">An argument is null.</exception>
	/// <exception cref="InvalidOperationException">A conflicting metadata registration exists.</exception>
	public static IOutboxBuilder WithMetadataSerialization(
		this IOutboxBuilder builder,
		OutboxMetadataSerialization serialization)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.AddOutboxMetadataSerialization(serialization);
		return builder;
	}
}
