<#
.SYNOPSIS
    Сборка, восстановление зависимостей и запуск всех тестов решения с генерацией HTML и TRX отчётов.
.DESCRIPTION
    Скрипт ищет файл решения с расширением .slnx в указанной папке, добавляет в него все тестовые проекты,
    выполняет restore, build и test для всего решения.
.PARAMETER ProjectPath
    Путь к папке с решением (по умолчанию текущая). Если передан путь к .slnx, используется он.
.PARAMETER Configuration
    Конфигурация сборки: Debug или Release (по умолчанию Debug).
.PARAMETER ResultsPath
    Путь к папке для отчётов (по умолчанию ./Test/TestResults).
.PARAMETER NoRestore
    Если указан, пропускает восстановление зависимостей.
.EXAMPLE
    .\RunTests.ps1
    .\RunTests.ps1 -ProjectPath .\MySolution -Configuration Release
    .\RunTests.ps1 -ResultsPath .\Reports -NoRestore
#>

param(
    [string]$ProjectPath = ".",
    [string]$Configuration = "Debug",
    [string]$ResultsPath = ".\Test\TestResults",
    [switch]$NoRestore
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

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

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-ErrorMsg "Команда 'dotnet' не найдена. Установите .NET SDK."
    exit 1
}

# Определяем файл решения — ищем только .slnx
function Get-SolutionFile {
    param([string]$Path)
    if (Test-Path $Path -PathType Container) {
        $slnFiles = Get-ChildItem -Path $Path -File | Where-Object { $_.Extension -eq '.slnx' }
        if ($slnFiles.Count -eq 0) {
            Write-ErrorMsg "Не найден .slnx файл в папке '$Path'."
            exit 1
        }
        if ($slnFiles.Count -gt 1) {
            Write-ErrorMsg "Найдено несколько .slnx файлов: $($slnFiles.Name -join ', '). Укажите конкретный путь."
            exit 1
        }
        return $slnFiles[0].FullName
    } elseif (Test-Path $Path -PathType Leaf) {
        if ($Path -like "*.slnx") {
            return (Resolve-Path $Path).Path
        } else {
            Write-ErrorMsg "Указанный файл не является .slnx: $Path"
            exit 1
        }
    } else {
        Write-ErrorMsg "Путь '$Path' не существует."
        exit 1
    }
}

$slnPath = Get-SolutionFile -Path $ProjectPath
Write-Step "Решение: $slnPath" "Magenta"

# Находим все тестовые проекты в папке решения
$solutionFolder = Split-Path $slnPath -Parent
$testProjects = Get-ChildItem -Path $solutionFolder -Recurse -Filter "*Test*.csproj" -ErrorAction SilentlyContinue
if ($testProjects.Count -eq 0) {
    Write-ErrorMsg "Тестовые проекты не найдены."
    exit 1
}
Write-Info "Найдены тестовые проекты: $($testProjects.Name -join ', ')"

# Проверяем, входят ли они в решение (используем относительный путь)
$slnContent = Get-Content $slnPath -Raw
foreach ($proj in $testProjects) {
    # Вычисляем относительный путь к проекту относительно папки решения
    $relPath = [System.IO.Path]::GetRelativePath($solutionFolder, $proj.FullName)
    if ($slnContent -notmatch [regex]::Escape($relPath)) {
        Write-Step "Добавляем проект $($proj.Name) в решение" "Yellow"
        dotnet sln $slnPath add $proj.FullName
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMsg "Не удалось добавить $($proj.Name) в решение."
            exit 1
        }
        Write-Success "Проект $($proj.Name) добавлен."
    } else {
        Write-Info "Проект $($proj.Name) уже в решении."
    }
}

# Создаём папку для отчётов
if (-not (Test-Path $ResultsPath)) {
    New-Item -ItemType Directory -Path $ResultsPath -Force | Out-Null
}

$reportHtml = Join-Path -Path $ResultsPath -ChildPath "results.html"
$reportTrx   = Join-Path -Path $ResultsPath -ChildPath "results.trx"

$global:exitCode = 0

# Restore
if (-not $NoRestore) {
    Write-Step "Восстановление зависимостей (dotnet restore)" "Cyan"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet restore $slnPath -v q
    $sw.Stop()
    Write-Info "Время: $($sw.Elapsed.ToString('mm\:ss\.fff'))"
    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "Ошибка restore. Код: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Success "Восстановление завершено."
} else {
    Write-Info "Пропускаем restore (параметр -NoRestore)."
}

# Build
Write-Step "Сборка решения (dotnet build) - $Configuration" "Cyan"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet build $slnPath --no-restore -c $Configuration -v q
$sw.Stop()
Write-Info "Время: $($sw.Elapsed.ToString('mm\:ss\.fff'))"
if ($LASTEXITCODE -ne 0) {
    Write-ErrorMsg "Ошибка сборки. Код: $LASTEXITCODE"
    exit $LASTEXITCODE
}
Write-Success "Сборка завершена."

# Test — запускаем все тесты решения с двумя логгерами: HTML и TRX
Write-Step "Запуск всех тестов с генерацией HTML и TRX отчётов" "Cyan"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test $slnPath --no-build --no-restore -c $Configuration `
    --logger:"html;LogFileName=results.html" `
    --logger:"trx;LogFileName=results.trx" `
    --results-directory $ResultsPath `
    -v q
$sw.Stop()
Write-Info "Время тестирования: $($sw.Elapsed.ToString('mm\:ss\.fff'))"
if ($LASTEXITCODE -ne 0) {
    Write-ErrorMsg "Тесты завершились с ошибками. Код: $LASTEXITCODE"
    $global:exitCode = $LASTEXITCODE
} else {
    Write-Success "Все тесты пройдены."
}

# Открываем HTML-отчёт
if (Test-Path $reportHtml) {
    Write-Step "Открытие HTML-отчёта: $reportHtml" "Yellow"
    try {
        explorer.exe $reportHtml
        Write-Success "Отчёт открыт."
    } catch {
        Write-ErrorMsg "Не удалось открыть отчёт: $_"
        $global:exitCode = 1
    }
} else {
    Write-ErrorMsg "HTML-отчёт не найден: $reportHtml"
    $global:exitCode = 1
}

# Информация о TRX
if (Test-Path $reportTrx) {
    Write-Info "TRX-отчёт сохранён: $reportTrx"
} else {
    Write-ErrorMsg "TRX-отчёт не найден: $reportTrx"
    $global:exitCode = 1
}

if ($global:exitCode -eq 0) {
    Write-Step "✅ ВСЕ ЭТАПЫ ВЫПОЛНЕНЫ УСПЕШНО" "Green"
} else {
    Write-Step "⚠️  СКРИПТ ЗАВЕРШИЛСЯ С ОШИБКОЙ (код $global:exitCode)" "Red"
}

exit $global:exitCode