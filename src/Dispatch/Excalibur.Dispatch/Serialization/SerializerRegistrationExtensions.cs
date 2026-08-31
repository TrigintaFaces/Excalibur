// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared registration helper so every <c>AddXSerializer()</c> entry point performs the identical
/// "become the current serializer" ritual — replacing any prior <see cref="ISerializer"/> registration,
/// wiring startup options validation, and setting the registry entry plus current serializer name — so
/// the direct <see cref="ISerializer"/> resolution and the <see cref="PluggableSerializationOptions"/>
/// registry path can never drift apart.
/// </summary>
internal static class SerializerRegistrationExtensions
{
	/// <summary>
	/// Registers <paramref name="serializer"/> as the single current serializer: it becomes the resolved
	/// <see cref="ISerializer"/> (last-registration-wins), is added to the pluggable registry under
	/// <paramref name="serializerId"/>, and is set as the current serializer name — with
	/// <c>ValidateOnStart</c> wired so misconfiguration fails fast at startup.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="serializerId">The framework-assigned serializer id (see <see cref="SerializerIds"/>).</param>
	/// <param name="serializer">The serializer instance to make current.</param>
	/// <param name="name">The current serializer name recorded on <see cref="PluggableSerializationOptions"/>.</param>
	/// <returns>The service collection for method chaining.</returns>
	internal static IServiceCollection SetCurrentSerializer(
		this IServiceCollection services,
		byte serializerId,
		ISerializer serializer,
		string name)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		// Selecting a serializer must actually deliver it. AddPluggableSerialization seats the registry,
		// the IPayloadSerializer facade the inbox and transports resolve, and
		// AddOptions<PluggableSerializationOptions>().ValidateOnStart() — all first-wins, so calling it here
		// is idempotent and never clobbers a consumer's own registrations. Without it an AddXSerializer()
		// call configured a format nothing read: no IPayloadSerializer at all, and a consumer who followed
		// the package README silently stayed on JSON with no error and no log.
		_ = services.AddPluggableSerialization();

		// Single source of truth: direct ISerializer resolution must agree with the
		// PluggableSerializationOptions.CurrentSerializerName / registry path, which is last-registration-wins.
		// TryAdd would be first-wins and silently diverge from CurrentSerializerName when more than one
		// AddXSerializer() is called, so replace any prior registration to make BOTH paths last-wins.
		services.RemoveAll<ISerializer>();
		services.AddSingleton<ISerializer>(serializer);

		services.PostConfigure<PluggableSerializationOptions>(options =>
		{
			options.AddRegistration(registry => registry.Register(serializerId, serializer));
			options.CurrentSerializerName = name;
		});

		return services;
	}
}
