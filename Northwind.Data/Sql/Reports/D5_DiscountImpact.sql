SELECT CASE WHEN discount > 0 THEN 'With discount' ELSE 'No discount' END AS line_type,
       COUNT(*) AS lines, ROUND(SUM(gross_amount), 2) AS gross, ROUND(SUM(discount_amount), 2) AS discount, ROUND(SUM(net_amount), 2) AS net
FROM v_order_lines GROUP BY 1 ORDER BY 1;
