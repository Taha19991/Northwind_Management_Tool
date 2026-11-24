SELECT date_trunc('month', order_date)::date AS month, ROUND(SUM(net_amount), 2) AS net_revenue
FROM v_order_lines GROUP BY 1 ORDER BY 1;
