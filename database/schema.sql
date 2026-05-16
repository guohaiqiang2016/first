-- Stator/Rotor MES relational schema baseline.
-- Target: PostgreSQL-compatible SQL. The script is idempotent for local/dev initialization.

CREATE TABLE IF NOT EXISTS products (
    product_code VARCHAR(64) PRIMARY KEY,
    product_name VARCHAR(200) NOT NULL,
    product_family VARCHAR(64) NOT NULL,
    customer_part_no VARCHAR(100),
    version VARCHAR(32) NOT NULL DEFAULT '1.0'
);

CREATE TABLE IF NOT EXISTS process_routes (
    route_code VARCHAR(64) NOT NULL,
    route_version VARCHAR(32) NOT NULL,
    product_code VARCHAR(64) NOT NULL REFERENCES products(product_code),
    status VARCHAR(32) NOT NULL DEFAULT 'Released',
    PRIMARY KEY (route_code, route_version)
);

CREATE TABLE IF NOT EXISTS process_operations (
    route_code VARCHAR(64) NOT NULL,
    route_version VARCHAR(32) NOT NULL,
    operation_code VARCHAR(64) NOT NULL,
    operation_name VARCHAR(200) NOT NULL,
    sequence_no INT NOT NULL,
    requires_inspection BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (route_code, route_version, operation_code),
    FOREIGN KEY (route_code, route_version) REFERENCES process_routes(route_code, route_version)
);

CREATE TABLE IF NOT EXISTS operation_material_requirements (
    route_code VARCHAR(64) NOT NULL,
    route_version VARCHAR(32) NOT NULL,
    operation_code VARCHAR(64) NOT NULL,
    material_code VARCHAR(64) NOT NULL,
    required_qty NUMERIC(18, 6),
    PRIMARY KEY (route_code, route_version, operation_code, material_code),
    FOREIGN KEY (route_code, route_version, operation_code) REFERENCES process_operations(route_code, route_version, operation_code)
);

CREATE TABLE IF NOT EXISTS operation_parameter_specs (
    route_code VARCHAR(64) NOT NULL,
    route_version VARCHAR(32) NOT NULL,
    operation_code VARCHAR(64) NOT NULL,
    tag_code VARCHAR(64) NOT NULL,
    lower_limit NUMERIC(18, 6),
    upper_limit NUMERIC(18, 6),
    unit VARCHAR(32) NOT NULL,
    is_blocking BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (route_code, route_version, operation_code, tag_code),
    FOREIGN KEY (route_code, route_version, operation_code) REFERENCES process_operations(route_code, route_version, operation_code)
);

CREATE TABLE IF NOT EXISTS equipment (
    equipment_code VARCHAR(64) PRIMARY KEY,
    equipment_name VARCHAR(200) NOT NULL,
    line_code VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Standby'
);

CREATE TABLE IF NOT EXISTS production_orders (
    order_no VARCHAR(64) PRIMARY KEY,
    product_code VARCHAR(64) NOT NULL REFERENCES products(product_code),
    route_code VARCHAR(64) NOT NULL,
    route_version VARCHAR(32) NOT NULL,
    planned_quantity INT NOT NULL CHECK (planned_quantity > 0),
    line_code VARCHAR(64) NOT NULL,
    due_date DATE NOT NULL,
    good_quantity INT NOT NULL DEFAULT 0,
    scrap_quantity INT NOT NULL DEFAULT 0,
    status VARCHAR(32) NOT NULL DEFAULT 'Draft',
    FOREIGN KEY (route_code, route_version) REFERENCES process_routes(route_code, route_version)
);

CREATE TABLE IF NOT EXISTS serial_units (
    serial_no VARCHAR(100) PRIMARY KEY,
    order_no VARCHAR(64) NOT NULL REFERENCES production_orders(order_no),
    product_code VARCHAR(64) NOT NULL REFERENCES products(product_code),
    current_operation_code VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Created'
);

