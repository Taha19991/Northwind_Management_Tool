SELECT e.first_name || ' ' || e.last_name AS employee, c.category_name, ROUND(SUM(v.net_amount), 2) AS net_revenue
FROM v_order_lines v
JOIN employees  e ON e.employee_id = v.employee_id
JOIN categories c ON c.category_id = v.category_id
GROUP BY e.employee_id, employee, c.category_name
ORDER BY employee, net_revenue DESC;
