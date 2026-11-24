SELECT category, status, COUNT(*) AS tickets FROM support_tickets GROUP BY category, status ORDER BY category, status;
