SELECT (SELECT ROUND(SUM(net_amount), 2) FROM v_order_lines)                          AS total_net_revenue,
       (SELECT COUNT(*) FROM orders)                                                  AS total_orders,
       (SELECT COUNT(*) FROM orders WHERE shipped_date IS NULL)                       AS open_orders,
       (SELECT COUNT(*) FROM support_tickets WHERE status IN ('Open','In progress'))  AS open_tickets,
       (SELECT COUNT(*) FROM products WHERE discontinued = 0 AND units_in_stock <= reorder_level) AS low_stock_products;
