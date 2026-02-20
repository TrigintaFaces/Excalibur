// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Excalibur;

/// <summary>
///     Fake Token provider for testing
/// </summary>
public class FakeTokenProvider
{
	/// <summary>
	///     Gets a fake access Token
	/// </summary>
	/// <returns> A fake access Token </returns>
	public static FakeAccessToken GetToken() => new() { Token = "fake-token" };
}
