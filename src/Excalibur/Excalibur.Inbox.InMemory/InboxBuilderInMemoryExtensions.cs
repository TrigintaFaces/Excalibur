// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory provider on <see cref="IInboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IInboxBuilder"/> interface.
/// </para>
/// <para>
/// The in-memory provider is intended for testing and development scenarios only.
/// Data is not persisted and is lost when the application restarts.
/// </para>
/// </remarks>
public static class InboxBuilderInMemoryExtensions
{
	/// <summary>
	/// Configures the inbox to use in-memory storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Optional action to configure in-memory-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring in-memory as the inbox storage provider.
	/// It registers the <see cref="InMemoryInboxStore"/> and related services.
	/// </para>
	/// <para>
	/// <strong>Warning:</strong> The in-memory store is not suitable for production use.
	/// Use it only for unit tests, integration tests, or local development.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Simple test configuration
	/// services.AddExcaliburInbox(inbox =>
	/// {
	///     inbox.UseInMemory();
	/// });
	///
	/// // With custom limits
	/// services.AddExcaliburInbox(inbox =>
	/// {
	///     inbox.UseInMemory(inmemory =>
	///     {
	///         inmemory.MaxEntries(5000)
	///                 .RetentionPeriod(TimeSpan.FromHours(1))
	///                 .EnableAutomaticCleanup(true)
	///                 .CleanupInterval(TimeSpan.FromMinutes(2));
	///     });
	/// });
	/// </code>
	/// </example>
	public static IInboxBuilder UseInMemory(
		this IInboxBuilder builder,
		Action<IInMemoryInboxBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Create and configure in-memory options
		var inmemoryOptions = new InMemoryInboxOptions();

		if (configure is not null)
		{
			var inmemoryBuilder = new InMemoryInboxBuilder(inmemoryOptions);
			configure(inmemoryBuilder);
		}

		// Register in-memory options
		_ = builder.Services.AddOptions<InMemoryInboxOptions>()
			.Configure(opt =>
			{
				opt.MaxEntries = inmemoryOptions.MaxEntries;
				opt.EnableAutomaticCleanup = inmemoryOptions.EnableAutomaticCleanup;
				opt.CleanupInterval = inmemoryOptions.CleanupInterval;
				opt.RetentionPeriod = inmemoryOptions.RetentionPeriod;
			})
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<InMemoryInboxOptions>, InMemoryInboxOptionsValidator>());

		// Register in-memory inbox store
		builder.Services.TryAddSingleton<InMemoryInboxStore>();
		builder.Services.AddKeyedSingleton<IInboxStore>("inmemory", (sp, _) => sp.GetRequiredService<InMemoryInboxStore>());
		builder.Services.TryAddKeyedSingleton<IInboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IInboxStore>("inmemory"));

		return builder;
	}
}
