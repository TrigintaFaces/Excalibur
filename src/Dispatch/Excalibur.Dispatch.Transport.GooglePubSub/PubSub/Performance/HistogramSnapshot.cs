// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

public sealed class HistogramSnapshot
{
	public long Count { get; set; }

	public long Sum { get; set; }

	public long Min { get; set; }

	public long Max { get; set; }

	public long Mean { get; set; }

	public long P95 { get; set; }
}
