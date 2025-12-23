#!/bin/bash
# Скрипт для запуска тестов и генерации отчёта о покрытии

set -e
echo "================================="
echo "Запуск Unit тестов"
echo "================================="

cd "$(dirname "$0")/.."

if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK не установлен"
    exit 1
fi

echo "✓ .NET SDK: $(dotnet --version)"
echo "Запуск тестов..."

dotnet test tests/unit/HardwareAnalysisSystem.Tests.csproj \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults \
    --logger "console;verbosity=detailed"

echo ""
echo "✓ Тесты завершены!"
echo "Результаты: ./TestResults/"
