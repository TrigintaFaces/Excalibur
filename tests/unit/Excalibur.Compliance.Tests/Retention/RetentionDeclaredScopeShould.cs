// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Retention;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Compliance.Tests.Retention;

/// <summary>
/// Retention enforcement acts on the retention scope the host DECLARES, never on whatever annotated types
/// happen to be loaded. Both fixture types below are annotated and both are loaded in this test process;
/// only one is declared.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RetentionDeclaredScopeShould
{
	private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task NeverDeleteAnAnnotatedLoadedTypeOutsideTheDeclaredScope()
	{
		var store = new PolicyHonouringStore();
		store.Add(nameof(UndeclaredRetentionSubject), typeof(UndeclaredRetentionSubject), Now.AddDays(-400));
		store.Add(nameof(DeclaredRetentionSubject), typeof(DeclaredRetentionSubject), Now.AddDays(-400));
		var enforcement = BuildEnforcement(store, static services => services.AddRetentionPolicies<DeclaredRetentionSubject>());

		var policies = await enforcement.GetRetentionPoliciesAsync(CancellationToken.None);
		_ = await enforcement.EnforceRetentionAsync(CancellationToken.None);

		// SAFETY: annotated, loaded, 400 days past a 30-day bound -- and not declared, so untouched.
		policies.ShouldNotContain(p => p.TypeName == typeof(UndeclaredRetentionSubject).FullName);
		store.Contains(nameof(UndeclaredRetentionSubject)).ShouldBeTrue();

		// Control in the same pass: the declared record of the same age IS gone, so the store deletes.
		store.Contains(nameof(DeclaredRetentionSubject)).ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteADeclaredRecordPastItsRetentionBound_AndKeepOneWithinIt()
	{
		var store = new PolicyHonouringStore();
		store.Add("expired", typeof(DeclaredRetentionSubject), Now.AddDays(-31));
		store.Add("fresh", typeof(DeclaredRetentionSubject), Now.AddDays(-29));
		var enforcement = BuildEnforcement(store, static services => services.AddRetentionPolicies<DeclaredRetentionSubject>());

		var result = await enforcement.EnforceRetentionAsync(CancellationToken.None);

		// LIVENESS: a declared, in-scope, past-retention record IS deleted ("delete nothing" fails here).
		store.Contains("expired").ShouldBeFalse();
		store.Contains("fresh").ShouldBeTrue();
		result.RecordsCleaned.ShouldBe(1);
		result.PoliciesEvaluated.ShouldBe(1);
		result.CompletedAt.ShouldBe(Now);
	}

	[Fact]
	public async Task DeclareAnAssemblyWithoutReachingTypesOutsideIt()
	{
		var assembly = typeof(DeclaredRetentionSubject).Assembly;
		var enforcement = BuildEnforcement(
			new PolicyHonouringStore(),
			services => services.AddRetentionPoliciesFromAssembly(assembly));

		var policies = await enforcement.GetRetentionPoliciesAsync(CancellationToken.None);

		policies.ShouldContain(p =>
			p.TypeName == typeof(DeclaredRetentionSubject).FullName
			&& p.PropertyName == nameof(DeclaredRetentionSubject.Email)
			&& p.RetentionDays == 30);
		policies.ShouldAllBe(p => assembly.GetType(p.TypeName) != null);
	}

	[Fact]
	public async Task NotDoublePoliciesWhenATypeIsDeclaredTwice()
	{
		var enforcement = BuildEnforcement(
			new PolicyHonouringStore(),
			static services => services
				.AddRetentionPolicies<DeclaredRetentionSubject>()
				.AddRetentionPoliciesFromAssembly(typeof(DeclaredRetentionSubject).Assembly));

		var policies = await enforcement.GetRetentionPoliciesAsync(CancellationToken.None);

		policies.Count(p => p.TypeName == typeof(DeclaredRetentionSubject).FullName).ShouldBe(1);
	}

	[Fact]
	public void FailAtStartupWhenEnabledWithNothingDeclared()
	{
		var services = BaseServices(new PolicyHonouringStore());
		_ = services.AddRetentionEnforcement();
		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RetentionEnforcementOptions>>().Value);
		ex.Message.ShouldContain("no retention scope is declared");
	}

	[Fact]
	public void FailAtStartupWhenEnabledWithNothingDeclaredAndNoContributor()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddRetentionEnforcement();
		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RetentionEnforcementOptions>>().Value);
	}

	/// <summary>
	/// A contributor with its OWN age rule — the shape of the built-in outbox and inbox contributors, and of any
	/// consumer contributor like them — needs no declared scope. Enforcement must start. This is decided by the
	/// capability the contributor declares, not by which type it is.
	/// </summary>
	[Fact]
	public void StartWithNothingDeclared_WhenNoContributorConsumesDeclaredPolicies()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IRetentionContributor>(new SelfContainedAgeContributor());
		_ = services.AddRetentionEnforcement();
		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<RetentionEnforcementOptions>>().Value.Enabled.ShouldBeTrue();
	}

	/// <summary>
	/// The complement: ONE policy-consuming contributor among self-contained ones is enough to require a
	/// declared scope. Without this, a validator that ignored the capability entirely would pass the arm above.
	/// </summary>
	[Fact]
	public void FailAtStartupWithNothingDeclared_WhenAnyContributorConsumesDeclaredPolicies()
	{
		var services = BaseServices(new PolicyHonouringStore());
		_ = services.AddSingleton<IRetentionContributor>(new SelfContainedAgeContributor());
		_ = services.AddRetentionEnforcement();
		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RetentionEnforcementOptions>>().Value);
		ex.Message.ShouldContain("no retention scope is declared");
	}

	/// <summary>
	/// SAFETY. A contributor that said it does not consume declared policies is handed an EMPTY list even when
	/// policies ARE declared, so it cannot act on a population it disclaimed. This goes red if the service hands
	/// the declared policies to every contributor.
	/// </summary>
	[Fact]
	public async Task HandANonConsumingContributorNoPolicies_EvenWhenPoliciesAreDeclared()
	{
		var selfContained = new SelfContainedAgeContributor();
		var services = BaseServices(new PolicyHonouringStore());
		_ = services.AddSingleton<IRetentionContributor>(selfContained);
		_ = services.AddRetentionPolicies<DeclaredRetentionSubject>();
		_ = services.AddRetentionEnforcement();
		using var provider = services.BuildServiceProvider();

		_ = await provider.CreateScope().ServiceProvider.GetRequiredService<IRetentionEnforcementService>()
			.EnforceRetentionAsync(CancellationToken.None).ConfigureAwait(false);

		selfContained.ReceivedPolicies.ShouldNotBeNull("the contributor must still have been invoked");
		selfContained.ReceivedPolicies.ShouldBeEmpty(
			"a contributor that declared ConsumesDeclaredPolicies = false must not be handed the declared policies");
	}

	[Fact]
	public void StartWhenDisabledExplicitlyWithNothingDeclared()
	{
		var services = BaseServices(new PolicyHonouringStore());
		_ = services.AddRetentionEnforcement(static o => o.Enabled = false);
		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IOptions<RetentionEnforcementOptions>>().Value.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void RefuseToDeclareATypeWithNoRetentionPeriod()
	{
		_ = Should.Throw<ArgumentException>(
			() => new ServiceCollection().AddRetentionPolicies<NoRetentionSubject>());
	}

	[Fact]
	public void RefuseToDeclareAnAssemblyWithNoRetentionPeriod()
	{
		_ = Should.Throw<ArgumentException>(
			() => new ServiceCollection().AddRetentionPoliciesFromAssembly(typeof(PersonalDataAttribute).Assembly));
	}

	private static ServiceCollection BaseServices(PolicyHonouringStore store)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
		_ = services.AddSingleton<IRetentionContributor>(store);
		return services;
	}

	private static IRetentionEnforcementService BuildEnforcement(
		PolicyHonouringStore store,
		Action<IServiceCollection> declare)
	{
		var services = BaseServices(store);
		declare(services);
		_ = services.AddRetentionEnforcement();
		var provider = services.BuildServiceProvider();
		return provider.CreateScope().ServiceProvider.GetRequiredService<IRetentionEnforcementService>();
	}

	/// <summary>
	/// A contributor that does exactly what the contract asks: deletes records of the types in
	/// <see cref="RetentionContributorContext.Policies"/> whose age exceeds the policy's bound at
	/// <see cref="RetentionContributorContext.AsOf"/>, and nothing else.
	/// </summary>
	/// <summary>A contributor with its own age rule that never reads declared policies; records what it was given.</summary>
	private sealed class SelfContainedAgeContributor : IRetentionContributor
	{
		public string Name => nameof(SelfContainedAgeContributor);

		public bool ConsumesDeclaredPolicies => false;

		public IReadOnlyList<RetentionPolicy>? ReceivedPolicies { get; private set; }

		public Task<RetentionContributorResult> EnforceAsync(RetentionContributorContext context, CancellationToken cancellationToken)
		{
			ReceivedPolicies = context.Policies;
			return Task.FromResult(RetentionContributorResult.Succeeded(0));
		}
	}

	private sealed class PolicyHonouringStore : IRetentionContributor
	{
		private readonly Dictionary<string, (string TypeName, DateTimeOffset CreatedAt)> _records = [];

		public string Name => nameof(PolicyHonouringStore);

		public bool ConsumesDeclaredPolicies => true;

		public void Add(string id, Type type, DateTimeOffset createdAt) => _records[id] = (type.FullName!, createdAt);

		public bool Contains(string id) => _records.ContainsKey(id);

		public Task<RetentionContributorResult> EnforceAsync(RetentionContributorContext context, CancellationToken cancellationToken)
		{
			var removed = 0;
			foreach (var (id, record) in _records.ToList())
			{
				var policy = context.Policies.FirstOrDefault(p => p.TypeName == record.TypeName);
				if (policy is not null && record.CreatedAt.AddDays(policy.RetentionDays) <= context.AsOf && _records.Remove(id))
				{
					removed++;
				}
			}

			return Task.FromResult(RetentionContributorResult.Succeeded(removed));
		}
	}
}

/// <summary>Annotated and declared in the scope tests.</summary>
public sealed class DeclaredRetentionSubject
{
	[PersonalData(Category = PersonalDataCategory.ContactInfo, RetentionDays = 30)]
	public string? Email { get; set; }
}

/// <summary>Annotated and loaded, but never declared by the enforcement arms.</summary>
public sealed class UndeclaredRetentionSubject
{
	[PersonalData(Category = PersonalDataCategory.ContactInfo, RetentionDays = 30)]
	public string? Email { get; set; }
}

/// <summary>Annotated with no retention period.</summary>
public sealed class NoRetentionSubject
{
	[PersonalData(Category = PersonalDataCategory.ContactInfo)]
	public string? Email { get; set; }
}
