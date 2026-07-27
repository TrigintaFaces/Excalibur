// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Cross-provider structural parity guard for the shared serverless cleanup-reserve.
/// </summary>
/// <remarks>
/// The three first-party serverless providers (AWS Lambda, Azure Functions, Google Cloud
/// Functions) must remain in lockstep on the cleanup-reserve substrate: each provider's
/// registration delegates to the shared <c>AddServerlessHosting</c> path (so every provider
/// wires the single <see cref="ServerlessHostOptionsValidator"/>), and the execution-timeout
/// computation draws from one shared <c>DefaultCleanupReserve</c> constant rather than a
/// divergent per-provider value. These locks fail if a future provider is added (or an
/// existing one refactored) in a way that breaks that parity.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessCleanupReserveParityShould : UnitTestBase
{

	private static void RegisterProvider(string provider, IServiceCollection services)
	{
		switch (provider)
		{
			case "AwsLambda": _ = services.AddAwsLambdaHosting(); break;
			case "AzureFunctions": _ = services.AddAzureFunctionsHosting(); break;
			case "GoogleCloudFunctions": _ = services.AddGoogleCloudFunctionsHosting(); break;
			default: throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown serverless provider");
		}
	}

	[Theory]
	[InlineData("AwsLambda")]
	[InlineData("AzureFunctions")]
	[InlineData("GoogleCloudFunctions")]
	public void Register_the_shared_serverless_options_validator(string provider)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		RegisterProvider(provider, services);

		// Assert — every provider must wire the single shared validator (cross-provider parity).
		var validators = services
			.Where(sd => sd.ServiceType == typeof(IValidateOptions<ServerlessHostOptions>))
			.ToList();

		validators.ShouldNotBeEmpty(
			$"{provider} hosting must register the shared ServerlessHostOptions validator");
		validators.ShouldContain(
			sd => sd.ImplementationType == typeof(ServerlessHostOptionsValidator),
			$"{provider} hosting must wire ServerlessHostOptionsValidator (cross-provider parity)");
	}

	[Fact]
	public void Expose_a_single_shared_default_cleanup_reserve_constant()
	{
		// The shared reserve is the single source of truth for every provider's timeout math.
		var field = typeof(ServerlessHostOptions).GetField(
			"DefaultCleanupReserve",
			BindingFlags.NonPublic | BindingFlags.Static);

		field.ShouldNotBeNull("ServerlessHostOptions must expose the shared DefaultCleanupReserve constant");
		field!.FieldType.ShouldBe(typeof(TimeSpan));
		((TimeSpan)field.GetValue(null)!).ShouldBe(TimeSpan.FromMilliseconds(500));
	}
}
