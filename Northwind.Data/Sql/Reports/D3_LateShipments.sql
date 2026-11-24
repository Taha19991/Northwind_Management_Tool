SELECT s.company_name AS shipper,
       COUNT(*) FILTER (WHERE o.shipped_date IS NOT NULL)        AS shipped,
       COUNT(*) FILTER (WHERE o.shipped_date > o.required_date)  AS late,
       ROUND(100.0 * COUNT(*) FILTER (WHERE o.shipped_date > o.required_date)
             / NULLIF(COUNT(*) FILTER (WHERE o.shipped_date IS NOT NULL), 0), 1) AS late_pct
FROM orders o JOIN shippers s ON s.shipper_id = o.ship_via
GROUP BY s.company_name ORDER BY late DESC;
