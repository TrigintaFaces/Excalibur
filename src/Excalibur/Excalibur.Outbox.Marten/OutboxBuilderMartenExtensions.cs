// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox;
using Excalibur.Outbox.Marten;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Marten provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// The Marten outbox store resolves an <c>IDocumentStore</c> from the container, so the consumer
/// must also register Marten (for example via <c>services.AddMarten(...)</c>) with the outbox
/// document type mapped.
/// </remarks>
public static class OutboxBuilderMartenExtensions
{
	/// <summary>
	/// Configures the outbox to use Marten document storage.
	/// </summary>
	/// <param name="builder"> The outbox builder. </param>
	/// <param name="configure"> Optional action to configure Marten-specific outbox options. </param>
	/// <returns> The builder for fluent chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder"/> is null. </exception>
	public static IOutboxBuilder UseMarten(
		this IOutboxBuilder builder,
		Action<MartenOutboxStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddOptions<MartenOutboxStoreOptions>()
			.Configure(configure ?? (_ => { }))
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MartenOutboxStoreOptions>, MartenOutboxStoreOptionsValidator>());

		builder.Services.TryAddSingleton<MartenOutboxStore>();
		builder.Services.AddKeyedSingleton<IOutboxStore>("marten", (sp, _) => sp.GetRequiredService<MartenOutboxStore>());
		builder.Services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("marten"));

		return builder;
	}
}
