SELECT reason, COUNT(*) AS returns, SUM(quantity) AS units, SUM(refund_amount) AS refund
FROM v_return_overview WHERE status <> 'Rejected' GROUP BY reason ORDER BY refund DESC;
