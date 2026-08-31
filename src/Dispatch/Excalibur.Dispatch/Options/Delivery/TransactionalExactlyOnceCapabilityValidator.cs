// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Validates, at startup, that when <see cref="ExactlyOnceOptions.RequireTransactionalExactlyOnce"/> is set the
/// registered inbox store can process and mark a message inside a single transaction
/// over EITHER seam -- the relational <see cref="ITransactionalInboxStore"/> or the document-store
/// <see cref="IScopedTransactionalInboxStore"/> -- so a request for transactional exactly-once cannot
/// silently run under the weaker at-least-once boundary. The union is deliberate: a document store serves
/// the guarantee through the scoped seam alone, and testing only the relational interface would reject a
/// configuration that does honour exactly-once.
/// </summary>
/// <remarks>
/// Probes the EFFECTIVE capability via <see cref="IInboxStoreCapabilities.SupportsTransactional"/> so a decorator
/// that statically declares <see cref="ITransactionalInboxStore"/> but wraps a non-transactional inner is
/// rejected at startup rather than throwing <see cref="NotSupportedException"/> at first use. Registered with
/// <c>ValidateOnStart()</c>, this makes the silent-degrade of an explicit exactly-once request structurally
/// inexpressible.
/// </remarks>
internal sealed class TransactionalExactlyOnceCapabilityValidator : IValidateOptions<ExactlyOnceOptions>
{
	private readonly IInboxStore _inboxStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransactionalExactlyOnceCapabilityValidator"/> class.
	/// </summary>
	/// <param name="inboxStore">The registered default inbox store.</param>
	public TransactionalExactlyOnceCapabilityValidator([FromKeyedServices("default")] IInboxStore inboxStore) =>
		_inboxStore = inboxStore;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ExactlyOnceOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!options.RequireTransactionalExactlyOnce)
		{
			return ValidateOptionsResult.Success;
		}

		var supportsTransactional = _inboxStore is IInboxStoreCapabilities capabilities
			? capabilities.SupportsTransactional
			: _inboxStore is ITransactionalInboxStore or IScopedTransactionalInboxStore;

		if (supportsTransactional)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			$"AddExactlyOnceMessaging was configured with RequireTransactionalExactlyOnce=true, but the registered " +
			$"inbox store '{_inboxStore.GetType().FullName}' offers neither transactional seam -- neither " +
			$"'{nameof(ITransactionalInboxStore)}' nor '{nameof(IScopedTransactionalInboxStore)}'. " +
			"Transactional exactly-once requires the handler and the processed-mark to commit in a single " +
			"transaction; without it the pipeline would silently degrade to at-least-once. Register a " +
			"transaction-capable inbox store (for example SqlServer or Postgres), or clear " +
			"RequireTransactionalExactlyOnce to accept the documented at-least-once-with-deduplication boundary.");
	}
}
