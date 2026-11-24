# Northwind WPF app — database setup (PostgreSQL 16, tested)

Run in this order on an empty database named `northwind`:

    psql -d northwind -f 01_northwind.sql          # pthom/northwind_psql (original data)
    psql -d northwind -f 02_app_extensions.sql     # NUMERIC money, sequences, CHECKs, indexes,
                                                   # support_tickets, product_returns, app_users,
                                                   # 2 triggers, 3 views; drops 3 unused tables
    psql -d northwind -f 03_sample_data.sql        # 26 tickets, 14 returns, 4 demo users
    psql -d northwind -f 05_app_role.sql           # nw_app login role (change the password!)

Dashboard / report SQL used by the app: 04_dashboard_queries.sql (10 queries, all tested).

Demo users (password for all: Demo123!): manager, sales, logistics, support.
Check: SELECT ROUND(SUM(net_amount),2) FROM v_order_lines;  -> 1265793.04

    psql -d northwind -f 06_console_role.sql       # nw_readonly role for the SQL Console (change the password!)
