// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Microsoft.Extensions.DependencyInjection;

// AddExcaliburDataServices(...) + both AddExcaliburDataServicesWithPersistence(...) overloads
// were deleted in S804 (bd-sdhocq A6) per ADR-325 §2. The canonical composition path is
// services.AddExcalibur(x => ...) which registers Dispatch primitives (including
// DispatchJsonSerializer) and exposes persistence via explicit opt-ins (e.g. .AddPersistence(...)
// on the consumer side, or the per-provider AddExcaliburSqlServices / AddExcaliburPostgres /
// AddExcaliburMongoDb entry points).
//
// Dapper global configuration (MatchNamesWithUnderscores) is now applied on first use of
// IDataRequest infrastructure via a module initializer (see DapperDefaultsInitializer).
