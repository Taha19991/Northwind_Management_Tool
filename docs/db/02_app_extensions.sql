-- =====================================================================
-- Northwind — application extensions (run AFTER pthom's northwind.sql)
-- PostgreSQL 16 · tested on the pthom/northwind_psql dataset
-- =====================================================================
BEGIN;

-- 0. Tables that are not part of our ER / BCNF schema (empty or unused)
DROP TABLE IF EXISTS customer_customer_demo, customer_demographics, us_states;

-- 1. Exact money types instead of REAL (float) ------------------------
ALTER TABLE order_details
    ALTER COLUMN unit_price TYPE NUMERIC(10,2) USING ROUND(unit_price::numeric, 2),
    ALTER COLUMN discount   TYPE NUMERIC(4,2)  USING ROUND(discount::numeric, 2);
ALTER TABLE orders   ALTER COLUMN freight    TYPE NUMERIC(10,2) USING ROUND(freight::numeric, 2);
ALTER TABLE products ALTER COLUMN unit_price TYPE NUMERIC(10,2) USING ROUND(unit_price::numeric, 2);

-- 2. Wider order ids + automatic ids for new rows ---------------------
ALTER TABLE order_details ALTER COLUMN order_id TYPE INTEGER;
ALTER TABLE orders        ALTER COLUMN order_id TYPE INTEGER;
CREATE SEQUENCE orders_order_id_seq     OWNED BY orders.order_id;
CREATE SEQUENCE products_product_id_seq OWNED BY products.product_id;
CREATE SEQUENCE suppliers_supplier_id_seq OWNED BY suppliers.supplier_id;
CREATE SEQUENCE categories_category_id_seq OWNED BY categories.category_id;
CREATE SEQUENCE shippers_shipper_id_seq OWNED BY shippers.shipper_id;
CREATE SEQUENCE employees_employee_id_seq OWNED BY employees.employee_id;
SELECT setval('orders_order_id_seq',        (SELECT MAX(order_id)    FROM orders));
SELECT setval('products_product_id_seq',    (SELECT MAX(product_id)  FROM products));
SELECT setval('suppliers_supplier_id_seq',  (SELECT MAX(supplier_id) FROM suppliers));
SELECT setval('categories_category_id_seq', (SELECT MAX(category_id) FROM categories));
SELECT setval('shippers_shipper_id_seq',    (SELECT MAX(shipper_id)  FROM shippers));
SELECT setval('employees_employee_id_seq',  (SELECT MAX(employee_id) FROM employees));
ALTER TABLE orders     ALTER COLUMN order_id    SET DEFAULT nextval('orders_order_id_seq');
ALTER TABLE products   ALTER COLUMN product_id  SET DEFAULT nextval('products_product_id_seq');
ALTER TABLE suppliers  ALTER COLUMN supplier_id SET DEFAULT nextval('suppliers_supplier_id_seq');
ALTER TABLE categories ALTER COLUMN category_id SET DEFAULT nextval('categories_category_id_seq');
ALTER TABLE shippers   ALTER COLUMN shipper_id  SET DEFAULT nextval('shippers_shipper_id_seq');
ALTER TABLE employees  ALTER COLUMN employee_id SET DEFAULT nextval('employees_employee_id_seq');
-- customers keep their 5-letter code (entered by the user)

-- 3. Integrity rules from the ER diagram (total participation, ranges) -
ALTER TABLE orders
    ALTER COLUMN customer_id SET NOT NULL,
    ALTER COLUMN employee_id SET NOT NULL,
    ALTER COLUMN order_date  SET NOT NULL,
    ALTER COLUMN ship_via    SET NOT NULL,
    ADD CONSTRAINT ck_orders_shipped CHECK (shipped_date IS NULL OR shipped_date >= order_date),
    ADD CONSTRAINT ck_orders_freight CHECK (freight >= 0);
ALTER TABLE products
    ALTER COLUMN supplier_id SET NOT NULL,
    ALTER COLUMN category_id SET NOT NULL,
    ADD CONSTRAINT uq_products_name UNIQUE (product_name),
    ADD CONSTRAINT ck_products_price CHECK (unit_price >= 0),
    ADD CONSTRAINT ck_products_stock CHECK (units_in_stock >= 0),
    ADD CONSTRAINT ck_products_discontinued CHECK (discontinued IN (0, 1));
ALTER TABLE order_details
    ADD CONSTRAINT ck_od_qty      CHECK (quantity > 0),
    ADD CONSTRAINT ck_od_price    CHECK (unit_price >= 0),
    ADD CONSTRAINT ck_od_discount CHECK (discount BETWEEN 0 AND 1);
ALTER TABLE categories ADD CONSTRAINT uq_categories_name UNIQUE (category_name);

-- 4. Indexes on foreign keys (PostgreSQL does not create them) ---------
CREATE INDEX ix_orders_customer   ON orders(customer_id);
CREATE INDEX ix_orders_employee   ON orders(employee_id);
CREATE INDEX ix_orders_shipper    ON orders(ship_via);
CREATE INDEX ix_od_product        ON order_details(product_id);
CREATE INDEX ix_products_supplier ON products(supplier_id);
CREATE INDEX ix_products_category ON products(category_id);
CREATE INDEX ix_territories_region ON territories(region_id);
CREATE INDEX ix_et_territory      ON employee_territories(territory_id);
CREATE INDEX ix_employees_manager ON employees(reports_to);

-- 5. New project tables -----------------------------------------------
CREATE TABLE support_tickets (
    ticket_id            INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id             INTEGER     REFERENCES orders(order_id),
    customer_id          VARCHAR(5)  REFERENCES customers(customer_id),
    assigned_employee_id SMALLINT    REFERENCES employees(employee_id),
    category  VARCHAR(20) NOT NULL CHECK (category IN ('Late delivery','Damaged product','Wrong item','Billing','Other')),
    priority  VARCHAR(10) NOT NULL DEFAULT 'Medium' CHECK (priority IN ('Low','Medium','High')),
    status    VARCHAR(12) NOT NULL DEFAULT 'Open' CHECK (status IN ('Open','In progress','Resolved','Closed')),
    subject     VARCHAR(120) NOT NULL,
    description TEXT,
    created_at  TIMESTAMP NOT NULL DEFAULT now(),
    resolved_at TIMESTAMP,
    -- 3NF: an order already determines its customer, so a ticket stores EITHER the order OR the customer
    CONSTRAINT ck_ticket_order_xor_customer CHECK ((order_id IS NULL) <> (customer_id IS NULL)),
    CONSTRAINT ck_ticket_resolved CHECK (resolved_at IS NULL OR resolved_at >= created_at)
);
CREATE INDEX ix_tickets_order    ON support_tickets(order_id);
CREATE INDEX ix_tickets_customer ON support_tickets(customer_id);
CREATE INDEX ix_tickets_employee ON support_tickets(assigned_employee_id);

CREATE TABLE product_returns (
    return_id    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id     INTEGER  NOT NULL,
    product_id   SMALLINT NOT NULL,
    ticket_id    INTEGER  REFERENCES support_tickets(ticket_id),
    processed_by SMALLINT REFERENCES employees(employee_id),
    return_date  DATE     NOT NULL DEFAULT CURRENT_DATE,
    quantity     SMALLINT NOT NULL CHECK (quantity > 0),
    reason   VARCHAR(20) NOT NULL CHECK (reason IN ('Damaged','Wrong item','Expired','Not needed','Other')),
    status   VARCHAR(10) NOT NULL DEFAULT 'Requested' CHECK (status IN ('Requested','Approved','Rejected','Refunded')),
    restocked BOOLEAN NOT NULL DEFAULT FALSE,
    FOREIGN KEY (order_id, product_id) REFERENCES order_details(order_id, product_id)
);
CREATE INDEX ix_returns_line ON product_returns(order_id, product_id);

-- 6. Application users (login + role) ----------------------------------
CREATE TABLE app_users (
    user_id       INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username      VARCHAR(40)  NOT NULL UNIQUE,
    password_hash VARCHAR(100) NOT NULL,              -- BCrypt hash, never the password
    role          VARCHAR(12)  NOT NULL CHECK (role IN ('Manager','Sales','Logistics','Support')),
    employee_id   SMALLINT UNIQUE REFERENCES employees(employee_id),
    is_active     BOOLEAN NOT NULL DEFAULT TRUE
);

-- 7. Trigger: a return cannot exceed what was ordered (Ch.5 triggers) --
CREATE FUNCTION trg_check_return_qty() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ordered SMALLINT; already INTEGER;
BEGIN
    SELECT quantity INTO ordered FROM order_details WHERE order_id = NEW.order_id AND product_id = NEW.product_id;
    SELECT COALESCE(SUM(quantity), 0) INTO already FROM product_returns
     WHERE order_id = NEW.order_id AND product_id = NEW.product_id
       AND status <> 'Rejected' AND return_id IS DISTINCT FROM NEW.return_id;
    IF already + NEW.quantity > ordered THEN
        RAISE EXCEPTION 'Return quantity % exceeds remaining ordered quantity % (order %, product %)',
              NEW.quantity, ordered - already, NEW.order_id, NEW.product_id USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER tg_returns_qty BEFORE INSERT OR UPDATE OF quantity, status ON product_returns
    FOR EACH ROW EXECUTE FUNCTION trg_check_return_qty();

-- 8. Trigger: approved + restocked returns go back to stock -------------
CREATE FUNCTION trg_restock() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.restocked AND NOT COALESCE(OLD.restocked, FALSE) AND NEW.status IN ('Approved','Refunded') THEN
        UPDATE products SET units_in_stock = units_in_stock + NEW.quantity WHERE product_id = NEW.product_id;
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER tg_returns_restock AFTER INSERT OR UPDATE OF restocked, status ON product_returns
    FOR EACH ROW EXECUTE FUNCTION trg_restock();

-- 9. Views used by the manager dashboard (Ch.5 views) -------------------
CREATE VIEW v_order_lines AS
SELECT od.order_id, o.order_date, o.customer_id, o.employee_id, o.ship_via,
       od.product_id, p.category_id, od.quantity, od.unit_price, od.discount,
       od.unit_price * od.quantity                     AS gross_amount,     -- exact NUMERIC, round only for display
       od.unit_price * od.quantity * od.discount       AS discount_amount,
       od.unit_price * od.quantity * (1 - od.discount) AS net_amount
FROM order_details od
JOIN orders   o ON o.order_id   = od.order_id
JOIN products p ON p.product_id = od.product_id;

CREATE VIEW v_ticket_overview AS
SELECT t.ticket_id, t.subject, t.category, t.priority, t.status, t.created_at, t.resolved_at,
       COALESCE(t.customer_id, o.customer_id) AS customer_id,      -- customer derived from the order
       c.company_name, t.order_id,
       e.first_name || ' ' || e.last_name     AS assigned_to
FROM support_tickets t
LEFT JOIN orders    o ON o.order_id    = t.order_id
JOIN      customers c ON c.customer_id = COALESCE(t.customer_id, o.customer_id)
LEFT JOIN employees e ON e.employee_id = t.assigned_employee_id;

CREATE VIEW v_return_overview AS
SELECT r.return_id, r.return_date, r.order_id, r.product_id, p.product_name, r.quantity, r.reason, r.status,
       ROUND(od.unit_price * r.quantity * (1 - od.discount), 2) AS refund_amount  -- derived, not stored
FROM product_returns r
JOIN order_details od ON od.order_id = r.order_id AND od.product_id = r.product_id
JOIN products      p  ON p.product_id = r.product_id;

COMMIT;
