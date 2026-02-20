// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Source generator that creates AOT-compatible service registration methods.
/// Discovers types marked with [AutoRegister] at compile time and generates explicit registration code.
/// </summary>
/// <remarks>
/// <para>
/// This generator scans for types with the <c>[AutoRegister]</c> attribute and generates
/// a <c>GeneratedServiceCollectionExtensions.AddGeneratedServices()</c> extension method
/// that registers all discovered services with the DI container.
/// </para>
/// <para>
/// <strong>Opt-In Design:</strong>
/// Only types explicitly marked with <c>[AutoRegister]</c> are included. This prevents
/// conflicts with manual registrations and gives consumers full control over which
/// types are auto-registered.
/// </para>
/// </remarks>
[Generator]
public sealed class ServiceRegistrationSourceGenerator : IIncrementalGenerator
{
	private const string AutoRegisterAttributeFullName = "Excalibur.Dispatch.Abstractions.AutoRegisterAttribute";

	/// <summary>
	/// Initializes the service registration source generator with the given context.
	/// Sets up syntax providers to find types with [AutoRegister] and registers
	/// source output generation for compile-time service registration.
	/// </summary>
	/// <param name="context">The generator initialization context providing access to syntax providers and source output registration.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Use ForAttributeWithMetadataName for efficient attribute-based scanning
		// This only processes types that have the [AutoRegister] attribute
		var autoRegisteredTypes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				AutoRegisterAttributeFullName,
				predicate: static (node, _) => node is ClassDeclarationSyntax classDecl &&
											   !classDecl.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword) &&
											   !classDecl.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword),
				transform: static (ctx, cancellationToken) => GetServiceRegistrationInfo(ctx, cancellationToken))
			.Where(static info => info is not null);

		// Collect all registration info and generate the extension method
		context.RegisterSourceOutput(
			autoRegisteredTypes.Collect(),
			static (ctx, registrations) => GenerateServiceRegistrationExtension(ctx, registrations));
	}

	private static ServiceRegistrationInfo? GetServiceRegistrationInfo(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Discard to satisfy IDE0060

		if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
		{
			return null;
		}

		// Skip abstract, static, or non-class types
		if (classSymbol.IsAbstract || classSymbol.IsStatic || classSymbol.TypeKind != TypeKind.Class)
		{
			return null;
		}

		// Extract attribute values
		var attributeData = context.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AutoRegisterAttributeFullName);

		if (attributeData is null)
		{
			return null;
		}

		// Parse attribute properties
		var lifetime = ServiceLifetime.Scoped; // Default
		var asSelf = true;
		var asInterfaces = true;

		foreach (var namedArg in attributeData.NamedArguments)
		{
			switch (namedArg.Key)
			{
				case "Lifetime":
					if (namedArg.Value.Value is int lifetimeValue)
					{
						lifetime = (ServiceLifetime)lifetimeValue;
					}

					break;
				case "AsSelf":
					if (namedArg.Value.Value is bool asSelfValue)
					{
						asSelf = asSelfValue;
					}

					break;
				case "AsInterfaces":
					if (namedArg.Value.Value is bool asInterfacesValue)
					{
						asInterfaces = asInterfacesValue;
					}

					break;
			}
		}

		// Collect interfaces (excluding System namespace interfaces like IDisposable)
		var interfaces = new List<INamedTypeSymbol>();
		if (asInterfaces)
		{
			// Use AllInterfaces to include handler interfaces from base types
			foreach (var iface in classSymbol.AllInterfaces)
			{
				// Skip system interfaces
				var ns = iface.ContainingNamespace?.ToDisplayString() ?? "";
				if (ns.StartsWith("System", StringComparison.Ordinal) ||
					ns.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) ||
					ns.StartsWith("Microsoft.Extensions.Hosting", StringComparison.Ordinal))
				{
					continue;
				}

				interfaces.Add(iface);
			}
		}

		return new ServiceRegistrationInfo
		{
			ClassSymbol = classSymbol,
			Lifetime = lifetime,
			AsSelf = asSelf,
			AsInterfaces = asInterfaces,
			Interfaces = interfaces
		};
	}

	private static void GenerateServiceRegistrationExtension(
		SourceProductionContext context,
		ImmutableArray<ServiceRegistrationInfo?> registrations)
	{
		var validRegistrations = registrations.Where(r => r is not null).Cast<ServiceRegistrationInfo>().ToList();

		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine("// This file was generated by ServiceRegistrationSourceGenerator.");
		_ = sb.AppendLine("// Changes to this file may be lost when the code is regenerated.");
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine();
		_ = sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
		_ = sb.AppendLine();

		// Use Microsoft.Extensions.DependencyInjection namespace per ADR-075 conventions
		_ = sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection;");
		_ = sb.AppendLine();

		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Extension methods for registering services discovered at compile time via [AutoRegister] attribute.");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("/// <remarks>");
		_ = sb.AppendLine("/// <para>");
		_ = sb.AppendLine("/// This class is auto-generated by the <c>ServiceRegistrationSourceGenerator</c>.");
		_ = sb.AppendLine($"/// It discovered {validRegistrations.Count} type(s) marked with [AutoRegister].");
		_ = sb.AppendLine("/// </para>");
		_ = sb.AppendLine("/// <para>");
		_ = sb.AppendLine("/// <strong>AOT Benefits:</strong>");
		_ = sb.AppendLine("/// <list type=\"bullet\">");
		_ = sb.AppendLine("/// <item><description>No runtime reflection - all service discovery at compile time</description></item>");
		_ = sb.AppendLine("/// <item><description>Faster startup - no assembly scanning required</description></item>");
		_ = sb.AppendLine("/// <item><description>Native AOT support - compatible with <c>PublishAot=true</c></description></item>");
		_ = sb.AppendLine("/// <item><description>Trimming safe - no types unexpectedly removed by IL trimmer</description></item>");
		_ = sb.AppendLine("/// </list>");
		_ = sb.AppendLine("/// </para>");
		_ = sb.AppendLine("/// </remarks>");
		_ = sb.AppendLine("[ExcludeFromCodeCoverage]");
		_ = sb.AppendLine(
			"[UnconditionalSuppressMessage(\"Trimming\", \"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming\",");
		_ = sb.AppendLine("    Justification = \"All service types are preserved by this source generator\")]");
		_ = sb.AppendLine("public static class GeneratedServiceCollectionExtensions");
		_ = sb.AppendLine("{");

		// Generate the AddGeneratedServices method
		_ = sb.AppendLine("    /// <summary>");
		_ = sb.AppendLine("    /// Registers all services discovered at compile time via [AutoRegister] attribute.");
		_ = sb.AppendLine("    /// </summary>");
		_ = sb.AppendLine("    /// <param name=\"services\">The service collection to add the services to.</param>");
		_ = sb.AppendLine("    /// <returns>The service collection for chaining.</returns>");
		_ = sb.AppendLine("    /// <remarks>");
		_ = sb.AppendLine($"    /// This method registers {validRegistrations.Count} service(s) discovered at compile time.");
		_ = sb.AppendLine("    /// Services can coexist with manual registrations without conflict.");
		_ = sb.AppendLine("    /// </remarks>");
		_ = sb.AppendLine("    public static IServiceCollection AddGeneratedServices(this IServiceCollection services)");
		_ = sb.AppendLine("    {");

		if (validRegistrations.Count == 0)
		{
			_ = sb.AppendLine("        // No types with [AutoRegister] were discovered at compile time.");
			_ = sb.AppendLine("        // Add [AutoRegister] to classes you want to auto-register.");
		}
		else
		{
			foreach (var registration in validRegistrations.Distinct(ServiceRegistrationComparer.Instance))
			{
				var classFullName = registration.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var lifetime = GetLifetimeMethodName(registration.Lifetime);

				// Register as self if requested
				if (registration.AsSelf)
				{
					_ = sb.AppendLine($"        services.Add{lifetime}<{classFullName}>();");
				}

				// Register for each interface
				foreach (var iface in registration.Interfaces)
				{
					var ifaceFullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
					_ = sb.AppendLine($"        services.Add{lifetime}<{ifaceFullName}, {classFullName}>();");
				}
			}
		}

		_ = sb.AppendLine();
		_ = sb.AppendLine("        return services;");
		_ = sb.AppendLine("    }");
		_ = sb.AppendLine();

		// Generate a method to get the count of registered services
		_ = sb.AppendLine("    /// <summary>");
		_ = sb.AppendLine("    /// Gets the count of services discovered at compile time via [AutoRegister] attribute.");
		_ = sb.AppendLine("    /// </summary>");
		_ = sb.AppendLine($"    public static int GeneratedServiceCount => {validRegistrations.Count};");
		_ = sb.AppendLine("}");

		context.AddSource("GeneratedServiceCollectionExtensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));

		// Report diagnostic about service discovery
		if (validRegistrations.Count > 0)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"SRG001",
					"Service Registration Generation Complete",
					$"Discovered {validRegistrations.Count} type(s) with [AutoRegister] attribute for service registration",
					"Excalibur.Dispatch.SourceGenerators",
					DiagnosticSeverity.Info,
					isEnabledByDefault: true),
				Location.None));
		}

		// Report SRG002 for [AutoRegister(AsInterfaces=true)] types that have no discoverable interfaces
		foreach (var registration in validRegistrations)
		{
			if (registration.AsInterfaces && registration.Interfaces.Count == 0)
			{
				var location = registration.ClassSymbol.Locations.FirstOrDefault() ?? Location.None;
				context.ReportDiagnostic(Diagnostic.Create(
					new DiagnosticDescriptor(
						"SRG002",
						"No interfaces found for AutoRegister type",
						"Type '{0}' is marked with [AutoRegister(AsInterfaces=true)] but no registerable interfaces were discovered. Consider setting AsInterfaces=false or implementing an interface.",
						"Excalibur.Dispatch.SourceGenerators",
						DiagnosticSeverity.Warning,
						isEnabledByDefault: true),
					location,
					registration.ClassSymbol.Name));
			}
		}
	}

	private static string GetLifetimeMethodName(ServiceLifetime lifetime)
	{
		return lifetime switch
		{
			ServiceLifetime.Singleton => "Singleton",
			ServiceLifetime.Scoped => "Scoped",
			ServiceLifetime.Transient => "Transient",
			_ => "Scoped"
		};
	}

	private sealed class ServiceRegistrationInfo
	{
		public INamedTypeSymbol ClassSymbol { get; set; } = null!;
		public ServiceLifetime Lifetime { get; set; }
		public bool AsSelf { get; set; }
		public bool AsInterfaces { get; set; }
		public List<INamedTypeSymbol> Interfaces { get; set; } = new();
	}

	private enum ServiceLifetime
	{
		Singleton = 0,
		Scoped = 1,
		Transient = 2
	}

	private sealed class ServiceRegistrationComparer : IEqualityComparer<ServiceRegistrationInfo>
	{
		public static ServiceRegistrationComparer Instance { get; } = new();

		public bool Equals(ServiceRegistrationInfo? x, ServiceRegistrationInfo? y)
		{
			if (x is null && y is null)
			{
				return true;
			}

			if (x is null || y is null)
			{
				return false;
			}

			return SymbolEqualityComparer.Default.Equals(x.ClassSymbol, y.ClassSymbol);
		}

		public int GetHashCode(ServiceRegistrationInfo obj)
		{
			return SymbolEqualityComparer.Default.GetHashCode(obj.ClassSymbol);
		}
	}
}
