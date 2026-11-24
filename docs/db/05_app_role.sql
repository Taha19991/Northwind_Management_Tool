-- Least-privilege login role for the WPF application (run as a superuser, once)
-- Change the password before use and keep it out of Git.
CREATE ROLE nw_app LOGIN PASSWORD 'change_me';
GRANT CONNECT ON DATABASE northwind TO nw_app;
GRANT USAGE ON SCHEMA public TO nw_app;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO nw_app;                       -- tables + views
GRANT INSERT, UPDATE ON customers, products, orders, order_details,
                        support_tickets, product_returns TO nw_app;
GRANT DELETE ON order_details TO nw_app;                                   -- remove a line while editing an order
GRANT UPDATE (password_hash, is_active) ON app_users TO nw_app;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO nw_app;                  -- new ids via nextval
