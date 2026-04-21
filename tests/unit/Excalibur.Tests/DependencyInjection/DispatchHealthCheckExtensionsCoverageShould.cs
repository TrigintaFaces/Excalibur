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
		var assemblyName = typeof(DispatchHealthChecksBuilderExtensions).Assembly.GetName().Name!;

		// Act
		var resolved = InvokeResolveType(assemblyName, "Microsoft.Extensions.DependencyInjection.NotARealType");

		// Assert
		resolved.ShouldBeNull();
	}

	[Fact]
	public void ResolveType_ReturnType_WhenAssemblyLoadsOnDemand()
	{
		// Arrange
		var candidate = FindOnDemandLoadCandidate();
		if (candidate is null)
		{
			var fallbackAssembly = typeof(string).Assembly.GetName().Name!;
			var fallbackType = typeof(string).FullName!;
			var fallbackResolved = InvokeResolveType(fallbackAssembly, fallbackType);
			fallbackResolved.ShouldBe(typeof(string));
			return;
		}

		// Act
		var resolved = InvokeResolveType(candidate.Value.AssemblyName, candidate.Value.TypeName);

		// Assert
		resolved.ShouldNotBeNull();
		resolved!.FullName.ShouldBe(candidate.Value.TypeName);
	}

	[Fact]
	public void ResolveType_ReturnType_WhenAssemblyDisplayNameForcesLoadPath()
	{
		// Arrange
		var assembly = typeof(DispatchHealthChecksBuilderExtensions).Assembly.GetName();
		var displayName = assembly.FullName!;
		displayName.ShouldNotBe(assembly.Name);

		// Act
		var resolved = InvokeResolveType(displayName, typeof(DispatchHealthChecksBuilderExtensions).FullName!);

		// Assert
		resolved.ShouldBe(typeof(DispatchHealthChecksBuilderExtensions));
	}

	private static bool InvokeTryInvokeHealthCheckExtension(IHealthChecksBuilder builder, string methodName)
	{
		var method = typeof(DispatchHealthChecksBuilderExtensions).GetMethod(
			"TryInvokeHealthCheckExtension",
			BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		return (bool)method!.Invoke(null, [builder, methodName])!;
	}

	private static Type? InvokeResolveType(string assemblyName, string typeName)
	{
		var method = typeof(DispatchHealthChecksBuilderExtensions).GetMethod(
			"ResolveType",
			BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		return (Type?)method!.Invoke(null, [assemblyName, typeName]);
	}

	private static void WithTemporaryHealthCheckTarget(
		(string AssemblyName, string TypeName, string MethodName) target,
		Action action)
	{
		var field = typeof(DispatchHealthChecksBuilderExtensions).GetField(
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

	private static (string AssemblyName, string TypeName)? FindOnDemandLoadCandidate()
	{
		var loadedAssemblyNames = AppDomain.CurrentDomain.GetAssemblies()
			.Select(static assembly => assembly.GetName().Name)
			.Where(static name => !string.IsNullOrWhiteSpace(name))
			.ToHashSet(StringComparer.Ordinal);

		var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
		if (string.IsNullOrWhiteSpace(runtimeDirectory))
		{
			return null;
		}

		var candidates = new (string AssemblyName, string TypeName)[]
		{
			("System.ComponentModel.TypeConverter", "System.ComponentModel.TypeConverter"),
			("System.Runtime.Numerics", "System.Numerics.BigInteger"),
			("System.Text.Encodings.Web", "System.Text.Encodings.Web.TextEncoder"),
			("System.IO.Pipes", "System.IO.Pipes.PipeStream"),
		};

		for (var i = 0; i < candidates.Length; i++)
		{
			var candidate = candidates[i];
			if (loadedAssemblyNames.Contains(candidate.AssemblyName))
			{
				continue;
			}

			var candidatePath = Path.Combine(runtimeDirectory, $"{candidate.AssemblyName}.dll");
			if (!File.Exists(candidatePath))
			{
				continue;
			}

			return candidate;
		}

		return null;
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
