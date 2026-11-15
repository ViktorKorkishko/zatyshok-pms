-- Database roles and grants for a deployed environment (report 2, 12.8.4, listing 12.6).
--
-- Two roles:
--   zatyshok_owner - owns the database and the schemas; the migrations are applied under it;
--   zatyshok_app   - the API and the Worker connect under it: DML on module tables, but on the
--                    audit log only SELECT, INSERT and DELETE (the trigger of the migration
--                    ReportingViewsAndAuditGuard allows DELETE only for records older than 3 years).
--
-- This is not an EF migration because creating roles needs a superuser (or CREATEROLE), while
-- migrations run as the schema owner. In Development (docker compose) the application simply
-- works as the database owner and this script is not needed.
--
-- Order of deployment:
--   1. a superuser creates zatyshok_owner and the database owned by it (passwords from secrets):
--        CREATE ROLE zatyshok_owner LOGIN PASSWORD '...';
--        CREATE DATABASE zatyshok OWNER zatyshok_owner;
--   2. the migrations are applied under zatyshok_owner (dotnet ef migrations script --idempotent);
--   3. a superuser runs this script in that database:
--        psql -h <host> -U postgres -d zatyshok -v ON_ERROR_STOP=1 -f deploy/sql/roles-and-grants.sql
--   4. the password of the application role is set from the secrets:
--        ALTER ROLE zatyshok_app PASSWORD '...';
--
-- The script is plain SQL without psql meta-commands, so tests can run it through Npgsql too.
-- Running it again is safe: existing roles are kept and GRANT of an existing privilege is a no-op.

-- Roles are cluster-wide; create them only if they do not exist yet.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'zatyshok_owner') THEN
        CREATE ROLE zatyshok_owner LOGIN;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'zatyshok_app') THEN
        CREATE ROLE zatyshok_app LOGIN;
    END IF;
END
$$;

-- Objects that already exist.
GRANT USAGE ON SCHEMA identity, rooms, guests, bookings, housekeeping,
                      billing, notifications, audit, reporting TO zatyshok_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA
      identity, rooms, guests, bookings, housekeeping, billing, notifications TO zatyshok_app;

-- USAGE lets the application call nextval() for the DEFAULT of Bookings.Number and Invoices.Number.
GRANT USAGE ON ALL SEQUENCES IN SCHEMA bookings, billing TO zatyshok_app;

-- No UPDATE and no TRUNCATE on the audit log (NFR-10); DELETE is limited by the trigger.
GRANT SELECT, INSERT, DELETE ON audit."AuditLogs" TO zatyshok_app;

-- ALL TABLES also covers views. The views run with the owner's rights, so the application
-- needs no extra rights on the base tables to read them.
GRANT SELECT ON ALL TABLES IN SCHEMA reporting TO zatyshok_app;

-- Objects that zatyshok_owner creates in later migrations get the same rights automatically.
ALTER DEFAULT PRIVILEGES FOR ROLE zatyshok_owner
      IN SCHEMA identity, rooms, guests, bookings, housekeeping, billing, notifications
      GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO zatyshok_app;

ALTER DEFAULT PRIVILEGES FOR ROLE zatyshok_owner
      IN SCHEMA identity, rooms, guests, bookings, housekeeping, billing, notifications
      GRANT USAGE ON SEQUENCES TO zatyshok_app;

ALTER DEFAULT PRIVILEGES FOR ROLE zatyshok_owner IN SCHEMA audit
      GRANT SELECT, INSERT ON TABLES TO zatyshok_app;

ALTER DEFAULT PRIVILEGES FOR ROLE zatyshok_owner IN SCHEMA reporting
      GRANT SELECT ON TABLES TO zatyshok_app;
