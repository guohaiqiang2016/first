#!/usr/bin/env python3
"""Offline validation for the stator/rotor MES C# project structure.

The execution environment used by the agent may not have the .NET SDK installed.
This script provides deterministic repository checks that can still run locally in
CI-like conditions and fail if the C# implementation or test scaffolding is missing.
"""
from __future__ import annotations

from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]

REQUIRED_FILES = [
    "Mes.StatorRotor.sln",
    "src/Mes.Domain/Mes.Domain.csproj",
    "src/Mes.Api/Mes.Api.csproj",
    "src/Mes.Api/Program.cs",
    "src/Mes.Domain/Entities/MesEnums.cs",
    "src/Mes.Domain/Entities/ProductionModels.cs",
    "src/Mes.Domain/Entities/ExecutionModels.cs",
    "src/Mes.Domain/Entities/QualityEquipmentModels.cs",
    "src/Mes.Domain/Entities/IntegrationModels.cs",
    "src/Mes.Domain/Repositories/IMesRepository.cs",
    "src/Mes.Domain/Repositories/InMemoryMesRepository.cs",
    "src/Mes.Domain/Services/MesExecutionService.cs",
    "src/Mes.Domain/Services/MesIntegrationService.cs",
    "database/schema.sql",
    "database/seed-data.sql",
    "database/README.md",
    "tests/Mes.Domain.Tests/Mes.Domain.Tests.csproj",
    "tests/Mes.Domain.Tests/MesExecutionServiceTests.cs",
]

REQUIRED_ENDPOINTS = [
    "/health",
    "/api/dashboard",
    "/api/integrations/erp/production-plans",
    "/api/integrations/outbox",
    "/api/integrations/messages/{messageId}/delivered",
    "/api/integrations/orders/{orderNo}/completion",
    "/api/integrations/orders/{orderNo}/picking-request",
    "/api/orders",
    "/api/orders/{orderNo}/serials",
    "/api/serials/{serialNo}/operations/{operationCode}/start",
    "/api/serials/{serialNo}/operations/{operationCode}/materials",
    "/api/serials/{serialNo}/operations/{operationCode}/parameters",
    "/api/serials/{serialNo}/operations/{operationCode}/complete",
    "/api/serials/{serialNo}/operations/{operationCode}/inspection",
    "/api/nonconformances/{ncNo}/disposition",
    "/api/equipment",
    "/api/equipment/{equipmentCode}/status",
    "/api/equipment/{equipmentCode}/samples",
    "/api/equipment/{equipmentCode}/alarms",
    "/api/trace/{serialNo}",
    "/api/trace/material-lots/{materialLot}",
]

REQUIRED_DOMAIN_RULES = [
    "Missing required materials",
    "out of specification",
    "current operation",
    "does not require inspection",
    "cannot start production",
    "No pending inspection task",
    "QueuePickingRequest",
    "MarkDelivered",
]


def read(relative_path: str) -> str:
    path = ROOT / relative_path
    if not path.exists():
        raise AssertionError(f"Missing required file: {relative_path}")
    return path.read_text(encoding="utf-8")


def assert_contains(text: str, expected: str, context: str) -> None:
    if expected not in text:
        raise AssertionError(f"Expected {context} to contain {expected!r}")


def main() -> int:
    for relative_path in REQUIRED_FILES:
        read(relative_path)

    solution = read("Mes.StatorRotor.sln")
    for project in ["Mes.Domain", "Mes.Api", "Mes.Domain.Tests"]:
        assert_contains(solution, project, "solution")

    api = read("src/Mes.Api/Program.cs")
    for endpoint in REQUIRED_ENDPOINTS:
        assert_contains(api, endpoint, "API endpoints")

    service = read("src/Mes.Domain/Services/MesExecutionService.cs") + read("src/Mes.Domain/Services/MesIntegrationService.cs")
    for rule in REQUIRED_DOMAIN_RULES:
        assert_contains(service, rule, "domain service rules")

    schema = read("database/schema.sql")
    for table_name in [
        "products",
        "process_routes",
        "production_orders",
        "serial_units",
        "material_bindings",
        "parameter_records",
        "inspection_tasks",
        "nonconformances",
        "equipment_samples",
        "integration_messages",
    ]:
        assert_contains(schema, f"CREATE TABLE IF NOT EXISTS {table_name}", "database schema")

    seed_data = read("database/seed-data.sql")
    for seed_value in ["STATOR-A", "ROTOR-B", "EQ-WND-01", "MSG-DEMO-WMS-PICK-STATOR"]:
        assert_contains(seed_data, seed_value, "database seed data")

    tests = read("tests/Mes.Domain.Tests/MesExecutionServiceTests.cs")
    fact_count = len(re.findall(r"\[Fact\]", tests))
    if fact_count < 10:
        raise AssertionError(f"Expected at least 10 xUnit facts, found {fact_count}")

    for scenario in [
        "CompleteOperation_Blocks_WhenRequiredMaterialIsMissing",
        "CompleteOperation_Blocks_WhenBlockingParameterIsOutOfSpec",
        "Trace_ReturnsMaterialParameterOperationAndInspectionHistory",
        "CompleteOperation_CreatesInspectionTask_ForInspectionOperation",
        "FailedInspection_CreatesNonconformance_AndScrapDispositionUpdatesOrder",
        "TraceByMaterialLot_ReturnsImpactedSerials",
        "StartOperation_Blocks_WhenEquipmentIsFaulted",
        "Dashboard_ReturnsPlanQualityAndEquipmentSummary",
        "ReceiveProductionPlan_CreatesOrderAndQueuesWmsPickingRequest",
        "QueueCompletion_CreatesPendingErpOutboxMessageAndMarksDelivered",
    ]:
        assert_contains(tests, scenario, "domain tests")

    print("C# MES project offline validation passed.")
    print(f"Validated {len(REQUIRED_FILES)} required files, {len(REQUIRED_ENDPOINTS)} API endpoints, and {fact_count} xUnit facts.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssertionError as exc:
        print(f"VALIDATION FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1)
