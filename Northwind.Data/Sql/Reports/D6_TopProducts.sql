SELECT p.product_name, ROUND(SUM(v.net_amount), 2) AS net_revenue
FROM v_order_lines v JOIN products p ON p.product_id = v.product_id
GROUP BY p.product_name ORDER BY net_revenue DESC LIMIT 10;
