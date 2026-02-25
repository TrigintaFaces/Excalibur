// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery.Registry;

namespace Excalibur.Dispatch.TypeResolution;

/// <summary>
/// Type resolver implementation that uses the MessageTypeRegistry.
/// </summary>
public sealed class MessageTypeResolver : ITypeResolver
{
	/// <summary>
	/// Initializes static members of the <see cref="MessageTypeResolver"/> class.
	/// Static constructor to register this resolver.
	/// </summary>
	static MessageTypeResolver() =>

		// Automatically register this resolver when the type is loaded
		TypeResolverRegistry.Register(Instance);

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageTypeResolver"/> class.
	/// Private constructor to enforce singleton pattern.
	/// </summary>
	private MessageTypeResolver()
	{
	}

	/// <summary>
	/// Gets the singleton instance of the MessageTypeResolver.
	/// </summary>
	/// <value>
	/// The singleton instance of the MessageTypeResolver.
	/// </value>
	public static MessageTypeResolver Instance { get; } = new();

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegating to MessageTypeRegistry which handles AOT")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegating to MessageTypeRegistry which handles trimming")]
	public bool TryGetType(string typeName, [NotNullWhen(true)] out Type? type) => MessageTypeRegistry.TryGetType(typeName, out type);
}