CREATE TABLE IF NOT EXISTS material_bindings (
    id BIGSERIAL PRIMARY KEY,
    serial_no VARCHAR(100) NOT NULL REFERENCES serial_units(serial_no),
    operation_code VARCHAR(64) NOT NULL,
    material_code VARCHAR(64) NOT NULL,
    lot_no VARCHAR(100) NOT NULL,
    quantity NUMERIC(18, 6) NOT NULL,
    bound_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS operation_records (
    id BIGSERIAL PRIMARY KEY,
    serial_no VARCHAR(100) NOT NULL REFERENCES serial_units(serial_no),
    operation_code VARCHAR(64) NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,
    operator_id VARCHAR(64) NOT NULL,
    workstation_code VARCHAR(64) NOT NULL,
    equipment_code VARCHAR(64) NOT NULL REFERENCES equipment(equipment_code)
);

CREATE TABLE IF NOT EXISTS parameter_records (
    id BIGSERIAL PRIMARY KEY,
    serial_no VARCHAR(100) NOT NULL REFERENCES serial_units(serial_no),
    operation_code VARCHAR(64) NOT NULL,
    tag_code VARCHAR(64) NOT NULL,
    value NUMERIC(18, 6) NOT NULL,
    unit VARCHAR(32) NOT NULL,
    is_in_spec BOOLEAN NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS inspection_tasks (
    task_no VARCHAR(100) PRIMARY KEY,
    serial_no VARCHAR(100) NOT NULL REFERENCES serial_units(serial_no),
    order_no VARCHAR(64) NOT NULL REFERENCES production_orders(order_no),
    operation_code VARCHAR(64) NOT NULL,
    inspection_type VARCHAR(64) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Pending',
    created_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS inspection_results (
    id BIGSERIAL PRIMARY KEY,
    serial_no VARCHAR(100) NOT NULL REFERENCES serial_units(serial_no),
    operation_code VARCHAR(64) NOT NULL,
    item_code VARCHAR(64) NOT NULL,
    numeric_value NUMERIC(18, 6),
    text_value TEXT,
    judgement VARCHAR(16) NOT NULL,
    inspector_id VARCHAR(64) NOT NULL,
    inspected_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS nonconformances (
    nc_no VARCHAR(100) PRIMARY KEY,
    serial_no VARCHAR(100) NOT NULL REFERENCES serial_units(serial_no),
    order_no VARCHAR(64) NOT NULL REFERENCES production_orders(order_no),
    operation_code VARCHAR(64) NOT NULL,
    defect_code VARCHAR(64) NOT NULL,
    description TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    disposition VARCHAR(64),
    responsible_department VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL,
    disposed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS equipment_samples (
    id BIGSERIAL PRIMARY KEY,
    equipment_code VARCHAR(64) NOT NULL REFERENCES equipment(equipment_code),
    tag_code VARCHAR(64) NOT NULL,
    value NUMERIC(18, 6) NOT NULL,
    unit VARCHAR(32) NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL,
    order_no VARCHAR(64),
    serial_no VARCHAR(100),
    operation_code VARCHAR(64)
);

CREATE TABLE IF NOT EXISTS equipment_alarms (
    alarm_no VARCHAR(100) PRIMARY KEY,
    equipment_code VARCHAR(64) NOT NULL REFERENCES equipment(equipment_code),
    alarm_code VARCHAR(64) NOT NULL,
    message TEXT NOT NULL,
    raised_at TIMESTAMPTZ NOT NULL,
    is_closed BOOLEAN NOT NULL DEFAULT FALSE,
    closed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS integration_messages (
    message_id VARCHAR(100) PRIMARY KEY,
    external_system VARCHAR(32) NOT NULL,
    direction VARCHAR(16) NOT NULL,
    message_type VARCHAR(64) NOT NULL,
    business_key VARCHAR(100) NOT NULL,
    payload_json TEXT NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'Pending',
    created_at TIMESTAMPTZ NOT NULL,
    delivered_at TIMESTAMPTZ,
    error TEXT
);

CREATE INDEX IF NOT EXISTS ix_material_bindings_lot_no ON material_bindings(lot_no);
CREATE INDEX IF NOT EXISTS ix_parameter_records_serial_operation ON parameter_records(serial_no, operation_code);
CREATE INDEX IF NOT EXISTS ix_integration_messages_outbox ON integration_messages(direction, status, external_system, created_at);
CREATE INDEX IF NOT EXISTS ix_equipment_samples_equipment_time ON equipment_samples(equipment_code, recorded_at);
