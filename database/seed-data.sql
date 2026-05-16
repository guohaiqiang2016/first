-- Initial master data and sample records for the stator/rotor MES reference implementation.
-- Run after database/schema.sql.

INSERT INTO products (product_code, product_name, product_family, customer_part_no, version) VALUES
('STATOR-A', '定子总成 A', 'STATOR', 'CUS-STATOR-A', '1.0'),
('ROTOR-B', '转子总成 B', 'ROTOR', 'CUS-ROTOR-B', '1.0')
ON CONFLICT (product_code) DO UPDATE SET
    product_name = EXCLUDED.product_name,
    product_family = EXCLUDED.product_family,
    customer_part_no = EXCLUDED.customer_part_no,
    version = EXCLUDED.version;

INSERT INTO process_routes (route_code, route_version, product_code, status) VALUES
('STATOR-A', '1.0', 'STATOR-A', 'Released'),
('ROTOR-B', '1.0', 'ROTOR-B', 'Released')
ON CONFLICT (route_code, route_version) DO UPDATE SET status = EXCLUDED.status;

INSERT INTO process_operations (route_code, route_version, operation_code, operation_name, sequence_no, requires_inspection) VALUES
('STATOR-A', '1.0', 'WINDING', '定子绕线', 10, FALSE),
('STATOR-A', '1.0', 'DIPPING', '浸漆固化', 20, FALSE),
('STATOR-A', '1.0', 'EOL_TEST', '定子终检', 30, TRUE),
('ROTOR-B', '1.0', 'PRESS', '转子压装', 10, FALSE),
('ROTOR-B', '1.0', 'MAGNET', '磁钢装配', 20, FALSE),
('ROTOR-B', '1.0', 'BALANCE', '动平衡', 30, TRUE)
ON CONFLICT (route_code, route_version, operation_code) DO UPDATE SET
    operation_name = EXCLUDED.operation_name,
    sequence_no = EXCLUDED.sequence_no,
    requires_inspection = EXCLUDED.requires_inspection;

INSERT INTO operation_material_requirements (route_code, route_version, operation_code, material_code, required_qty) VALUES
('STATOR-A', '1.0', 'WINDING', 'CU-WIRE-001', 0.45),
('STATOR-A', '1.0', 'DIPPING', 'VARNISH-001', 0.20),
('ROTOR-B', '1.0', 'PRESS', 'SHAFT-001', 1),
('ROTOR-B', '1.0', 'PRESS', 'CORE-001', 1),
('ROTOR-B', '1.0', 'MAGNET', 'MAGNET-001', 8),
('ROTOR-B', '1.0', 'MAGNET', 'GLUE-001', 1.50)
ON CONFLICT (route_code, route_version, operation_code, material_code) DO UPDATE SET required_qty = EXCLUDED.required_qty;

INSERT INTO operation_parameter_specs (route_code, route_version, operation_code, tag_code, lower_limit, upper_limit, unit, is_blocking) VALUES
('STATOR-A', '1.0', 'WINDING', 'TENSION', 10, 14, 'N', TRUE),
('STATOR-A', '1.0', 'WINDING', 'TURNS', 48, 48, 'turn', TRUE),
('STATOR-A', '1.0', 'DIPPING', 'OVEN_TEMP', 130, 150, '℃', TRUE),
('STATOR-A', '1.0', 'EOL_TEST', 'IR', 100, NULL, 'MΩ', TRUE),
('ROTOR-B', '1.0', 'PRESS', 'PRESS_FORCE', 20, 35, 'kN', TRUE),
('ROTOR-B', '1.0', 'MAGNET', 'GLUE_WEIGHT', 1.2, 1.8, 'g', TRUE),
('ROTOR-B', '1.0', 'BALANCE', 'UNBALANCE', 0, 3, 'g.mm', TRUE)
ON CONFLICT (route_code, route_version, operation_code, tag_code) DO UPDATE SET
    lower_limit = EXCLUDED.lower_limit,
    upper_limit = EXCLUDED.upper_limit,
    unit = EXCLUDED.unit,
    is_blocking = EXCLUDED.is_blocking;

INSERT INTO equipment (equipment_code, equipment_name, line_code, status) VALUES
('EQ-WND-01', '绕线机 01', 'L-STATOR-01', 'Standby'),
('EQ-DIP-01', '浸漆炉 01', 'L-STATOR-01', 'Standby'),
('EQ-EOL-01', '定子终检台 01', 'L-STATOR-01', 'Standby'),
('EQ-PRS-01', '压装机 01', 'L-ROTOR-01', 'Standby'),
('EQ-MAG-01', '磁钢装配站 01', 'L-ROTOR-01', 'Standby'),
('EQ-BAL-01', '动平衡机 01', 'L-ROTOR-01', 'Standby')
ON CONFLICT (equipment_code) DO UPDATE SET
    equipment_name = EXCLUDED.equipment_name,
    line_code = EXCLUDED.line_code,
    status = EXCLUDED.status;

INSERT INTO production_orders (order_no, product_code, route_code, route_version, planned_quantity, line_code, due_date, status) VALUES
('WO-DEMO-STATOR-001', 'STATOR-A', 'STATOR-A', '1.0', 100, 'L-STATOR-01', DATE '2026-05-20', 'Released'),
('WO-DEMO-ROTOR-001', 'ROTOR-B', 'ROTOR-B', '1.0', 80, 'L-ROTOR-01', DATE '2026-05-20', 'Released')
ON CONFLICT (order_no) DO UPDATE SET
    planned_quantity = EXCLUDED.planned_quantity,
    line_code = EXCLUDED.line_code,
    due_date = EXCLUDED.due_date,
    status = EXCLUDED.status;

INSERT INTO integration_messages (message_id, external_system, direction, message_type, business_key, payload_json, status, created_at) VALUES
('MSG-DEMO-WMS-PICK-STATOR', 'WMS', 'Outbound', 'PickingRequest', 'WO-DEMO-STATOR-001', '{"orderNo":"WO-DEMO-STATOR-001","materials":[{"materialCode":"CU-WIRE-001"},{"materialCode":"VARNISH-001"}]}', 'Pending', TIMESTAMPTZ '2026-05-16 08:00:00+00'),
('MSG-DEMO-WMS-PICK-ROTOR', 'WMS', 'Outbound', 'PickingRequest', 'WO-DEMO-ROTOR-001', '{"orderNo":"WO-DEMO-ROTOR-001","materials":[{"materialCode":"SHAFT-001"},{"materialCode":"CORE-001"},{"materialCode":"MAGNET-001"},{"materialCode":"GLUE-001"}]}', 'Pending', TIMESTAMPTZ '2026-05-16 08:00:00+00')
ON CONFLICT (message_id) DO NOTHING;
