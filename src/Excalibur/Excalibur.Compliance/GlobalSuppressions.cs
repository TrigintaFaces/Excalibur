// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
		"Usage",
		"CA2254:Template should be a static expression",
		Justification = "Compliance logging uses localized resource templates to support globalization.")]
