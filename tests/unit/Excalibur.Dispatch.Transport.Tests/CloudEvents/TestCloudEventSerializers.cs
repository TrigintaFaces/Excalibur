// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// The serializer these transport tests hand their adapters.
/// <para>
/// Reflection-backed on purpose: the adapters under test marshal arbitrary anonymous and ad-hoc payload
/// types that no source-generated context declares, and these tests are about transport framing — the
/// header and body shape — not about payload metadata. The reflection-free path is covered where it
/// belongs, against the serialization seam itself.
/// </para>
/// <para>
/// It lives in one place so the production reflection factory is named exactly once across the test
/// project. If that call ever needs a suppression or a justification, there is a single site to put it
/// on rather than eighteen.
/// </para>
/// </summary>
internal static class TestCloudEventSerializers
{
	private static readonly Lazy<CloudEventSerializer> ReflectionLazy = new(Create);

	/// <summary>Gets a shared reflection-backed serializer for transport framing tests.</summary>
	internal static CloudEventSerializer Reflection => ReflectionLazy.Value;

	private static CloudEventSerializer Create() =>
		new(CloudEventPayloadSerialization.CreateReflectionOptions());
}
