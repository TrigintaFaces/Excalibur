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
	/// <summary>
	/// The three first-party serverless provider options types.
	/// </summary>
	public static readonly Type[] ProviderOptionTypes =
	[
		typeof(AwsLambdaOptions),
		typeof(AzureFunctionsOptions),
		typeof(GoogleCloudFunctionsOptions),
	];

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

	[Fact]
	public void Not_let_any_provider_declare_a_divergent_cleanup_reserve()
	{
		// Parity guard: no provider options type may introduce its own cleanup-reserve member —
		// they must all defer to the shared ServerlessHostOptions.DefaultCleanupReserve.
		foreach (var optionsType in ProviderOptionTypes)
		{
			var divergent = optionsType
				.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				.Where(m => m.Name.Contains("CleanupReserve", StringComparison.Ordinal))
				.Select(m => m.Name)
				.ToList();

			divergent.ShouldBeEmpty(
				$"{optionsType.Name} must not declare its own cleanup-reserve member; "
				+ "the shared ServerlessHostOptions.DefaultCleanupReserve is the single source of truth");
		}
	}

	[Theory]
	[InlineData("AddAwsLambdaHosting", typeof(AwsLambdaOptions))]
	[InlineData("AddAzureFunctionsHosting", typeof(AzureFunctionsOptions))]
	[InlineData("AddGoogleCloudFunctionsHosting", typeof(GoogleCloudFunctionsOptions))]
	public void Expose_a_configure_action_overload_per_provider(string methodName, Type optionsType)
	{
		// Parity of the configuration surface: every provider Add* method offers the same
		// Action<TOptions> configure overload.
		var method = typeof(ServerlessServiceCollectionExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == methodName)
			.ToList();

		method.ShouldNotBeEmpty($"{methodName} must exist on ServerlessServiceCollectionExtensions");

		var configureActionType = typeof(Action<>).MakeGenericType(optionsType);
		method.ShouldContain(
			m => m.GetParameters().Any(p => p.ParameterType == configureActionType),
			$"{methodName} must accept an Action<{optionsType.Name}> configure overload (cross-provider parity)");
	}
}
