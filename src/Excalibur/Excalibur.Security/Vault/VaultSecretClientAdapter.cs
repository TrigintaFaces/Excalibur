// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Security.Vault;

/// <summary>
/// Default <see cref="IVaultSecretClient"/> implementation that talks to the real
/// HashiCorp Vault KV&#160;v2 secrets engine over HTTP. This adapter is intentionally
/// the only place in the framework that issues live Vault HTTP calls — tests drive a
/// fake <see cref="IVaultSecretClient"/> instead (mirrors the Azure <c>SecretClientAdapter</c>
/// pattern). Uses the BCL <see cref="HttpClient"/> + <see cref="System.Text.Json"/> only,
/// so it adds no third-party dependency and stays AOT-compatible.
/// </summary>
internal sealed class VaultSecretClientAdapter : IVaultSecretClient
{
	private readonly HttpClient _httpClient;
	private readonly string _mountPath;

	/// <summary>
	/// Initializes a new instance of the <see cref="VaultSecretClientAdapter"/> class.
	/// </summary>
	/// <param name="httpClient">
	/// The HTTP client, pre-configured with the Vault base address and
	/// <c>X-Vault-Token</c> authentication header.
	/// </param>
	/// <param name="mountPath"> The KV&#160;v2 secrets-engine mount path (e.g. <c>secret</c>). </param>
	public VaultSecretClientAdapter(HttpClient httpClient, string mountPath)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPath);
		_mountPath = mountPath;
	}

	/// <inheritdoc/>
	public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken)
	{
		var path = BuildDataPath(key);

		using var response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);

		// A missing secret is a normal, non-error outcome — distinct from a backend failure.
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}

		// Any other non-success status is a genuine failure and must surface (never swallowed).
		_ = response.EnsureSuccessStatusCode();

		var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		await using (stream.ConfigureAwait(false))
		{
			using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			// KV v2 read envelope: { "data": { "data": { "value": "<secret>" }, "metadata": { ... } } }
			if (document.RootElement.TryGetProperty("data", out var outer)
				&& outer.ValueKind == JsonValueKind.Object
				&& outer.TryGetProperty("data", out var inner)
				&& inner.ValueKind == JsonValueKind.Object
				&& inner.TryGetProperty("value", out var value)
				&& value.ValueKind == JsonValueKind.String)
			{
				return value.GetString();
			}

			return null;
		}
	}

	/// <inheritdoc/>
	public async Task SetSecretAsync(string key, string value, CancellationToken cancellationToken)
	{
		var path = BuildDataPath(key);

		// KV v2 write envelope: { "data": { "value": "<secret>" } }
		var envelope = new VaultKvWriteEnvelope(
			new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = value });
		var json = JsonSerializer.Serialize(envelope, VaultJsonContext.Default.VaultKvWriteEnvelope);

		using var content = new StringContent(json, Encoding.UTF8, "application/json");
		using var response = await _httpClient.PostAsync(path, content, cancellationToken).ConfigureAwait(false);

		// Surface any write failure; never report success on a non-2xx response.
		_ = response.EnsureSuccessStatusCode();
	}

	private Uri BuildDataPath(string key)
	{
		// Escape each path segment so reserved characters in a consumer-supplied key cannot alter the
		// request path, while preserving '/' as a legitimate KV v2 nested-path separator.
		var segments = key.Split('/');
		for (var i = 0; i < segments.Length; i++)
		{
			segments[i] = Uri.EscapeDataString(segments[i]);
		}

		return new Uri($"/v1/{_mountPath}/data/{string.Join('/', segments)}", UriKind.Relative);
	}
}

/// <summary>The KV&#160;v2 write request envelope (<c>{ "data": { ... } }</c>).</summary>
/// <param name="Data"> The secret key/value map written under the <c>data</c> wrapper. </param>
internal sealed record VaultKvWriteEnvelope(
	[property: JsonPropertyName("data")] IReadOnlyDictionary<string, string> Data);

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible Vault KV&#160;v2 request bodies.
/// </summary>
[JsonSerializable(typeof(VaultKvWriteEnvelope))]
internal sealed partial class VaultJsonContext : JsonSerializerContext;
