-- PostgreSQL MIGRATION for Excalibur.Compliance.Postgres — ERASURE CERTIFICATE PAYLOAD COLUMN
-- Version: 1.0
--
-- Adds erasure_certificates.payload and backfills it for rows written before it existed.
-- Run this once per database, before deploying a build that contains this column.
--
--
-- WHY THIS EXISTS
-- ----------------
-- An erasure certificate's signature covers the whole document. Reconstructing that document from
-- individual columns cannot be relied on, because a column has a type and a type may be unable to
-- hold the value it was given: TIMESTAMPTZ keeps microseconds, while a .NET DateTimeOffset carries
-- hundreds of nanoseconds. Measured on this table, three of a certificate's four timestamp columns
-- came back rounded — request_received_at, generated_at and retain_until — so a certificate read
-- back no longer matched the bytes that were signed over it. The consumer-visible symptom was not a
-- missing field; it was a compliance record reporting as altered.
--
-- The remaining columns are unchanged and keep their jobs. They are INDEXES: things a query filters,
-- joins or sweeps on (certificate_id, request_id, retain_until for the retention sweep). Nothing
-- reads them back into the document any more.
--
--
-- WHAT THE BACKFILL DOES, AND WHAT IT CANNOT DO
-- ---------------------------------------------
-- Existing rows keep their claims: the backfill assembles a payload document from the columns so
-- those certificates stay readable. It does NOT make them verifiable, and nothing can. Their
-- signatures were issued under an earlier scheme that covered three identity fields and none of the
-- document's claims, so there is no content integrity to recover — re-issuing the certificate is the
-- only remedy. Checking one of these certificates reports that it carries no signature this
-- framework can verify, which is the accurate answer rather than a report of tampering.
--
-- The timestamps a backfilled document carries are the ones this table holds, at the precision it
-- holds them. That is the honest record of what was persisted.

BEGIN;

ALTER TABLE compliance.erasure_certificates
	ADD COLUMN IF NOT EXISTS payload TEXT;

-- json_build_object, not jsonb_build_object: jsonb reorders keys and rewrites numbers, and this
-- column holds a document verbatim rather than one to be queried into. The member names below are
-- the enum names the certificate's canonical form uses; the integer columns are this table's own
-- representation and are not part of the document.
UPDATE compliance.erasure_certificates
SET payload = json_build_object(
		'CertificateId', certificate_id,
		'RequestId', request_id,
		'DataSubjectReference', data_subject_reference,
		'RequestReceivedAt', request_received_at,
		'CompletedAt', completed_at,
		'Method', CASE method
			WHEN 0 THEN 'CryptographicErasure'
			WHEN 1 THEN 'PhysicalDeletion'
			WHEN 2 THEN 'SecureOverwrite'
			WHEN 3 THEN 'Hybrid'
		END,
		'Summary', summary,
		'Verification', verification,
		'LegalBasis', CASE legal_basis
			WHEN 0 THEN 'DataNoLongerNecessary'
			WHEN 1 THEN 'ConsentWithdrawal'
			WHEN 2 THEN 'RightToObject'
			WHEN 3 THEN 'UnlawfulProcessing'
			WHEN 4 THEN 'LegalObligation'
			WHEN 5 THEN 'ChildData'
			WHEN 6 THEN 'DataSubjectRequest'
		END,
		'Exceptions', exceptions,
		'GeneratedAt', generated_at,
		'RetainUntil', retain_until,
		'Version', version
	)::text
WHERE payload IS NULL;

-- Fail loudly rather than install a NOT NULL that cannot hold. A row the CASE arms above did not
-- cover carries a method or legal basis this migration does not know, which means the database was
-- written by a build newer than this script.
DO $$
DECLARE
	unmapped bigint;
BEGIN
	SELECT count(*) INTO unmapped FROM compliance.erasure_certificates WHERE payload IS NULL;

	IF unmapped > 0 THEN
		RAISE EXCEPTION
			'% erasure certificate row(s) could not be backfilled: method or legal_basis holds a value '
			'this migration does not map. Check those rows before retrying.', unmapped;
	END IF;
END
$$;

ALTER TABLE compliance.erasure_certificates
	ALTER COLUMN payload SET NOT NULL;

COMMIT;
