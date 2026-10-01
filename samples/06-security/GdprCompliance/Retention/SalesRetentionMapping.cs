// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

using Excalibur.EventSourcing.Erasure;

namespace GdprCompliance.Retention;

/// <summary>
/// Remembers which aggregates belong to which data subject, keyed by the hash the erasure path uses.
/// </summary>
/// <remarks>
/// <para>
/// An erasure never sees a raw data-subject identifier — it sees the keyed hash — so anything that has
/// to answer "which aggregates are this person's?" has to be keyed the same way. A production
/// deployment does this with a lookup table or an index; this sample keeps it in memory, which is
/// enough to run the demo and loses everything on restart.
/// </para>
/// </remarks>
public sealed class SubjectAggregateIndex
{
	private readonly ConcurrentDictionary<string, List<AggregateReference>> _bySubjectHash = new(StringComparer.Ordinal);

	/// <summary>Records that an aggregate holds personal data belonging to a data subject.</summary>
	/// <param name="dataSubjectIdHash">The hash of the data subject identifier.</param>
	/// <param name="reference">The aggregate holding their data.</param>
	public void Add(string dataSubjectIdHash, AggregateReference reference)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);
		ArgumentNullException.ThrowIfNull(reference);

		var references = _bySubjectHash.GetOrAdd(dataSubjectIdHash, _ => []);
		lock (references)
		{
			if (!references.Contains(reference))
			{
				references.Add(reference);
			}
		}
	}

	/// <summary>Gets every aggregate recorded against a data subject.</summary>
	/// <param name="dataSubjectIdHash">The hash of the data subject identifier.</param>
	/// <returns>The aggregates recorded against them; empty when none are.</returns>
	public IReadOnlyList<AggregateReference> Find(string dataSubjectIdHash)
	{
		if (!_bySubjectHash.TryGetValue(dataSubjectIdHash, out var references))
		{
			return [];
		}

		lock (references)
		{
			return [.. references];
		}
	}
}

/// <summary>
/// Tells the erasure path which aggregates hold a data subject's personal data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both aggregate types are returned, including the retained one, and that is deliberate.</b> This
/// interface answers "where is this person's data?", which is a question of fact. Whether a given
/// aggregate type may be destroyed is a separate, legal question, answered by the retention declared in
/// <c>Program.cs</c> — and it is answered on the erasure path, before anything is destroyed.
/// </para>
/// <para>
/// Withholding the sales record here instead would produce the same surviving record and a <b>silent</b>
/// one: the erasure would never learn the data was there, so the certificate would not name it, its
/// legal basis or its period, and the data subject would never be told their personal data lawfully
/// persists. Declare the retention; do not hide the location.
/// </para>
/// </remarks>
public sealed class SalesRetentionMapping : IAggregateDataSubjectMapping
{
	private readonly SubjectAggregateIndex _index;

	/// <summary>Initializes a new instance of the <see cref="SalesRetentionMapping"/> class.</summary>
	/// <param name="index">The subject-to-aggregate index this sample populates when it seeds.</param>
	public SalesRetentionMapping(SubjectAggregateIndex index) => _index = index;

	/// <inheritdoc/>
	public Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken) =>
		Task.FromResult(_index.Find(dataSubjectIdHash));
}
