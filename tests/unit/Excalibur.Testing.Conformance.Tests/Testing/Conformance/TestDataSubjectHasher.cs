// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Shared keyed data-subject hasher for tests, using a fixed high-entropy pepper.
/// </summary>
internal static class TestDataSubjectHasher
{
	public static readonly IDataSubjectHasher Instance = new HmacDataSubjectHasher(
		Options.Create(new DataSubjectHashingOptions { Pepper = "test-pepper-0123456789abcdef0123456789ab" }));
}
