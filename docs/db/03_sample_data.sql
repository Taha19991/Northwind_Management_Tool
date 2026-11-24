-- Sample data for the project tables (run after 02_app_extensions.sql)
BEGIN;
-- demo users, password for all: Demo123!  (BCrypt hash)
INSERT INTO app_users (username, password_hash, role, employee_id) VALUES
  ('manager',   '$2b$11$/pwHLCaBvYYoBjIbzm9/I.yUgvAycB7o14bg3RhF9K6NtV291JQe6', 'Manager',   2),
  ('sales',     '$2b$11$/pwHLCaBvYYoBjIbzm9/I.yUgvAycB7o14bg3RhF9K6NtV291JQe6', 'Sales',     1),
  ('logistics', '$2b$11$/pwHLCaBvYYoBjIbzm9/I.yUgvAycB7o14bg3RhF9K6NtV291JQe6', 'Logistics', 5),
  ('support',   '$2b$11$/pwHLCaBvYYoBjIbzm9/I.yUgvAycB7o14bg3RhF9K6NtV291JQe6', 'Support',   8);

INSERT INTO support_tickets (order_id, customer_id, assigned_employee_id, category, priority, status, subject, description, created_at, resolved_at) VALUES
  (10264, NULL, 8, 'Damaged product', 'High', 'In progress', 'Damaged goods in order 10264', 'Customer contacted support about order 10264.', '1996-08-23 10:51', NULL),
  (10271, NULL, 8, 'Late delivery', 'Medium', 'In progress', 'Order 10271 arrived after the required date', 'Customer contacted support about order 10271.', '1996-09-02 10:47', NULL),
  (10280, NULL, 8, 'Late delivery', 'Low', 'Open', 'Order 10280 arrived after the required date', 'Customer contacted support about order 10280.', '1996-09-14 10:54', NULL),
  (10302, NULL, 8, 'Wrong item', 'Low', 'Resolved', 'Wrong product delivered in order 10302', 'Customer contacted support about order 10302.', '1996-10-11 10:58', '1996-10-13 16:30'),
  (10380, NULL, 8, 'Late delivery', 'Low', 'Open', 'Order 10380 arrived after the required date', 'Customer contacted support about order 10380.', '1997-01-18 10:34', NULL),
  (10423, NULL, 8, 'Late delivery', 'High', 'Resolved', 'Order 10423 arrived after the required date', 'Customer contacted support about order 10423.', '1997-02-24 10:26', '1997-02-27 16:30'),
  (10427, NULL, 8, 'Damaged product', 'Medium', 'Open', 'Damaged goods in order 10427', 'Customer contacted support about order 10427.', '1997-03-06 10:15', NULL),
  (10523, NULL, 8, 'Late delivery', 'Low', 'Resolved', 'Order 10523 arrived after the required date', 'Customer contacted support about order 10523.', '1997-06-01 10:55', '1997-06-06 16:30'),
  (10545, NULL, 8, 'Late delivery', 'High', 'Open', 'Order 10545 arrived after the required date', 'Customer contacted support about order 10545.', '1997-06-26 10:24', NULL),
  (10578, NULL, 8, 'Wrong item', 'Low', 'In progress', 'Wrong product delivered in order 10578', 'Customer contacted support about order 10578.', '1997-07-25 10:34', NULL),
  (10596, NULL, 8, 'Late delivery', 'Medium', 'Closed', 'Order 10596 arrived after the required date', 'Customer contacted support about order 10596.', '1997-08-14 10:20', '1997-08-20 16:30'),
  (10660, NULL, 8, 'Late delivery', 'High', 'Resolved', 'Order 10660 arrived after the required date', 'Customer contacted support about order 10660.', '1997-10-17 10:27', '1997-10-19 16:30'),
  (10705, NULL, 8, 'Damaged product', 'Low', 'In progress', 'Damaged goods in order 10705', 'Customer contacted support about order 10705.', '1997-11-19 10:39', NULL),
  (10726, NULL, 8, 'Late delivery', 'High', 'Resolved', 'Order 10726 arrived after the required date', 'Customer contacted support about order 10726.', '1997-12-08 10:45', '1997-12-14 16:30'),
  (10749, NULL, 8, 'Late delivery', 'Low', 'Resolved', 'Order 10749 arrived after the required date', 'Customer contacted support about order 10749.', '1997-12-20 10:12', '1997-12-21 16:30'),
  (10827, NULL, 8, 'Wrong item', 'Low', 'Resolved', 'Wrong product delivered in order 10827', 'Customer contacted support about order 10827.', '1998-02-09 10:46', '1998-02-10 16:30'),
  (10927, NULL, 8, 'Late delivery', 'High', 'In progress', 'Order 10927 arrived after the required date', 'Customer contacted support about order 10927.', '1998-04-10 10:41', NULL),
  (10960, NULL, 8, 'Late delivery', 'Medium', 'Closed', 'Order 10960 arrived after the required date', 'Customer contacted support about order 10960.', '1998-04-11 10:18', '1998-04-13 16:30'),
  (NULL, 'GREAL', NULL, 'Billing', 'Low', 'In progress', 'Invoice amount question', 'General request, not tied to one order.', '1998-04-03 09:00', NULL),
  (NULL, 'SEVES', NULL, 'Billing', 'Low', 'Open', 'Invoice amount question', 'General request, not tied to one order.', '1998-02-21 09:00', NULL),
  (NULL, 'ROMEY', NULL, 'Billing', 'Low', 'Closed', 'Invoice amount question', 'General request, not tied to one order.', '1998-01-13 09:00', '1998-01-15 12:00'),
  (NULL, 'HANAR', NULL, 'Other', 'Low', 'Closed', 'Request for updated catalogue', 'General request, not tied to one order.', '1998-03-18 09:00', '1998-03-20 12:00'),
  (NULL, 'SPLIR', NULL, 'Billing', 'Low', 'Open', 'Invoice amount question', 'General request, not tied to one order.', '1998-03-25 09:00', NULL),
  (NULL, 'OLDWO', NULL, 'Other', 'Low', 'Open', 'Request for updated catalogue', 'General request, not tied to one order.', '1998-03-14 09:00', NULL),
  (NULL, 'MORGK', NULL, 'Billing', 'Low', 'Closed', 'Invoice amount question', 'General request, not tied to one order.', '1998-01-24 09:00', '1998-01-26 12:00'),
  (NULL, 'LINOD', NULL, 'Other', 'Low', 'In progress', 'Request for updated catalogue', 'General request, not tied to one order.', '1998-01-28 09:00', NULL);

