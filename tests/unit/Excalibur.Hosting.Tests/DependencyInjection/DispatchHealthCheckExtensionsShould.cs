// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Hosting.Options;
using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Hosting.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class DispatchHealthCheckExtensionsShould
{
	private static readonly object HealthCheckTargetsSync = new();

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IHealthChecksBuilder)null!).AddDispatchHealthChecks());
	}

	[Fact]
	public void RegisterAllHealthChecks_WhenAllServicesPresent()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IOutboxPublisher>());
		services.AddSingleton(A.Fake<IInboxStore>());
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		services.AddSingleton(A.Fake<ILeaderElection>());
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks();

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldContain(r => r.Name == "outbox");
		registrations.ShouldContain(r => r.Name == "inbox");
		registrations.ShouldContain(r => r.Name == "sagas");
		registrations.ShouldContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void RegisterOnlyOutbox_WhenOnlyOutboxServiceRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IOutboxPublisher>());
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks();

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldContain(r => r.Name == "outbox");
		registrations.ShouldNotContain(r => r.Name == "inbox");
		registrations.ShouldNotContain(r => r.Name == "sagas");
		registrations.ShouldNotContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void RegisterNoHealthChecks_WhenNoServicesRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks();

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldNotContain(r => r.Name == "outbox");
		registrations.ShouldNotContain(r => r.Name == "inbox");
		registrations.ShouldNotContain(r => r.Name == "sagas");
		registrations.ShouldNotContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void RespectOptionsFlags_WhenSpecificChecksDisabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IOutboxPublisher>());
		services.AddSingleton(A.Fake<IInboxStore>());
		services.AddSingleton(A.Fake<ISagaMonitoringService>());
		services.AddSingleton(A.Fake<ILeaderElection>());
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		builder.AddDispatchHealthChecks(options =>
		{
			options.IncludeOutbox = false;
			options.IncludeLeaderElection = false;
		});

		// Assert
		var registrations = GetHealthCheckRegistrations(services);
		registrations.ShouldNotContain(r => r.Name == "outbox");
		registrations.ShouldContain(r => r.Name == "inbox");
		registrations.ShouldContain(r => r.Name == "sagas");
		registrations.ShouldNotContain(r => r.Name == "leader-election");
	}

	[Fact]
	public void ReturnBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddDispatchHealthChecks();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DefaultOptionsIncludeAllChecks()
	{
		// Arrange & Act
		var options = new DispatchHealthCheckOptions();

		// Assert
		options.IncludeOutbox.ShouldBeTrue();
		options.IncludeInbox.ShouldBeTrue();
		options.IncludeSaga.ShouldBeTrue();
		options.IncludeLeaderElection.ShouldBeTrue();
	}

	[Fact]
	public void ResolveType_ReturnNull_WhenAssemblyDoesNotExist()
	{
		// Act
		var resolved = InvokeResolveType("Excalibur.Missing.Assembly", "Missing.Type");

		// Assert
		resolved.ShouldBeNull();
	}

	[Fact]
	public void ResolveType_ReturnType_WhenAssemblyAndTypeAreLoaded()
	{
		// Arrange
		var assemblyName = typeof(DispatchHealthCheckExtensions).Assembly.GetName().Name!;
		var typeName = typeof(DispatchHealthCheckExtensions).FullName!;

		// Act
		var resolved = InvokeResolveType(assemblyName, typeName);

		// Assert
		resolved.ShouldBe(typeof(DispatchHealthCheckExtensions));
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

	[Fact]
	public void ResolveType_ReturnType_WhenAssemblyLoadsOnDemand()
	{
		// Arrange
		var candidate = FindOnDemandLoadCandidate();
		if (candidate is null)
		{
			// Fallback keeps this deterministic across runtime variations.
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
		var assembly = typeof(DispatchHealthCheckExtensions).Assembly.GetName();
		var displayName = assembly.FullName!;
		displayName.ShouldNotBe(assembly.Name);

		// Act
		var resolved = InvokeResolveType(displayName, typeof(DispatchHealthCheckExtensions).FullName!);

		// Assert
		resolved.ShouldBe(typeof(DispatchHealthCheckExtensions));
	}

	[Fact]
	public void TryInvokeHealthCheckExtension_ReturnFalse_WhenMethodNameIsUnknown()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();

		// Act
		var invoked = InvokeTryInvokeHealthCheckExtension(builder, "UnknownHealthCheckMethod");

		// Assert
		invoked.ShouldBeFalse();
	}

	[Fact]
	public void TryInvokeHealthCheckExtension_ReturnTrue_ForKnownMethod()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();

		// Act
		var invoked = InvokeTryInvokeHealthCheckExtension(builder, "AddOutboxHealthCheck");

		// Assert
		invoked.ShouldBeTrue();
	}

	[Fact]
	public void TryInvokeHealthCheckExtension_ReturnFalse_WhenMethodHasNoParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();
		var target = (
			typeof(DispatchHealthCheckExtensionsShould).Assembly.GetName().Name!,
			typeof(HealthCheckExtensionTestTargets).FullName!,
			nameof(HealthCheckExtensionTestTargets.NoParameterMethod));

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
	public void TryInvokeHealthCheckExtension_ReturnFalse_WhenMethodHasRequiredParameters()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();
		var target = (
			typeof(DispatchHealthCheckExtensionsShould).Assembly.GetName().Name!,
			typeof(HealthCheckExtensionTestTargets).FullName!,
			nameof(HealthCheckExtensionTestTargets.RequiredParameterMethod));

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
			typeof(DispatchHealthCheckExtensionsShould).Assembly.GetName().Name!,
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
			typeof(DispatchHealthCheckExtensionsShould).Assembly.GetName().Name!,
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
			typeof(DispatchHealthCheckExtensionsShould).Assembly.GetName().Name!,
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

	private static IReadOnlyList<HealthCheckRegistration> GetHealthCheckRegistrations(IServiceCollection services)
	{
		var registrations = new List<HealthCheckRegistration>();

		foreach (var descriptor in services)
		{
			if (descriptor.ServiceType == typeof(HealthCheckRegistration) &&
				descriptor.ImplementationInstance is HealthCheckRegistration registration)
			{
				registrations.Add(registration);
			}
		}

		// Also check via IConfigureOptions<HealthCheckServiceOptions> pattern
		using var sp = services.BuildServiceProvider();
		var options = sp.GetService<IOptions<HealthCheckServiceOptions>>();
		if (options?.Value.Registrations is { } regs)
		{
			foreach (var reg in regs)
			{
				if (!registrations.Any(r => r.Name == reg.Name))
				{
					registrations.Add(reg);
				}
			}
		}

		return registrations;
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
		public static void NoParameterMethod()
		{
		}

		public static void RequiredParameterMethod(IHealthChecksBuilder builder, string requiredValue)
		{
			_ = builder;
			_ = requiredValue;
		}

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
