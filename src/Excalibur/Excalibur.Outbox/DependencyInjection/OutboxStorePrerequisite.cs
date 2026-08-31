// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>
/// Single source of truth for the "no outbox store was configured" condition and the message that
/// names the remedy.
/// </summary>
/// <remarks>
/// <para>
/// Three sites ask this question and they must not answer it differently: the options-validation
/// hook that runs at <c>ValidateOnStart</c>, the startup prerequisite validator that runs at host
/// start, and the service factories for <see cref="IOutboxProcessor"/> / <see
/// cref="IOutboxDispatcher"/>, whose construction is the thing the missing store actually blocks.
/// </para>
/// <para>
/// The predicate is deliberately the dependency itself rather than a proxy for it. A polling store
/// reaches the pipeline as a non-keyed <see cref="IOutboxStore"/> — either registered directly by a
/// consumer, or forwarded from the keyed "default" registration a provider extension creates — and
/// that is the exact constructor parameter <c>OutboxProcessor</c> and <c>MessageOutbox</c> require.
/// A change-feed store satisfies the outbox through the separate <see
/// cref="ICloudNativeOutboxStore"/> contract and is never drained by those two types, so it is
/// accepted here as its own family rather than folded into the first.
/// </para>
/// </remarks>
internal static class OutboxStorePrerequisite
{
	/// <summary>
	/// The message shown when <c>AddOutbox(...)</c> was called but no store backs it. It names the
	/// missing contract and every provider call that supplies one, split by store family.
	/// </summary>
	internal const string MissingStoreMessage =
		"No outbox store has been configured. AddOutbox(...) registers the outbox pipeline but not " +
		"the IOutboxStore it drains, so a provider call is required inside the AddOutbox callback: " +
		"for a polling store call one of UseSqlServer, UsePostgres, UseOracle, UseMongoDB, UseRedis, " +
		"UseElasticSearch, UseMarten, or UseInMemory — for example " +
		"services.AddExcalibur(x => x.AddOutbox(o => o.UseSqlServer(sql => sql.ConnectionString(...)))); " +
		"for a change-feed store call one of UseCosmosDb, UseDynamoDb, or UseFirestore. Registering an " +
		"IOutboxStore directly on the IServiceCollection before the host is built also satisfies this.";

	/// <summary>
	/// Returns <see langword="true"/> when a store the outbox can actually reach is registered.
	/// </summary>
	/// <param name="services">The built provider to probe.</param>
	internal static bool IsSatisfied(IServiceProvider services) =>
		services.GetService<IOutboxStore>() is not null
		|| services.GetService<ICloudNativeOutboxStore>() is not null;
}

/// <summary>
/// Marker options type used to participate in the <c>Microsoft.Extensions.Options</c> validation
/// pipeline so a missing outbox store surfaces via <c>ValidateOnStart()</c>.
/// </summary>
internal sealed class OutboxStorePrerequisiteValidationOptions
{
}

/// <summary>
/// Options-validation hook that fails host startup when <c>AddOutbox(...)</c> was called without a
/// store provider, with a message naming the provider call that fixes it.
/// </summary>
/// <remarks>
/// This runs at <c>IStartupValidator.Validate()</c> — the earliest point at which the whole
/// <c>IServiceCollection</c> is known — so a consumer who registers their own store *after*
/// <c>AddOutbox(...)</c> is still judged on the finished container rather than on registration order.
/// </remarks>
internal sealed class OutboxStorePrerequisiteValidator : IValidateOptions<OutboxStorePrerequisiteValidationOptions>
{
	private readonly IServiceProvider _services;

	public OutboxStorePrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OutboxStorePrerequisiteValidationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return OutboxStorePrerequisite.IsSatisfied(_services)
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(OutboxStorePrerequisite.MissingStoreMessage);
	}
}
