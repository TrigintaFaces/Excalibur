// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Serialization;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Serialization;

/// <summary>
/// Verifies that Serialization DI registrations wire up
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> correctly.
/// Sprint 564 S564.49: Serialization ValidateOnStart verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SerializationValidateOnStartRegistrationShould
{
	[Fact]
	public void DispatchSerializer_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddDispatchSerializer();

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MessageSerializerOptions>>();
		validators.ShouldNotBeEmpty("AddDispatchSerializer should register IValidateOptions<MessageSerializerOptions>");
	}

	[Fact]
	public void PluggableSerialization_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<PluggableSerializationOptions>>();
		validators.ShouldNotBeEmpty("AddPluggableSerialization should register IValidateOptions<PluggableSerializationOptions>");
	}
}
