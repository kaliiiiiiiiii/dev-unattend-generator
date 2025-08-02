@echo off
setlocal

rem https://superuser.com/a/498798/1774848
rem Bypass "Terminate Batch Job" prompt.
if "%~1"=="-FIXED_CTRL_C" (
   REM Remove the -FIXED_CTRL_C parameter
   SHIFT
) ELSE (
   REM Run the batch with <NUL and -FIXED_CTRL_C
   CALL <NUL %0 -FIXED_CTRL_C %*
   GOTO :EOF
)

:: Check for --publish argument
set PUBLISH_MODE=false
IF "%~1"=="--publish" set PUBLISH_MODE=true

IF EXIST "./out/bin" (
        rmdir /S /Q "./out/bin
)

:: Copy default config to working and output dirs
robocopy ".\defaultconfig" ".\config" /E /PURGE /NFL /NDL /NJH /NJS /NC
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

robocopy ".\defaultconfig" ".\out\bin\config" /E /PURGE /NFL /NDL /NJH /NJS /NC
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

:: Build or publish the project
IF "%PUBLISH_MODE%"=="true" (
    echo dotnet publish -c Release --output ".\out\bin" ".\WinDevGen\WinDevGen.csproj"
    dotnet publish -c Release --output ".\out\bin" ".\WinDevGen\WinDevGen.csproj"
    IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%
) ELSE (
    echo dotnet build -c Release --output ".\out\bin" ".\WinDevGen\WinDevGen.csproj"
    dotnet build -c Release --output ".\out\bin" ".\WinDevGen\WinDevGen.csproj"
    IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%
)

endlocal
