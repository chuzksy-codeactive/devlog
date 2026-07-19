-- Closes the gap every RLS script since 001 has flagged and deferred:
-- "the app currently connects as the tables' owning role, which bypasses
-- RLS by default." That note undersold the actual severity. Checked
-- against how the official Postgres Docker image behaves (this project's
-- docker-compose.yml sets POSTGRES_USER: devlog): POSTGRES_USER is created
-- as a genuine Postgres SUPERUSER, not just a table owner. Per Postgres's
-- own rules, row security is unconditionally disabled for superusers —
-- ALTER TABLE ... FORCE ROW LEVEL SECURITY, which every prior script's
-- comment pointed to, only affects a non-superuser table OWNER choosing to
-- be bound by its own RLS policies. It does nothing for an actual
-- superuser. So the real fix isn't "force RLS on the owner" — it's
-- "stop the app from connecting as a superuser at all."
--
-- This script creates a separate, ordinary (non-superuser, non-owner)
-- role for the running application to connect as. Migrations continue to
-- run as `devlog` (the owner, needs DDL rights); real app traffic runs as
-- `devlog_app` instead, from this point forward. Because `devlog_app`
-- neither owns these tables nor is a superuser, Postgres's default RLS
-- behavior already applies to it fully — no FORCE ROW LEVEL SECURITY
-- needed anywhere.
--
-- Run this AFTER all migrations and sql/001-004 have been applied.
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/005_create_app_role.sql
-- (password: devlog_local, from docker-compose.yml)

CREATE ROLE devlog_app WITH LOGIN PASSWORD 'devlog_app_local';

GRANT CONNECT ON DATABASE devlog TO devlog_app;
GRANT USAGE ON SCHEMA public TO devlog_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO devlog_app;

-- Without this, every future EF Core migration (still run as `devlog`,
-- the owner) would create a new table devlog_app can't touch until this
-- script is manually re-run — easy to forget, and the kind of gap that
-- silently breaks production the day after a routine migration ships.
-- ALTER DEFAULT PRIVILEGES makes new tables inherit these grants
-- automatically.
ALTER DEFAULT PRIVILEGES FOR ROLE devlog IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO devlog_app;

-- No sequence grants: every entity in this schema uses a client-generated
-- GUID primary key (Id = Guid.NewGuid() in C#), so there are no Postgres
-- sequences/identity columns for devlog_app to need access to.
