-- Manager dashboard queries (used by the WPF app; :ref_date = MAX(order_date) in this dataset)

-- K1. KPI cards
SELECT (SELECT ROUND(SUM(net_amount), 2) FROM v_order_lines)                          AS total_net_revenue,
       (SELECT COUNT(*) FROM orders)                                                  AS total_orders,
       (SELECT COUNT(*) FROM orders WHERE shipped_date IS NULL)                       AS open_orders,
       (SELECT COUNT(*) FROM support_tickets WHERE status IN ('Open','In progress'))  AS open_tickets,
       (SELECT COUNT(*) FROM products WHERE discontinued = 0 AND units_in_stock <= reorder_level) AS low_stock_products;

-- D1. Monthly net revenue (line chart)
SELECT date_trunc('month', order_date)::date AS month, ROUND(SUM(net_amount), 2) AS net_revenue
FROM v_order_lines GROUP BY 1 ORDER BY 1;

-- D2. Revenue per employee per category (plan query 1 · grouped bar / heat map)
SELECT e.first_name || ' ' || e.last_name AS employee, c.category_name, ROUND(SUM(v.net_amount), 2) AS net_revenue
FROM v_order_lines v
JOIN employees  e ON e.employee_id = v.employee_id
JOIN categories c ON c.category_id = v.category_id
GROUP BY e.employee_id, employee, c.category_name
ORDER BY employee, net_revenue DESC;

-- D3. Late shipments per shipper (plan query 2 · bar chart)
SELECT s.company_name AS shipper,
       COUNT(*) FILTER (WHERE o.shipped_date IS NOT NULL)        AS shipped,
       COUNT(*) FILTER (WHERE o.shipped_date > o.required_date)  AS late,
       ROUND(100.0 * COUNT(*) FILTER (WHERE o.shipped_date > o.required_date)
             / NULLIF(COUNT(*) FILTER (WHERE o.shipped_date IS NOT NULL), 0), 1) AS late_pct
FROM orders o JOIN shippers s ON s.shipper_id = o.ship_via
GROUP BY s.company_name ORDER BY late DESC;

-- D4. Customers with no order in the 6 months before the reference date (plan query 3 · grid)
SELECT c.customer_id, c.company_name, c.country, MAX(o.order_date) AS last_order
FROM customers c LEFT JOIN orders o ON o.customer_id = c.customer_id
GROUP BY c.customer_id, c.company_name, c.country
HAVING MAX(o.order_date) IS NULL
    OR MAX(o.order_date) < (SELECT MAX(order_date) FROM orders) - INTERVAL '6 months'
ORDER BY last_order NULLS FIRST;

-- D5. Discount impact (plan query 4 · stacked bar)
SELECT CASE WHEN discount > 0 THEN 'With discount' ELSE 'No discount' END AS line_type,
       COUNT(*) AS lines, ROUND(SUM(gross_amount), 2) AS gross, ROUND(SUM(discount_amount), 2) AS discount, ROUND(SUM(net_amount), 2) AS net
FROM v_order_lines GROUP BY 1 ORDER BY 1;

-- D6. Top 10 products by net revenue
SELECT p.product_name, ROUND(SUM(v.net_amount), 2) AS net_revenue
FROM v_order_lines v JOIN products p ON p.product_id = v.product_id
GROUP BY p.product_name ORDER BY net_revenue DESC LIMIT 10;

-- D7. Support tickets by category and status
SELECT category, status, COUNT(*) AS tickets FROM support_tickets GROUP BY category, status ORDER BY category, status;

-- D8. Average resolution time (hours) per category
SELECT category, ROUND(AVG(EXTRACT(EPOCH FROM resolved_at - created_at) / 3600), 1) AS avg_hours
FROM support_tickets WHERE resolved_at IS NOT NULL GROUP BY category ORDER BY avg_hours DESC;

-- D9. Returns and refund amount per reason
SELECT reason, COUNT(*) AS returns, SUM(quantity) AS units, SUM(refund_amount) AS refund
FROM v_return_overview WHERE status <> 'Rejected' GROUP BY reason ORDER BY refund DESC;

-- D10. Products to reorder
SELECT product_id, product_name, units_in_stock, units_on_order, reorder_level
FROM products WHERE discontinued = 0 AND units_in_stock <= reorder_level ORDER BY units_in_stock;
