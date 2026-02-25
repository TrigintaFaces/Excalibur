// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
	Justification = "Test project - localization not required for test assertions and logging",
	Scope = "module")]
[assembly: SuppressMessage(
	"Design",
	"CS8632:The annotation for nullable reference types should only be used in code within a '#nullable' context.",
	Justification = "Test project - nullable reference type annotations are used without explicit nullable context enabling",
	Scope = "module")]
[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Test classes may be instantiated via reflection or serve as test data",
	Scope = "module")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
	Justification = "Performance optimization not required in test projects",
	Scope = "module")]
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness",
	Justification = "Test code does not require cryptographically secure randomness",
	Scope = "module")]
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
	Justification = "Test code prioritizes flexibility and maintainability over micro-optimizations",
	Scope = "module")]
