// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Registers the requirement that an application without runtime code generation supply its own event
/// serializer, and makes a composition that does not supply one fail at startup.
/// </summary>
/// <remarks>
/// There is no ahead-of-time default event serializer, and there cannot be one: the source-generated
/// serializer reads and writes through a <c>JsonSerializerContext</c> generated in the consumer's own
/// assembly over the consumer's own event types, which the framework cannot synthesize. The reflection
/// based default is therefore not registered where reflection cannot run, and this type replaces it with
/// a deterministic failure that names the call which fixes it.
/// </remarks>
internal static class AotEventSerializerRequirement
{
	/// <summary>
	/// The message reported when an application without runtime code generation resolves or validates an
	/// event serializer it never registered.
	/// </summary>
	internal const string Message =
		"No event serializer is registered. This application does not support runtime code generation, so the "
		+ "reflection-based default event serializer is not registered -- it cannot run here, and the framework "
		+ "cannot supply a replacement because the source-generated serializer needs a JsonSerializerContext "
		+ "declared over your own event types. Register one at composition: "
		+ "services.AddAotEventSerializer(MyJsonContext.Default).";

	/// <summary>
	/// Registers the failing placeholder and the startup validation that reports it.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	internal static void Register(IServiceCollection services)
	{
		var placeholder = ServiceDescriptor.Singleton<IEventSerializer>(
			static _ => throw new InvalidOperationException(Message));

		// TryAdd, not Add: AddAotEventSerializer replaces the IEventSerializer descriptor and may run
		// either side of this call. Whichever order the consumer writes, their registration survives.
		services.TryAdd(placeholder);

		// The placeholder only throws once something resolves the serializer, which is later than the
		// composition root and later than it needs to be. Validating at startup rejects the composition
		// itself, before a message is handled.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DispatchOptions>>(
			new PlaceholderStillRegistered(services, placeholder)));
		_ = services.AddOptions<DispatchOptions>().ValidateOnStart();
	}

	/// <summary>
	/// Fails validation while the failing placeholder is still the registered event serializer -- that is,
	/// while the consumer has not registered one of their own.
	/// </summary>
	private sealed class PlaceholderStillRegistered(IServiceCollection services, ServiceDescriptor placeholder)
		: IValidateOptions<DispatchOptions>
	{
		public ValidateOptionsResult Validate(string? name, DispatchOptions options) =>
			services.Contains(placeholder) ? ValidateOptionsResult.Fail(Message) : ValidateOptionsResult.Success;
	}
}
