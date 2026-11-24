-- Read-only role for the application's SQL Console screen (run as a superuser, once).
-- Defense in depth: the role can only SELECT, every session is read-only,
-- and long queries are stopped after 15 seconds. Password hashes stay hidden.
-- Change the password with:  ALTER ROLE nw_readonly PASSWORD '...';
DROP ROLE IF EXISTS nw_readonly;
CREATE ROLE nw_readonly LOGIN PASSWORD 'change_me';
GRANT CONNECT ON DATABASE northwind TO nw_readonly;
GRANT USAGE ON SCHEMA public TO nw_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO nw_readonly;                 -- tables + views
REVOKE SELECT ON app_users FROM nw_readonly;                                -- hide password_hash
GRANT SELECT (user_id, username, role, employee_id, is_active) ON app_users TO nw_readonly;
ALTER ROLE nw_readonly SET default_transaction_read_only = on;
ALTER ROLE nw_readonly SET statement_timeout = '15s';
