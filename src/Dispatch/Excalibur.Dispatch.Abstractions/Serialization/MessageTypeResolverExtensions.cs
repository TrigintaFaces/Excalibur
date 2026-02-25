// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Extension methods for <see cref="IMessageTypeResolver"/>.
/// </summary>
/// <remarks>
/// Provides convenience query methods and async wrappers that delegate to
/// the core synchronous methods on <see cref="IMessageTypeResolver"/>.
/// </remarks>
public static class MessageTypeResolverExtensions
{
	/// <summary>
	/// Determines if a type can be resolved by this resolver.
	/// </summary>
	/// <param name="resolver">The message type resolver.</param>
	/// <param name="typeIdentifier">The type identifier to check.</param>
	/// <returns>True if the type can be resolved; otherwise, false.</returns>
	/// <exception cref="ArgumentNullException">Thrown when resolver is null.</exception>
	public static bool CanResolve(this IMessageTypeResolver resolver, string typeIdentifier)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		return resolver.ResolveType(typeIdentifier) is not null;
	}

	#region Async Wrappers

	/// <summary>
	/// Resolves a .NET type from a type identifier string asynchronously.
	/// </summary>
	/// <param name="resolver">The message type resolver.</param>
	/// <param name="typeIdentifier">The type identifier from the message metadata.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The resolved .NET type, or null if the type cannot be resolved.</returns>
	/// <exception cref="ArgumentNullException">Thrown when resolver is null.</exception>
	public static Task<Type?> ResolveTypeAsync(
		this IMessageTypeResolver resolver,
		string typeIdentifier,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(resolver.ResolveType(typeIdentifier));
	}

	/// <summary>
	/// Gets the type identifier for a given .NET type asynchronously.
	/// </summary>
	/// <param name="resolver">The message type resolver.</param>
	/// <param name="messageType">The .NET type to get an identifier for.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The type identifier string for the given type.</returns>
	/// <exception cref="ArgumentNullException">Thrown when resolver is null.</exception>
	public static Task<string> GetTypeIdentifierAsync(
		this IMessageTypeResolver resolver,
		Type messageType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(resolver.GetTypeIdentifier(messageType));
	}

	/// <summary>
	/// Determines if a type can be resolved by this resolver asynchronously.
	/// </summary>
	/// <param name="resolver">The message type resolver.</param>
	/// <param name="typeIdentifier">The type identifier to check.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the type can be resolved; otherwise, false.</returns>
	/// <exception cref="ArgumentNullException">Thrown when resolver is null.</exception>
	public static Task<bool> CanResolveAsync(
		this IMessageTypeResolver resolver,
		string typeIdentifier,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(resolver.CanResolve(typeIdentifier));
	}

	/// <summary>
	/// Registers a type with the resolver for future resolution asynchronously.
	/// </summary>
	/// <param name="resolver">The message type resolver.</param>
	/// <param name="messageType">The .NET type to register.</param>
	/// <param name="typeIdentifier">The type identifier to associate with the type.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the registration operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when resolver is null.</exception>
	public static Task RegisterTypeAsync(
		this IMessageTypeResolver resolver,
		Type messageType,
		string typeIdentifier,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		cancellationToken.ThrowIfCancellationRequested();
		resolver.RegisterType(messageType, typeIdentifier);
		return Task.CompletedTask;
	}

	#endregion
}
