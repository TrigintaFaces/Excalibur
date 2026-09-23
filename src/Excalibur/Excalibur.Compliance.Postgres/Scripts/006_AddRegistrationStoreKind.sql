-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Records WHICH KIND OF STORE holds a registered location, so an erasure contributor can be offered the
-- obligations it covers and can name what it erased.
--
-- Nullable on purpose. A registration written before this column existed reads back as Unknown, and an
-- Unknown registration is offered to NO contributor -- so it discharges nothing and the erasure refuses
-- to complete rather than certifying an erasure nobody demonstrated. Classify existing registrations to
-- let erasures complete; until you do, they will continue to refuse.
ALTER TABLE "compliance"."data_inventory_registrations"
    ADD COLUMN IF NOT EXISTS store_kind VARCHAR(64) NULL;
