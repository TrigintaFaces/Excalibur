-- SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
-- SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

-- Records what an erasure did NOT reach, so a partly-successful erasure still produces a certificate
-- rather than no certificate at all.
--
-- WITHOUT THIS COLUMN THE STORE REFUSES AT STARTUP, by design, naming this column. That refusal is
-- preferable to the alternative: the certificate's signature covers the payload WHOLE, and this store
-- reassembles the document from one column per claim, so a claim with nowhere to live does not merely
-- go missing on read -- the reassembled payload stops matching what was signed and a GENUINE
-- certificate reports as TAMPERED.
--
-- NULLABLE, and that is load-bearing rather than tidiness. The signature is computed over the payload's
-- canonical form, which OMITS this property when it is null. A NOT NULL column defaulting to an empty
-- array would restore an empty list, the canonical form would then emit it, and every certificate
-- issued before this column existed would come back out of this store verifying as a forgery.
--
-- No backfill, for the same reason. A row written before this column existed had nothing outstanding to
-- record, and leaving it NULL is what keeps its signature verifiable. Do not "helpfully" default it.
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[compliance].[ErasureCertificates]')
                 AND name = N'UnreachedData')
BEGIN
    ALTER TABLE [compliance].[ErasureCertificates] ADD UnreachedData NVARCHAR(MAX) NULL;
END
GO
