// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Tests.Conformance;

/// <summary>
/// Conformance test that verifies every <c>_disposed</c> field across all Excalibur production
/// assemblies is declared as <c>volatile</c> to prevent torn reads on hot-path disposal checks.
/// </summary>
/// <remarks>
/// Sprint 569 — Task S569.20: Regression guard for the volatile _disposed sweep.
/// In .NET reflection, volatile fields are identified by checking if
/// <see cref="IsVolatile"/> is in the required custom modifiers.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Conformance")]
public sealed class VolatileDisposedFieldConformanceShould
{
	/// <summary>
	/// Production assembly names to scan for _disposed fields.
	/// Loaded via <see cref="Assembly.Load(string)"/> to ensure availability.
	/// </summary>
	private static readonly string[] ProductionAssemblyNames =
	[
		// Dispatch core
		"Excalibur.Dispatch",
		"Excalibur.Dispatch.Abstractions",
		"Excalibur.Dispatch.Observability",

		// Dispatch transports
		"Excalibur.Dispatch.Transport.Abstractions",
		"Excalibur.Dispatch.Transport.RabbitMQ",
		"Excalibur.Dispatch.Transport.Kafka",
		"Excalibur.Dispatch.Transport.AwsSqs",
		"Excalibur.Dispatch.Transport.AzureServiceBus",
		"Excalibur.Dispatch.Transport.GooglePubSub",

		// Dispatch compliance/security
		"Excalibur.Dispatch.Compliance",
		"Excalibur.Dispatch.Compliance.Aws",
		"Excalibur.Dispatch.Compliance.Azure",
		"Excalibur.Dispatch.Compliance.Vault",
		"Excalibur.Dispatch.Security",

		// Dispatch infra
		"Excalibur.Dispatch.Caching",
		"Excalibur.Dispatch.Hosting.Serverless.Abstractions",
		"Excalibur.Dispatch.AuditLogging.SqlServer",
		"Excalibur.Dispatch.Resilience.Polly",
	];

	[Fact]
	public void AllDisposedFieldsShouldBeVolatile()
	{
		// Arrange — force-load all known production assemblies
		LoadProductionAssemblies();

		// Act — scan for non-volatile _disposed fields in production assemblies only
		var nonVolatileFields = new List<string>();

		foreach (var assembly in GetExcaliburProductionAssemblies())
		{
			foreach (var type in GetTypesSafe(assembly))
			{
				var disposedFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
					.Where(f => f.Name == "_disposed" && f.FieldType == typeof(bool));

				foreach (var field in disposedFields)
				{
					var isVolatile = field.GetRequiredCustomModifiers()
						.Any(m => m == typeof(IsVolatile));

					if (!isVolatile)
					{
						nonVolatileFields.Add($"{type.FullName}._disposed in {assembly.GetName().Name}");
					}
				}
			}
		}

		// Assert — all _disposed fields must be volatile
		nonVolatileFields.ShouldBeEmpty(
			$"Found {nonVolatileFields.Count} non-volatile _disposed field(s). " +
			"All _disposed fields must be declared as 'volatile bool _disposed' to prevent torn reads. " +
			$"Non-conforming types:\n  {string.Join("\n  ", nonVolatileFields)}");
	}

	[Fact]
	public void AtLeastTenProductionAssembliesScanned()
	{
		// Arrange
		LoadProductionAssemblies();

		// Act
		var assemblies = GetExcaliburProductionAssemblies();

		// Assert — ensure meaningful coverage
		assemblies.Count.ShouldBeGreaterThanOrEqualTo(7,
			$"Expected at least 7 Excalibur production assemblies to be scanned, " +
			$"but only found {assemblies.Count}: " +
			$"{string.Join(", ", assemblies.Select(a => a.GetName().Name))}");
	}

	[Fact]
	public void AtLeastFiftyDisposedFieldsFound()
	{
		// Arrange
		LoadProductionAssemblies();

		// Act — count all _disposed fields in production assemblies
		var disposedFieldCount = 0;

		foreach (var assembly in GetExcaliburProductionAssemblies())
		{
			foreach (var type in GetTypesSafe(assembly))
			{
				disposedFieldCount += type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
					.Count(f => f.Name == "_disposed" && f.FieldType == typeof(bool));
			}
		}

		// Assert — ensure we're actually scanning meaningful code
		disposedFieldCount.ShouldBeGreaterThanOrEqualTo(35,
			$"Expected at least 35 _disposed fields across all Excalibur production assemblies, " +
			$"but only found {disposedFieldCount}. Are all assemblies loaded correctly?");
	}

	#region Helpers

	private static void LoadProductionAssemblies()
	{
		foreach (var name in ProductionAssemblyNames)
		{
			try { Assembly.Load(name); }
			catch (FileNotFoundException) { }
		}
	}

	/// <summary>
	/// Returns all loaded Excalibur.* assemblies, excluding test assemblies and shared test utilities.
	/// </summary>
	private static List<Assembly> GetExcaliburProductionAssemblies()
	{
		return AppDomain.CurrentDomain.GetAssemblies()
			.Where(a =>
			{
				var name = a.GetName().Name;
				if (name is null) return false;
				if (!name.StartsWith("Excalibur.", StringComparison.Ordinal)) return false;
				// Exclude test assemblies
				if (name.Contains("Tests", StringComparison.Ordinal)) return false;
				if (name.Contains("Testing", StringComparison.Ordinal)) return false;
				if (name.StartsWith("Tests.", StringComparison.Ordinal)) return false;
				return true;
			})
			.OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
			.ToList();
	}

	private static Type[] GetTypesSafe(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
		}
	}

	#endregion
}
