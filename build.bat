@echo off
setlocal

:: Check for --publish argument
set PUBLISH_MODE=false
IF "%~1"=="--publish" set PUBLISH_MODE=true

:: Copy default config to working and output dirs
robocopy ".\defaultconfig" ".\config" /E /PURGE /NFL /NDL /NJH /NJS /NC
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

robocopy ".\defaultconfig" ".\out\bin\config" /E /PURGE /NFL /NDL /NJH /NJS /NC
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

:: Build or publish the project
IF "%PUBLISH_MODE%"=="true" (
    echo dotnet publish -c Release --output ".\out\bin" ".\Generator\generate.csproj"
    dotnet publish -c Release --output ".\out\bin" ".\Generator\generate.csproj"
    IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%
) ELSE (
    echo dotnet build -c Release --output ".\out\bin" ".\Generator\generate.csproj"
    dotnet build -c Release --output ".\out\bin" ".\Generator\generate.csproj"
    IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%
)

endlocal
