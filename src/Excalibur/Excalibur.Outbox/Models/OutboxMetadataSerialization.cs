// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Outbox;

/// <summary>Explicit source-generated metadata configuration for an outbox store.</summary>
/// <remarks>
/// Each constructed store invokes the factory once to create its own bound, frozen options.
/// The factory supplies type metadata and must preserve the supplied canonical settings.
/// Register every actual header value type, including nested object-valued types. No reflection
/// fallback is added. Reused factories and any captured mutable state remain the caller's responsibility.
/// </remarks>
public sealed class OutboxMetadataSerialization
{
	private readonly Func<JsonSerializerOptions, IJsonTypeInfoResolver> _resolverFactory;

	/// <summary>Creates metadata configuration with an options-aware resolver factory.</summary>
	/// <param name="resolverFactory">A factory such as <c>options => new AppHeaderContext(options)</c>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="resolverFactory"/> is null.</exception>
	public OutboxMetadataSerialization(Func<JsonSerializerOptions, IJsonTypeInfoResolver> resolverFactory)
	{
		ArgumentNullException.ThrowIfNull(resolverFactory);
		_resolverFactory = resolverFactory;
	}

	internal OutboxMetadataWriter CreateWriter() => OutboxMetadataWriter.Create(_resolverFactory);
}
