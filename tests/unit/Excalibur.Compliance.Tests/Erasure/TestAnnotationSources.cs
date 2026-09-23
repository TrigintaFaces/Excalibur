// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Deterministic <see cref="IPersonalDataAnnotationSource"/> test doubles for the erasure coverage gate.
/// </summary>
/// <remarks>
/// <para>
/// The production default, <c>ReflectionPersonalDataAnnotationSource</c>, enumerates
/// <see cref="AppDomain.CurrentDomain"/> and reports every <c>[PersonalData]</c>-annotated property it
/// finds. Inside a test process that set is <b>the whole test assembly</b>, not a domain model: this
/// assembly declares four bare <c>[PersonalData]</c> fixture properties (in
/// <c>SubjectFieldCryptorEnvelopeMarkerShould</c>, <c>SubjectFieldCryptorTruncatedEnvelopeFailsClosedShould</c>
/// and two nested types in <c>SensitiveSelectsForEncryptionShould</c>), all of which take the attribute's
/// default <see cref="PersonalDataCategory.General"/>. The annotated-coverage gate therefore saw
/// <c>{General}</c>, no discovered location carried that category, and six arms asserting a COMPLETED
/// erasure went red on an input they never set up and cannot see.
/// </para>
/// <para>
/// An arm whose subject is store-level coverage (contributor / crypto-shred / exemption) must pin this
/// input to <see cref="None"/> so the annotated-coverage condition contributes nothing. An arm whose
/// subject IS annotated coverage states its categories explicitly via <see cref="With"/>. No arm should
/// take the ambient scan: what it returns depends on which fixture types happen to share the assembly,
/// which is not a property of the code under test.
/// </para>
/// </remarks>
internal sealed class TestAnnotationSource(bool scanEstablished, params PersonalDataCategory[] categories)
	: IPersonalDataAnnotationSource
{
	/// <summary>
	/// An <b>established</b> scan that found nothing — the domain genuinely annotates no personal data, so
	/// the annotated-coverage gate cannot fire and store-level coverage is isolated.
	/// </summary>
	/// <remarks>
	/// The establishment flag is what makes this usable for isolation. An empty set alone is ambiguous
	/// between "nothing is annotated" and "the scan could not see", and the gate now refuses the second —
	/// so a double that did not say which it meant would refuse every arm that uses it.
	/// </remarks>
	public static readonly IPersonalDataAnnotationSource None = new TestAnnotationSource(true);

	/// <summary>
	/// A scan that could NOT be completed. Its empty set carries no information, and an erasure must not
	/// be reported Completed on it — this is the trimmed/ahead-of-time host, and it is the state that was
	/// previously indistinguishable from <see cref="None"/>.
	/// </summary>
	public static readonly IPersonalDataAnnotationSource Unestablished = new TestAnnotationSource(false);

	/// <summary>Creates an established source declaring exactly <paramref name="categories"/>.</summary>
	/// <param name="categories">The annotated categories the domain is to be treated as declaring.</param>
	/// <returns>A deterministic annotation source.</returns>
	public static IPersonalDataAnnotationSource With(params PersonalDataCategory[] categories) =>
		new TestAnnotationSource(true, categories);

	/// <inheritdoc />
	public PersonalDataAnnotationScan GetAnnotatedCategories() =>
		new(new HashSet<PersonalDataCategory>(categories), scanEstablished);
}
