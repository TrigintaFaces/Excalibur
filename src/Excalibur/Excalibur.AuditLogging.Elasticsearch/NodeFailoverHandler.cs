// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.Elasticsearch;

/// <summary>
/// A <see cref="DelegatingHandler"/> that round-robins each outgoing request across the configured
/// Elasticsearch cluster nodes, rewriting only the request authority (scheme/host/port) while
/// preserving the request's path, query, headers, and content.
/// </summary>
/// <remarks>
/// <para>
/// This handler is registered <em>inner</em> to the standard resilience handler in the HttpClient
/// pipeline. Because the resilience handler re-invokes the inner pipeline on every retry, each retry
/// re-enters <see cref="SendAsync"/> and selects the next node — preserving per-attempt node failover.
/// </para>
/// </remarks>
internal sealed class NodeFailoverHandler : DelegatingHandler
{
	private readonly Uri[] _nodes;
	private int _nextNodeIndex;

	/// <summary>
	/// Initializes a new instance of the <see cref="NodeFailoverHandler"/> class.
	/// </summary>
	/// <param name="nodes">The cluster node base URIs to round-robin across. Must be non-empty.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="nodes"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="nodes"/> is empty.</exception>
	public NodeFailoverHandler(IReadOnlyList<Uri> nodes)
	{
		ArgumentNullException.ThrowIfNull(nodes);

		if (nodes.Count == 0)
		{
			throw new ArgumentException("At least one node URI must be provided.", nameof(nodes));
		}

		_nodes = [.. nodes];
	}

	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request.RequestUri is not null)
		{
			// Round-robin selection (same positive-modulo math the exporter previously used):
			// the first request targets node[0], preserving the original send order.
			var index = Interlocked.Increment(ref _nextNodeIndex);
			var node = _nodes[(((index - 1) % _nodes.Length) + _nodes.Length) % _nodes.Length];

			// Swap only the authority; keep the exporter-built path (/_bulk?refresh=...) and query intact.
			request.RequestUri = new UriBuilder(request.RequestUri)
			{
				Scheme = node.Scheme,
				Host = node.Host,
				Port = node.Port,
			}.Uri;
		}

		return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
