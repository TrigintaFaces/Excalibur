// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Tests.Postgres;

internal static class EnabledTestLogger
{
	public static ILogger<T> Create<T>()
	{
		var logger = A.Fake<ILogger<T>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return logger;
	}
}
