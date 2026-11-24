SELECT category, ROUND(AVG(EXTRACT(EPOCH FROM resolved_at - created_at) / 3600), 1) AS avg_hours
FROM support_tickets WHERE resolved_at IS NOT NULL GROUP BY category ORDER BY avg_hours DESC;
