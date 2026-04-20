// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Testing.Conformance.DependencyInjection;

/// <summary>
/// Abstract conformance test kit for validating the <b>Minimal-Wiring DX invariant</b>
/// on any public <c>Add*</c> / <c>Use*</c> builder extension in a shipping Excalibur package.
/// </summary>
/// <typeparam name="TBuilderExtension">
/// A marker type identifying the builder extension under test. Used by the S791
/// Roslyn source generator to emit one concrete <c>{Name}ConformanceTests</c> class
/// per inventory row; when authored by hand (the first-wave regression pins) the
/// marker serves only to scope xUnit class discovery.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>Contract (<c>management/specs/conformance-minimal-wiring-spec.md</c> §3):</b>
/// every public builder extension must either (a) succeed against an empty
/// <see cref="IServiceCollection"/> by <c>TryAdd</c>-registering sensible defaults,
/// or (b) fail loudly at registration time with a message naming the required sibling.
/// </para>
/// <para>
/// <b>Three gates per inventory row (spec §5.1):</b>
/// <list type="number">
/// <item><description>
/// <b>Isolation</b> — invoking the extension against a foundation-only
/// (<see cref="AddRequiredFoundation(IServiceCollection)"/>) service collection must either
/// succeed and produce a resolvable <see cref="IServiceProvider"/> for every service listed in
/// <see cref="ExpectedResolvableServices"/> (Bucket A / C), or throw an
/// <see cref="InvalidOperationException"/> / <see cref="Microsoft.Extensions.Options.OptionsValidationException"/>
/// whose message matches <see cref="ExpectedPrerequisiteMessageFragment"/> (Bucket B).
/// </description></item>
/// <item><description>
/// <b>Idempotence</b> — calling the extension a second time on the same service collection
/// must not add duplicate registrations (enforced via
/// <c>TryAdd*</c> / <c>TryAddEnumerable</c>). Measured by descriptor count delta.
/// </description></item>
/// <item><description>
/// <b>Override</b> — when a consumer pre-registers a sibling service, the extension must
/// defer to the consumer registration (TryAdd semantics preserve overrides). Only exercised
/// when <see cref="PreRegisterOverride"/> is non-null.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// <b>AOT-safety (spec §5.5):</b> this harness does not use <c>Activator.CreateInstance(Type)</c>,
/// <c>Assembly.GetTypes()</c>, or <c>MakeGenericType</c>. All concrete test classes are
/// statically emitted — either hand-authored for the first-wave regression pins or generated
/// at build time from the inventory CSV.
/// </para>
/// </remarks>
public abstract class MinimalWiringConformanceTestKit<TBuilderExtension>
	where TBuilderExtension : class
{
	/// <summary>
	/// Gets the delegate that invokes the builder extension under test.
	/// </summary>
	/// <remarks>
	/// Per spec §4.1 + COMPASS msg 1329, the inventory unit is a
	/// <c>(name, Action&lt;IServiceCollection&gt; invoke)</c> pair — a realized terminal
	/// chain rather than a single node. Consumers implement this by packaging the full
	/// fluent-chain invocation into the delegate body.
	/// </remarks>
	protected abstract Action<IServiceCollection> Invoke { get; }

	/// <summary>
	/// Gets the bucket classification for the extension under test. Defaults to
	/// <see cref="MinimalWiringBucket.SensibleDefaults"/> (Bucket A).
	/// </summary>
	protected virtual MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <summary>
	/// Gets the service types that must be resolvable from
	/// <see cref="IServiceProvider.GetService(Type)"/> after <see cref="Invoke"/> runs
	/// against a foundation-only container. Bucket A and C only; ignored for Bucket B.
	/// </summary>
	protected virtual IReadOnlyList<Type> ExpectedResolvableServices => Array.Empty<Type>();

	/// <summary>
	/// Gets a case-insensitive substring that must appear in the exception message
	/// thrown by <see cref="Invoke"/> when Bucket B prerequisite validation triggers.
	/// Ignored for Bucket A and C.
	/// </summary>
	protected virtual string? ExpectedPrerequisiteMessageFragment => null;

	/// <summary>
	/// Gets a delegate that pre-registers a consumer override for at least one service
	/// the extension would otherwise default via <c>TryAdd</c>. When non-null, the override
	/// gate runs; <see cref="AssertOverridePreserved(IServiceProvider)"/> must then verify
	/// the consumer registration survived.
	/// </summary>
	protected virtual Action<IServiceCollection>? PreRegisterOverride => null;

	/// <summary>
	/// Gets a value indicating whether the override gate should call
	/// <c>BuildServiceProvider(validateOnBuild: true)</c>. Set to <see langword="false"/> for
	/// extensions where a genuine runtime dep (e.g., a DbConnection factory) is intentionally
	/// not registered by the kit but is validated only at first resolve.
	/// </summary>
	protected virtual bool ValidateOnBuild => true;

	/// <summary>
	/// Gets a value indicating whether to enable scope-capture validation
	/// (<see cref="ServiceProviderOptions.ValidateScopes"/>). Default <see langword="true"/>
	/// catches captive-dependency defects (e.g., singleton consuming scoped). Set to
	/// <see langword="false"/> only when the extension under test registers services with
	/// deliberately mixed lifetimes that the harness can't rewire — and file a Beads task
	/// describing the rationale.
	/// </summary>
	protected virtual bool ValidateScopes => true;

	/// <summary>
	/// Gets predicates used to filter out descriptors that the Idempotence gate should
	/// ignore when computing descriptor-count drift across a second <see cref="Invoke"/>
	/// call.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Consumer scenario.</b> Some upstream third-party SDK <c>AddX()</c> extensions
	/// are non-idempotent and re-register descriptors on each call. When the Excalibur
	/// builder under test transitively invokes such a third-party <c>AddX()</c>, the
	/// Excalibur-owned registrations remain idempotent at the Excalibur boundary, but the
	/// total descriptor count drifts because of the upstream re-registration. Passing one
	/// or more predicates matching the upstream-owned descriptors excludes them from the
	/// drift computation without weakening the idempotence contract on Excalibur-owned
	/// descriptors.
	/// </para>
	/// <para>
	/// <b>Default</b> is an empty list — no descriptors are skipped beyond the built-in
	/// <see cref="Microsoft.Extensions.Options.IConfigureOptions{TOptions}"/> /
	/// <see cref="Microsoft.Extensions.Options.IPostConfigureOptions{TOptions}"/> /
	/// <see cref="Microsoft.Extensions.Options.IValidateOptions{TOptions}"/> family
	/// whitelisted by the kit itself (those descriptors are Microsoft-documented to
	/// re-enumerate per <c>AddOptions&lt;T&gt;()</c> call).
	/// </para>
	/// <para>
	/// A descriptor is ignored if <b>any</b> predicate returns <see langword="true"/>
	/// (logical OR).
	/// </para>
	/// <para>
	/// <b>Matching guidance.</b> Predicates SHOULD match on
	/// <see cref="ServiceDescriptor.ServiceType"/> (namespace / assembly), NOT on the
	/// implementation type. Implementation-type matches silently break when an upstream
	/// SDK renames or splits an internal class; service-type matches are stable across
	/// SDK versions because the service contract is the integration boundary.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyList<Func<ServiceDescriptor, bool>> IgnoredDescriptorPredicates =>
		Array.Empty<Func<ServiceDescriptor, bool>>();


	/// <summary>
	/// Adds the minimum hosting-foundation services every extension may reasonably assume
	/// are present — a logging pipeline and a bound <see cref="IConfiguration"/> root.
	/// Override to add additional foundation (hosting lifetime, tenant context) without
	/// widening the scope of what the extension itself must register.
	/// </summary>
	/// <remarks>
	/// Intentionally scoped narrower than <c>Host.CreateApplicationBuilder()</c>: this
	/// default does NOT register hosting-lifetime services (<c>IHostLifetime</c>,
	/// <c>IHostApplicationLifetime</c>, etc.). Extensions that resolve those at build time
	/// must declare them in their own <see cref="AddRequiredFoundation(IServiceCollection)"/>
	/// override (Bucket B Override) or <c>TryAdd</c> their own internal defaults (Bucket A).
	/// </remarks>
	/// <param name="services">The service collection to augment.</param>
	protected virtual void AddRequiredFoundation(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
		services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
	}

	/// <summary>
	/// Verifies that the consumer-provided override survived <see cref="Invoke"/>. Override
	/// in the concrete test class when <see cref="PreRegisterOverride"/> is non-null.
	/// </summary>
	/// <param name="provider">A built service provider with both the override and the extension applied.</param>
	protected virtual void AssertOverridePreserved(IServiceProvider provider)
	{
		_ = provider; // default no-op; concrete classes assert per fix
	}

	// -----------------------------------------------------------------------------
	// Gate implementations — concrete subclasses call via [Fact] wrappers so
	// xUnit discovery sees per-class test methods with their own failure signatures.
	// -----------------------------------------------------------------------------

	/// <summary>
	/// Executes the <b>Isolation</b> gate. See class remarks for the full contract.
	/// </summary>
	protected void ExecuteIsolationGate()
	{
		var services = new ServiceCollection();
		AddRequiredFoundation(services);

		switch (Bucket)
		{
			case MinimalWiringBucket.SensibleDefaults:
			case MinimalWiringBucket.ProgressiveEnhancement:
				if (ExpectedResolvableServices.Count == 0)
				{
					// SENTINEL review F1 (msg 1361): Bucket A/C rows MUST declare at least one
					// expected-resolvable service. A silent no-assert green would mask coverage
					// gaps — particularly harmful once the source generator emits rows at volume.
					throw new TestFixtureAssertionException(
						$"Bucket {Bucket} extension {typeof(TBuilderExtension).Name} must declare at " +
						$"least one entry in {nameof(ExpectedResolvableServices)}. A Bucket {Bucket} " +
						$"Isolation gate with no resolvable services asserts nothing — either add the " +
						$"services the extension advertises, or reclassify the row if no surface is advertised.");
				}

				Invoke(services);
				using (var provider = BuildProvider(services))
				{
					foreach (var type in ExpectedResolvableServices)
					{
						_ = provider.GetService(type)
							?? throw new TestFixtureAssertionException(
								$"Minimal-Wiring contract Bucket {Bucket}: {typeof(TBuilderExtension).Name} " +
								$"must leave {type.FullName} resolvable from an empty container, but GetService returned null.");
					}
				}
				break;

			case MinimalWiringBucket.ExplicitPrerequisite:
				{
					var fragment = ExpectedPrerequisiteMessageFragment;
					if (string.IsNullOrWhiteSpace(fragment))
					{
						throw new TestFixtureAssertionException(
							$"Bucket B extensions must declare {nameof(ExpectedPrerequisiteMessageFragment)}.");
					}

					Exception? captured = null;
					try
					{
						Invoke(services);
						using var provider = BuildProvider(services);
						// Trigger IStartupValidator so ValidateOnStart() checks fire. Most
						// Bucket B extensions defer their sibling validation to this phase.
						var startupValidator = provider.GetService<Microsoft.Extensions.Options.IStartupValidator>();
						startupValidator?.Validate();
					}
					catch (InvalidOperationException ex) { captured = ex; }
					catch (Microsoft.Extensions.Options.OptionsValidationException ex) { captured = ex; }
					catch (AggregateException ex) { captured = ex; }

					if (captured is null)
					{
						throw new TestFixtureAssertionException(
							$"Bucket B extension {typeof(TBuilderExtension).Name} must throw at registration " +
							$"or ValidateOnStart time when the required sibling is missing, but no exception was thrown.");
					}

					if (captured.Message is not string msg ||
					    !msg.Contains(fragment, StringComparison.OrdinalIgnoreCase))
					{
						throw new TestFixtureAssertionException(
							$"Bucket B failure message must name the missing sibling " +
							$"(expected fragment: '{fragment}', actual message: '{captured.Message}').");
					}
				}
				break;

			default:
				throw new InvalidOperationException($"Unrecognized {nameof(MinimalWiringBucket)}: {Bucket}.");
		}
	}

	/// <summary>
	/// Executes the <b>Idempotence</b> gate. See class remarks for the full contract.
	/// </summary>
	protected void ExecuteIdempotenceGate()
	{
		var services = new ServiceCollection();
		AddRequiredFoundation(services);

		if (Bucket == MinimalWiringBucket.ExplicitPrerequisite)
		{
			// Bucket B throws on the first call in an empty container; idempotence is not
			// a meaningful assertion until the required sibling is registered. The concrete
			// pin MUST declare PreRegisterOverride so Idempotence can be exercised once
			// siblings are present (SENTINEL review F2 msg 1361 — no silent green pass).
			if (PreRegisterOverride is null)
			{
				throw new TestFixtureAssertionException(
					$"Bucket B extension {typeof(TBuilderExtension).Name} must declare " +
					$"{nameof(PreRegisterOverride)} so the Idempotence gate can be exercised " +
					$"after the required siblings are registered. A silent return would pass the " +
					$"gate without asserting the contract.");
			}
			PreRegisterOverride(services);
		}

		Invoke(services);
		var before = services.Count;
		Invoke(services);
		var after = services.Count;

		if (after == before)
		{
			return;
		}

		// Benign-drift whitelist: the .NET options pipeline registers
		// IConfigureOptions<T> / IPostConfigureOptions<T> / IValidateOptions<T>
		// per-call as intentionally-enumerable configuration entries. Double-
		// registration is the Microsoft-documented shape for AddOptions<T> +
		// ValidateOnStart(). Drift limited to those descriptors is NOT a
		// Bucket A idempotence violation.
		var drift = new System.Text.StringBuilder();
		var nonBenignCount = 0;
		var consumerPredicates = IgnoredDescriptorPredicates;
		for (var i = before; i < after; i++)
		{
			var d = services[i];
			if (IsBenignOptionsDescriptor(d))
			{
				continue;
			}
			if (IsIgnoredByConsumerPredicate(d, consumerPredicates))
			{
				continue;
			}
			nonBenignCount++;
			_ = drift.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
				$"  + {d.Lifetime} {d.ServiceType.FullName} -> " +
				$"{d.ImplementationType?.FullName ?? (d.ImplementationInstance is { } inst ? "instance:" + inst.GetType().FullName : "<factory>")}");
		}

		if (nonBenignCount > 0)
		{
			throw new TestFixtureAssertionException(
				$"Minimal-Wiring contract: second invocation of {typeof(TBuilderExtension).Name} " +
				$"must be a no-op (TryAdd-style idempotence). Descriptor count drifted from " +
				$"{before} to {after} with {nonBenignCount} non-benign additions. " +
				$"Added descriptors (excluding MS-standard IConfigureOptions/IPostConfigureOptions/IValidateOptions):" +
				Environment.NewLine + drift);
		}
	}

	private static bool IsIgnoredByConsumerPredicate(
		ServiceDescriptor descriptor,
		IReadOnlyList<Func<ServiceDescriptor, bool>> predicates)
	{
		// Hot loop — avoid enumerator allocation by indexing.
		for (var i = 0; i < predicates.Count; i++)
		{
			if (predicates[i](descriptor))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsBenignOptionsDescriptor(ServiceDescriptor descriptor)
	{
		var svcType = descriptor.ServiceType;
		if (!svcType.IsGenericType)
		{
			return false;
		}

		var def = svcType.GetGenericTypeDefinition();
		return def == typeof(Microsoft.Extensions.Options.IConfigureOptions<>)
			|| def == typeof(Microsoft.Extensions.Options.IPostConfigureOptions<>)
			|| def == typeof(Microsoft.Extensions.Options.IValidateOptions<>);
	}

	/// <summary>
	/// Executes the <b>Override</b> gate. See class remarks for the full contract.
	/// </summary>
	protected void ExecuteOverrideGate()
	{
		var preRegister = PreRegisterOverride;
		if (preRegister is null)
		{
			// No override target declared; override gate is not applicable for this row.
			return;
		}

		var services = new ServiceCollection();
		AddRequiredFoundation(services);
		preRegister(services);

		Invoke(services);

		using var provider = BuildProvider(services);
		AssertOverridePreserved(provider);
	}

	private ServiceProvider BuildProvider(IServiceCollection services)
	{
		return services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = ValidateOnBuild,
			ValidateScopes = ValidateScopes,
		});
	}
}
