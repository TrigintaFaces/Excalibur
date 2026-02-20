// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Marks a type for compile-time service registration by the source generator.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a class, the <c>ServiceRegistrationSourceGenerator</c> will automatically
/// generate DI registration code at compile time. This enables AOT-compatible service
/// registration without runtime reflection.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// // Basic registration with default settings (Scoped lifetime, register as self and interfaces)
/// [AutoRegister]
/// public class OrderHandler : IDispatchHandler&lt;CreateOrderCommand&gt; { }
///
/// // Explicit Singleton lifetime
/// [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
/// public class CacheService : ICacheService { }
///
/// // Register as concrete type only (no interface registration)
/// [AutoRegister(AsSelf = true, AsInterfaces = false)]
/// public class HelperService { }
///
/// // Register for interfaces only (not as concrete type)
/// [AutoRegister(AsSelf = false, AsInterfaces = true)]
/// public class MultiService : IFirst, ISecond { }
/// </code>
/// </para>
/// <para>
/// The generated code produces an extension method that can be called at startup:
/// <code>
/// // In Program.cs or Startup.cs
/// services.AddGeneratedServices();
/// </code>
/// </para>
/// <para>
/// <strong>AOT Benefits:</strong>
/// <list type="bullet">
/// <item><description>No runtime reflection - all service discovery at compile time</description></item>
/// <item><description>Faster startup - no assembly scanning required</description></item>
/// <item><description>Native AOT support - compatible with <c>PublishAot=true</c></description></item>
/// <item><description>Trimming safe - no types unexpectedly removed by IL trimmer</description></item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AutoRegisterAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the service lifetime for the registration.
	/// </summary>
	/// <value>
	/// Default is <see cref="ServiceLifetime.Scoped"/>. Per .NET Core conventions,
	/// Scoped is the safest default for most services as it provides proper
	/// request isolation while avoiding common singleton pitfalls.
	/// </value>
	public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

	/// <summary>
	/// Gets or sets whether to register the type as itself (concrete type registration).
	/// </summary>
	/// <value>
	/// Default is <see langword="true"/>. When enabled, the service can be resolved
	/// by its concrete type (e.g., <c>services.GetRequiredService&lt;MyService&gt;()</c>).
	/// </value>
	public bool AsSelf { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to register the type for all implemented interfaces.
	/// </summary>
	/// <value>
	/// Default is <see langword="true"/>. When enabled, the service is registered
	/// for each interface it implements (e.g., if <c>MyService : IFirst, ISecond</c>,
	/// it will be registered for both <c>IFirst</c> and <c>ISecond</c>).
	/// </value>
	/// <remarks>
	/// System interfaces from <c>System</c> namespace (like <see cref="IDisposable"/>)
	/// are excluded from automatic interface registration.
	/// </remarks>
	public bool AsInterfaces { get; set; } = true;
}
