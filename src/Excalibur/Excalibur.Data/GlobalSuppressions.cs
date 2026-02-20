// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

// Suppress CS8602 in generated LoggerMessage code - the generator produces code that may have nullable warnings
// but the actual logger instance is validated in the constructor and cannot be null at runtime
[assembly: SuppressMessage("Compiler", "CS8602:Dereference of a possibly null reference.", Justification = "Generated code from LoggerMessage source generator; logger is validated in constructor and cannot be null at runtime.")]
