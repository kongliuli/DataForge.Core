CREATE TABLE IF NOT EXISTS orders (
  "OrderId" integer PRIMARY KEY,
  "Amount" numeric NOT NULL
);

CREATE TABLE IF NOT EXISTS order_headers (
  "OrderId" integer PRIMARY KEY,
  "Customer" text NOT NULL
);

CREATE TABLE IF NOT EXISTS order_lines (
  "OrderId" integer NOT NULL,
  "Sku" text NOT NULL,
  "Qty" integer NOT NULL
);

CREATE TABLE IF NOT EXISTS customer_orders (
  "Name" text NOT NULL,
  "Region" text NOT NULL,
  "Amount" numeric NOT NULL
);
