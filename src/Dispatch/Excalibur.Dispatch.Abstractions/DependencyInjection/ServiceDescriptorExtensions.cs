// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Keyed-safe accessors for the implementation members of a <see cref="ServiceDescriptor"/>.
/// </summary>
/// <remarks>
/// <para>
/// On .NET 8+ a <see cref="ServiceDescriptor"/> is either <em>keyed</em> or <em>non-keyed</em>, and the two
/// families of implementation properties are mutually exclusive: reading the non-keyed
/// <see cref="ServiceDescriptor.ImplementationType"/>, <see cref="ServiceDescriptor.ImplementationInstance"/>,
/// or <see cref="ServiceDescriptor.ImplementationFactory"/> on a keyed descriptor (and vice versa) throws
/// <see cref="System.InvalidOperationException"/>. Any code that enumerates an <see cref="IServiceCollection"/>
/// — for registration sweeps, decoration, diagnostics, or inspection — and reads those properties directly
/// will therefore crash the moment a keyed service is present (and the framework itself registers keyed
/// services).
/// </para>
/// <para>
/// These extensions are the single sanctioned way to read a descriptor's implementation: each transparently
/// returns the keyed member when <see cref="ServiceDescriptor.IsKeyedService"/> is <see langword="true"/> and
/// the non-keyed member otherwise, so callers never have to branch on the keyed/non-keyed distinction. This
/// mirrors the read-only intent of Microsoft's <c>ServiceCollectionDescriptorExtensions</c>.
/// </para>
/// </remarks>
public static class ServiceDescriptorExtensions
{
	/// <summary>
	/// Gets the implementation <see cref="System.Type"/> of the descriptor, transparently handling keyed
	/// descriptors.
	/// </summary>
	/// <param name="descriptor">The service descriptor to read.</param>
	/// <returns>
	/// The implementation type, or <see langword="null"/> when the descriptor was registered with an
	/// instance or a factory rather than a type.
	/// </returns>
	/// <exception cref="System.ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
	[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	public static Type? GetImplementationType(this ServiceDescriptor descriptor)
	{
		ArgumentNullException.ThrowIfNull(descriptor);

		return descriptor.IsKeyedService
			? descriptor.KeyedImplementationType
			: descriptor.ImplementationType;
	}

	/// <summary>
	/// Gets the singleton implementation instance of the descriptor, transparently handling keyed descriptors.
	/// </summary>
	/// <param name="descriptor">The service descriptor to read.</param>
	/// <returns>
	/// The implementation instance, or <see langword="null"/> when the descriptor was registered with a type
	/// or a factory rather than a pre-built instance.
	/// </returns>
	/// <exception cref="System.ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
	public static object? GetImplementationInstance(this ServiceDescriptor descriptor)
	{
		ArgumentNullException.ThrowIfNull(descriptor);

		return descriptor.IsKeyedService
			? descriptor.KeyedImplementationInstance
			: descriptor.ImplementationInstance;
	}

	/// <summary>
	/// Gets the implementation factory of the descriptor as a uniform
	/// <see cref="System.Func{IServiceProvider, Object}"/>, transparently handling keyed descriptors.
	/// </summary>
	/// <param name="descriptor">The service descriptor to read.</param>
	/// <returns>
	/// A factory that produces the implementation from an <see cref="System.IServiceProvider"/>, or
	/// <see langword="null"/> when the descriptor was registered with a type or an instance rather than a
	/// factory. For keyed descriptors the returned delegate forwards the descriptor's
	/// <see cref="ServiceDescriptor.ServiceKey"/> to the underlying keyed factory, so callers can invoke it
	/// with the service provider alone.
	/// </returns>
	/// <exception cref="System.ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
	public static Func<IServiceProvider, object>? GetImplementationFactory(this ServiceDescriptor descriptor)
	{
		ArgumentNullException.ThrowIfNull(descriptor);

		if (!descriptor.IsKeyedService)
		{
			return descriptor.ImplementationFactory;
		}

		var keyedFactory = descriptor.KeyedImplementationFactory;
		if (keyedFactory is null)
		{
			return null;
		}

		var serviceKey = descriptor.ServiceKey;
		return serviceProvider => keyedFactory(serviceProvider, serviceKey);
	}
}