INSERT INTO product_returns (order_id, product_id, processed_by, return_date, quantity, reason, status, restocked) VALUES
  (10764, 3, 5, '1997-12-20', 5, 'Expired', 'Requested', FALSE),
  (10492, 42, 5, '1997-04-15', 4, 'Expired', 'Requested', FALSE),
  (10960, 24, 5, '1998-04-20', 3, 'Damaged', 'Approved', TRUE),
  (10779, 16, 5, '1998-01-29', 1, 'Wrong item', 'Refunded', FALSE),
  (10667, 71, 5, '1997-09-24', 2, 'Expired', 'Refunded', FALSE),
  (10751, 30, 5, '1997-12-14', 5, 'Wrong item', 'Refunded', FALSE),
  (10408, 62, 5, '1997-01-27', 3, 'Expired', 'Refunded', FALSE),
  (10373, 71, 5, '1996-12-17', 4, 'Damaged', 'Refunded', TRUE),
  (10551, 35, 5, '1997-06-18', 3, 'Wrong item', 'Requested', FALSE),
  (10887, 25, 5, '1998-03-02', 5, 'Damaged', 'Approved', TRUE),
  (10382, 5, 5, '1996-12-19', 2, 'Expired', 'Requested', FALSE),
  (10694, 59, 5, '1997-10-19', 1, 'Wrong item', 'Approved', TRUE),
  (10903, 65, 5, '1998-03-16', 5, 'Not needed', 'Approved', FALSE),
  (11032, 36, 5, '1998-04-29', 2, 'Damaged', 'Refunded', FALSE);

COMMIT;
