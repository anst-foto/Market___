<#
.SYNOPSIS
    Сборка, восстановление зависимостей и запуск тестов .NET проекта с генерацией HTML-отчёта.
.DESCRIPTION
    Скрипт выполняет dotnet restore, dotnet build и dotnet test с опцией --logger:html.
    Всегда открывает сгенерированный отчёт в проводнике.
.PARAMETER ProjectPath
    Путь к файлу .sln или .csproj (по умолчанию текущая директория).
.PARAMETER Configuration
    Конфигурация сборки: Debug или Release (по умолчанию Debug).
.PARAMETER ResultsPath
    Путь к папке, куда будет сохранён HTML-отчёт (по умолчанию ./Test/TestResults).
.PARAMETER NoRestore
    Если указан, пропускает шаг восстановления (полезно при повторных запусках).
.EXAMPLE
    .\RunTests.ps1
    .\RunTests.ps1 -ProjectPath .\MySolution.sln -Configuration Release
    .\RunTests.ps1 -ResultsPath .\Reports -NoRestore
#>

param(
    [string]$ProjectPath = ".",
    [string]$Configuration = "Debug",
    [string]$ResultsPath = ".\Market.Core.Test\TestResults",
    [switch]$NoRestore
)

# --- Настройка кодировки консоли (для корректного отображения эмодзи) ---
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# --- Вспомогательные функции для красивого вывода ---
function Write-Step {
    param([string]$Message, [string]$Color = "Cyan")
    Write-Host ""
    Write-Host ">> $Message" -ForegroundColor $Color
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Yellow
}

# --- Проверка наличия dotnet ---
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-ErrorMsg "Команда 'dotnet' не найдена. Убедитесь, что .NET SDK установлен и доступен в PATH."
    exit 1
}

# --- Создание папки для отчётов (если её нет) ---
if (-not (Test-Path $ResultsPath)) {
    Write-Step "Создание папки для отчётов: $ResultsPath" "Yellow"
    New-Item -ItemType Directory -Path $ResultsPath -Force | Out-Null
}

# --- Полный путь к файлу отчёта ---
$reportFileName = "results.html"
$reportFullPath = Join-Path -Path $ResultsPath -ChildPath $reportFileName

# --- Основной процесс ---
Write-Step "Начинаем сборку и тестирование проекта: $ProjectPath" "Magenta"
$global:exitCode = 0

# 1. Restore (если не отключено)
if (-not $NoRestore) {
    Write-Step "Шаг 1: Восстановление зависимостей (dotnet restore)" "Cyan"
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet restore $ProjectPath -v q
    $stopwatch.Stop()
    Write-Info "Время выполнения 'dotnet restore': $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "Ошибка восстановления зависимостей. Код: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Success "Восстановление завершено успешно."
} else {
    Write-Info "Пропускаем восстановление зависимостей (указан параметр -NoRestore)."
}

# 2. Build
Write-Step "Шаг 2: Сборка проекта (dotnet build) - конфигурация $Configuration" "Cyan"
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build $ProjectPath --no-restore -c $Configuration -v q
$stopwatch.Stop()
Write-Info "Время выполнения 'dotnet build': $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
if ($LASTEXITCODE -ne 0) {
    Write-ErrorMsg "Ошибка сборки. Код: $LASTEXITCODE"
    exit $LASTEXITCODE
}
Write-Success "Сборка завершена успешно."

# 3. Test с генерацией отчёта
Write-Step "Шаг 3: Запуск тестов с генерацией HTML-отчёта" "Cyan"
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test $ProjectPath --no-build --no-restore -c $Configuration `
    --logger:"html;LogFileName=$reportFileName" `
    --results-directory $ResultsPath `
    -v q
$stopwatch.Stop()
Write-Info "Время выполнения 'dotnet test': $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
if ($LASTEXITCODE -ne 0) {
    Write-ErrorMsg "Тесты завершились с ошибкой. Код: $LASTEXITCODE"
    $global:exitCode = $LASTEXITCODE
} else {
    Write-Success "Все тесты пройдены успешно."
}

# --- Открытие отчёта (всегда) ---
if (Test-Path $reportFullPath) {
    Write-Step "Открытие отчёта: $reportFullPath" "Yellow"
    try {
        explorer.exe $reportFullPath
        Write-Success "Отчёт открыт в проводнике."
    } catch {
        Write-ErrorMsg "Не удалось открыть отчёт: $_"
        $global:exitCode = 1
    }
} else {
    Write-ErrorMsg "Файл отчёта не найден: $reportFullPath"
    $global:exitCode = 1
}

# --- Итоговое сообщение ---
if ($global:exitCode -eq 0) {
    Write-Step "✅ ВСЕ ЭТАПЫ ЗАВЕРШЕНЫ УСПЕШНО" "Green"
} else {
    Write-Step "⚠️  СКРИПТ ЗАВЕРШИЛСЯ С ОШИБКОЙ (код $global:exitCode)" "Red"
}

exit $global:exitCode