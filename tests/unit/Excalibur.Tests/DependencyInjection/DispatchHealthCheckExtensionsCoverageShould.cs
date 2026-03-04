// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class DispatchHealthCheckExtensionsCoverageShould
{
	private static readonly object HealthCheckTargetsSync = new();

	[Fact]
	public void TryInvokeHealthCheckExtension_ReturnFalse_WhenResolvedTypeCannotBeLoaded()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();
		var target = (
			"Excalibur.Missing.Assembly",
			"Excalibur.Missing.Type",
			"MissingMethod");

		// Act
		var invoked = false;
		WithTemporaryHealthCheckTarget(target, () =>
		{
			invoked = InvokeTryInvokeHealthCheckExtension(builder, target.Item3);
		});

		// Assert
		invoked.ShouldBeFalse();
	}

	[Fact]
	public void TryInvokeHealthCheckExtension_ReturnFalse_WhenResolvedTypeHasNoMatchingMethod()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();
		var target = (
			typeof(DispatchHealthCheckExtensionsCoverageShould).Assembly.GetName().Name!,
			typeof(HealthCheckExtensionTestTargets).FullName!,
			"DoesNotExistOnTargetType");

		// Act
		var invoked = false;
		WithTemporaryHealthCheckTarget(target, () =>
		{
			invoked = InvokeTryInvokeHealthCheckExtension(builder, target.Item3);
		});

		// Assert
		invoked.ShouldBeFalse();
	}

	[Fact]
	public void TryInvokeHealthCheckExtension_ReturnFalse_WhenFirstParameterIsNotHealthChecksBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();
		var target = (
			typeof(DispatchHealthCheckExtensionsCoverageShould).Assembly.GetName().Name!,
			typeof(HealthCheckExtensionTestTargets).FullName!,
			nameof(HealthCheckExtensionTestTargets.WrongFirstParameterMethod));

		// Act
		var invoked = false;
		WithTemporaryHealthCheckTarget(target, () =>
		{
			invoked = InvokeTryInvokeHealthCheckExtension(builder, target.Item3);
		});

		// Assert
		invoked.ShouldBeFalse();
	}

	[Fact]
	public void TryInvokeHealthCheckExtension_ReturnTrue_WhenMethodHasOnlyOptionalParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();
		var target = (
			typeof(DispatchHealthCheckExtensionsCoverageShould).Assembly.GetName().Name!,
			typeof(HealthCheckExtensionTestTargets).FullName!,
			nameof(HealthCheckExtensionTestTargets.OptionalParameterMethod));

		// Act
		var invoked = false;
		WithTemporaryHealthCheckTarget(target, () =>
		{
			invoked = InvokeTryInvokeHealthCheckExtension(builder, target.Item3);
		});

		// Assert
		invoked.ShouldBeTrue();
	}

	[Fact]
	public void ResolveType_ReturnNull_WhenTypeDoesNotExistInLoadedAssembly()
	{
		// Arrange
		var assemblyName = typeof(DispatchHealthCheckExtensions).Assembly.GetName().Name!;

		// Act
		var resolved = InvokeResolveType(assemblyName, "Microsoft.Extensions.DependencyInjection.NotARealType");

		// Assert
		resolved.ShouldBeNull();
	}

	private static bool InvokeTryInvokeHealthCheckExtension(IHealthChecksBuilder builder, string methodName)
	{
		var method = typeof(DispatchHealthCheckExtensions).GetMethod(
			"TryInvokeHealthCheckExtension",
			BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		return (bool)method!.Invoke(null, [builder, methodName])!;
	}

	private static Type? InvokeResolveType(string assemblyName, string typeName)
	{
		var method = typeof(DispatchHealthCheckExtensions).GetMethod(
			"ResolveType",
			BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		return (Type?)method!.Invoke(null, [assemblyName, typeName]);
	}

	private static void WithTemporaryHealthCheckTarget(
		(string AssemblyName, string TypeName, string MethodName) target,
		Action action)
	{
		var field = typeof(DispatchHealthCheckExtensions).GetField(
			"HealthCheckExtensionTargets",
			BindingFlags.NonPublic | BindingFlags.Static);
		field.ShouldNotBeNull();

		if (field!.GetValue(null) is not (string AssemblyName, string TypeName, string MethodName)[] targets)
		{
			throw new InvalidOperationException("HealthCheckExtensionTargets field did not return the expected tuple array.");
		}

		lock (HealthCheckTargetsSync)
		{
			var snapshot = ((string AssemblyName, string TypeName, string MethodName)[])targets.Clone();
			try
			{
				for (var i = 0; i < targets.Length; i++)
				{
					targets[i] = (string.Empty, string.Empty, string.Empty);
				}

				targets[0] = target;
				action();
			}
			finally
			{
				Array.Copy(snapshot, targets, snapshot.Length);
			}
		}
	}

	private static class HealthCheckExtensionTestTargets
	{
		public static IHealthChecksBuilder OptionalParameterMethod(IHealthChecksBuilder builder, string optionalValue = "default")
		{
			_ = optionalValue;
			return builder;
		}

		public static void WrongFirstParameterMethod(string invalidFirstParameter, string optionalValue = "default")
		{
			_ = invalidFirstParameter;
			_ = optionalValue;
		}
	}
}
