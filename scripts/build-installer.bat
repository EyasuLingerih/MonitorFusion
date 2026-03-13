@echo off
setlocal

:: ============================================================
::  MonitorFusion — Publish + Installer build script
::  Requirements:
::    - .NET 8 SDK  (dotnet in PATH)
::    - InnoSetup 6 (default install location checked below)
:: ============================================================

set PROJECT=src\MonitorFusion.App\MonitorFusion.App.csproj
set PUBLISH_DIR=publish
set INSTALLER_SCRIPT=installer\MonitorFusion.iss
set INNO_COMPILER="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo.
echo  ── Step 1: Clean previous publish output ──────────────────
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

echo.
echo  ── Step 2: Publish self-contained single-file app ─────────
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishReadyToRun=true ^
  -o "%PUBLISH_DIR%"

if errorlevel 1 (
  echo.
  echo  ERROR: dotnet publish failed.
  exit /b 1
)

echo.
echo  ── Step 3: Compile InnoSetup installer ────────────────────
if not exist %INNO_COMPILER% (
  echo.
  echo  WARNING: InnoSetup not found at %INNO_COMPILER%
  echo  Download from https://jrsoftware.org/isinfo.php
  echo  Skipping installer build.
  goto :done
)

%INNO_COMPILER% "%INSTALLER_SCRIPT%"

if errorlevel 1 (
  echo.
  echo  ERROR: InnoSetup compilation failed.
  exit /b 1
)

:done
echo.
echo  ── Done! ──────────────────────────────────────────────────
echo  Installer output: installer\output\
echo.
endlocal
