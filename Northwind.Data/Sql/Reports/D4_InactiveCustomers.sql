SELECT c.customer_id, c.company_name, c.country, MAX(o.order_date) AS last_order
FROM customers c LEFT JOIN orders o ON o.customer_id = c.customer_id
GROUP BY c.customer_id, c.company_name, c.country
HAVING MAX(o.order_date) IS NULL
    OR MAX(o.order_date) < CAST(@ReferenceDate AS date) - INTERVAL '6 months'
ORDER BY last_order NULLS FIRST;
