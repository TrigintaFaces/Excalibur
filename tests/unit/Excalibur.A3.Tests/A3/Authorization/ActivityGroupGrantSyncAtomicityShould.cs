// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MsServiceCollection = Microsoft.Extensions.DependencyInjection.ServiceCollection;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Locks the start-up decision about grant-sync atomicity: which compositions are refused, which are warned
/// about, and which are left alone.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ActivityGroupGrantSyncAtomicityShould
{
	/// <summary>
	/// A store that cannot replace grants atomically is refused at start-up under the default option, and the
	/// message names the store and the option so the reader can act on it.
	/// </summary>
	[Fact]
	public void Refuse_a_store_that_cannot_replace_atomically_when_the_option_requires_it()
	{
		var services = new MsServiceCollection();
		services.AddSingleton<IActivityGroupGrantStore, NonAtomicGrantStore>();

		var failure = Should.Throw<InvalidOperationException>(
			() => Validator(services, GrantSyncAtomicity.Required, NullLogger<ActivityGroupGrantSyncPrerequisiteValidator>.Instance).Validate());

		failure.Message.ShouldContain(nameof(NonAtomicGrantStore));
		failure.Message.ShouldContain(nameof(ActivityGroupSyncOptions.GrantSyncAtomicity));
		failure.Message.ShouldContain(nameof(GrantSyncAtomicity.BestEffort));
	}

	/// <summary>
	/// The same composition with the window accepted explicitly starts, and says once what was accepted.
	/// </summary>
	/// <remarks>
	/// The warning is the whole of what <see cref="GrantSyncAtomicity.BestEffort"/> costs a reader, so an arm
	/// that only asserted "it does not throw" would pass against a validator that said nothing at all.
	/// </remarks>
	[Fact]
	public void Warn_once_when_the_host_accepts_the_non_atomic_sync()
	{
		var services = new MsServiceCollection();
		services.AddSingleton<IActivityGroupGrantStore, NonAtomicGrantStore>();
		var logger = new RecordingLogger();
		var validator = Validator(services, GrantSyncAtomicity.BestEffort, logger);

		validator.Validate();
		validator.Validate();

		logger.Warnings.ShouldHaveSingleItem().ShouldContain(nameof(NonAtomicGrantStore));
	}

	/// <summary>
	/// LIVENESS: a store that CAN replace atomically ignores the option and is not refused, so the refusal
	/// above is a decision about the store rather than one this validator makes for everybody.
	/// </summary>
	[Fact]
	public void Leave_a_store_that_can_replace_atomically_alone()
	{
		var services = new MsServiceCollection();
		services.AddSingleton<IActivityGroupGrantStore, AtomicGrantStore>();
		var logger = new RecordingLogger();

		Validator(services, GrantSyncAtomicity.Required, logger).Validate();
		Validator(services, GrantSyncAtomicity.BestEffort, logger).Validate();

		logger.Warnings.ShouldBeEmpty("a store that replaces atomically has no window to warn about");
	}

	/// <summary>
	/// A composition with no grant store at all has nothing to decide about, and must not fail start-up for a
	/// feature it never wired.
	/// </summary>
	[Fact]
	public void Say_nothing_when_no_grant_store_is_composed() =>
		Should.NotThrow(() =>
			Validator(new MsServiceCollection(), GrantSyncAtomicity.Required, NullLogger<ActivityGroupGrantSyncPrerequisiteValidator>.Instance)
				.Validate());

	/// <summary>
	/// An atomicity setting that names neither behaviour is refused, because it would otherwise not match
	/// <see cref="GrantSyncAtomicity.Required"/> and the refusal above would silently not happen.
	/// </summary>
	[Fact]
	public void Refuse_an_atomicity_setting_that_names_neither_behaviour() =>
		new ActivityGroupSyncOptionsValidator()
			.Validate(name: null, new ActivityGroupSyncOptions { GrantSyncAtomicity = (GrantSyncAtomicity)7 })
			.Failed.ShouldBeTrue();

	/// <summary>LIVENESS for the arm above: both named behaviours are accepted.</summary>
	[Theory]
	[InlineData(GrantSyncAtomicity.Required)]
	[InlineData(GrantSyncAtomicity.BestEffort)]
	public void Accept_a_named_atomicity_setting(GrantSyncAtomicity atomicity) =>
		new ActivityGroupSyncOptionsValidator()
			.Validate(name: null, new ActivityGroupSyncOptions { GrantSyncAtomicity = atomicity })
			.Succeeded.ShouldBeTrue();

	private static ActivityGroupGrantSyncPrerequisiteValidator Validator(
		IServiceCollection services,
		GrantSyncAtomicity atomicity,
		ILogger<ActivityGroupGrantSyncPrerequisiteValidator> logger) =>
		new(
			services,
			Options.Create(new ActivityGroupSyncOptions { GrantSyncAtomicity = atomicity }),
			logger);

	/// <summary>A grant store with no atomic replace — the shape the document-database providers have.</summary>
	private class NonAtomicGrantStore : IActivityGroupGrantStore
	{
		public Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> DeleteAllActivityGroupGrantsAsync(string grantType, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> InsertActivityGroupGrantAsync(string userId, string fullName, string tenantId, string grantType,
			string qualifier, DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(string grantType, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<string>>([]);
	}

	/// <summary>A grant store that carries the capability — the shape the SQL and in-memory stores have.</summary>
	private sealed class AtomicGrantStore : NonAtomicGrantStore, IActivityGroupGrantReplacement
	{
		public Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsAsync(string grantType,
			ActivityGroupGrantSnapshot snapshot, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyCollection<string>>([]);

		public Task<IReadOnlyCollection<string>> ReplaceActivityGroupGrantsForUserAsync(string userId, string grantType,
			ActivityGroupGrantSnapshot snapshot, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyCollection<string>>([]);
	}

	/// <summary>Records the warnings written, so the one-warning claim can be checked rather than assumed.</summary>
	private sealed class RecordingLogger : ILogger<ActivityGroupGrantSyncPrerequisiteValidator>
	{
		public List<string> Warnings { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			ArgumentNullException.ThrowIfNull(formatter);

			if (logLevel == LogLevel.Warning)
			{
				Warnings.Add(formatter(state, exception));
			}
		}
	}
}
