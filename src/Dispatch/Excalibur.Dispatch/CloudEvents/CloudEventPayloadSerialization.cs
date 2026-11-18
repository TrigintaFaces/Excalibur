// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Builds the <see cref="JsonSerializerOptions"/> CloudEvents serialization runs through, composing the
/// framework's source-generated envelope metadata with a consumer-supplied resolver for their own
/// payload types.
/// </summary>
/// <remarks>
/// <para>
/// A CloudEvent has two halves and only one of them is ours. The <b>envelope</b> — the event and its
/// context attributes — is written and read without reflection either way. The <b>payload</b> is the
/// consumer's own CLR type, and no metadata the framework generates can describe a type it has never
/// seen. Serializing that payload is the single call in the whole CloudEvents path that needs a
/// resolver, which is why this type exists and why it is small.
/// </para>
/// <para>
/// The two factories below are deliberately <b>separate methods rather than one method with a nullable
/// parameter</b>, and the reason is the whole design:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="CreateOptions"/> requires a resolver. There is no reflection branch inside it, so it
/// carries no trimming annotation and a consumer calling it learns nothing alarming — because nothing
/// alarming happens.
/// </item>
/// <item>
/// <see cref="CreateReflectionOptions"/> is the reflection path, and it is <b>annotated</b>. The
/// annotation is the point: it appears in the consumer's IntelliSense and at their own build, so the
/// consumer who opted into reflection is the consumer who is told about it.
/// </item>
/// </list>
/// <para>
/// A single factory taking a nullable resolver would silently select one of two materially different
/// contracts based on an argument's nullness, and would force the annotation onto both callers or
/// neither. Trimming and dynamic-code support are also <b>orthogonal</b> preconditions — a trimmed but
/// JIT-capable host has dynamic code available and the reflected-over members removed anyway — so no
/// runtime guard can establish the trimming precondition on the caller's behalf. Splitting the surface
/// is what lets the annotation sit exactly where the unsafe contract does.
/// </para>
/// <para>
/// This mirrors how the platform itself handles the same problem: the reflection-based API is annotated
/// and a separate, unannotated source-generated path is shipped alongside it, rather than one API
/// suppressing the difference.
/// </para>
/// </remarks>
public static class CloudEventPayloadSerialization
{
	/// <summary>
	/// Builds reflection-free serializer options for CloudEvents, composing the framework's
	/// source-generated envelope metadata with the consumer's own payload resolver.
	/// </summary>
	/// <param name="payloadTypeInfoResolver">
	/// The consumer's resolver for the types they publish as CloudEvent data — typically a
	/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> they declare.
	/// </param>
	/// <returns>Frozen options that resolve both the CloudEvents envelope and the consumer's payloads.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="payloadTypeInfoResolver"/> is <see langword="null"/>. Use
	/// <see cref="CreateReflectionOptions"/> for the reflection-based path instead of passing null here.
	/// </exception>
	public static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver payloadTypeInfoResolver)
	{
		ArgumentNullException.ThrowIfNull(payloadTypeInfoResolver);

		// The consumer's resolver is composed AFTER the framework's envelope context, so envelope
		// metadata is found first and theirs covers the payload. Frozen, because a resolver chain that
		// can still be mutated after first use is a different contract from one that cannot.
		var options = new JsonSerializerOptions
		{
			TypeInfoResolver = JsonTypeInfoResolver.Combine(
				CloudEventJsonContext.Default,
				payloadTypeInfoResolver),
		};

		options.MakeReadOnly();
		return options;
	}

	/// <summary>
	/// Builds reflection-based serializer options for CloudEvents, for hosts that are neither trimmed
	/// nor published ahead-of-time.
	/// </summary>
	/// <remarks>
	/// Prefer <see cref="CreateOptions"/> with a source-generated resolver. This method exists for the
	/// many hosts that run on a full runtime and have no reason to declare one, and its annotations are
	/// deliberate rather than an oversight: a consumer who trims or publishes ahead-of-time will see
	/// them at their own build, which is the warning they need and the one a nullable parameter on a
	/// single factory would have hidden.
	/// </remarks>
	/// <returns>Frozen options that resolve payloads reflectively.</returns>
	[RequiresUnreferencedCode(
		"CloudEvents payload serialization uses reflection here. Call CreateOptions with a "
		+ "source-generated IJsonTypeInfoResolver covering the types you publish as CloudEvent data.")]
	[RequiresDynamicCode(
		"CloudEvents payload serialization uses reflection here. Call CreateOptions with a "
		+ "source-generated IJsonTypeInfoResolver covering the types you publish as CloudEvent data.")]
	public static JsonSerializerOptions CreateReflectionOptions()
	{
		// The framework's envelope context is still composed FIRST, so the envelope resolves from
		// generated metadata even on this path and only the payload falls through to reflection.
		var options = new JsonSerializerOptions
		{
			TypeInfoResolver = JsonTypeInfoResolver.Combine(
				CloudEventJsonContext.Default,
				new DefaultJsonTypeInfoResolver()),
		};

		options.MakeReadOnly();
		return options;
	}
}
