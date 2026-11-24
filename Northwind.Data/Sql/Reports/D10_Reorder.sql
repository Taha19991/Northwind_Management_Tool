SELECT product_id, product_name, units_in_stock, units_on_order, reorder_level
FROM products WHERE discontinued = 0 AND units_in_stock <= reorder_level ORDER BY units_in_stock;
