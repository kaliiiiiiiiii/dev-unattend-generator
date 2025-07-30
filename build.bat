@echo off
robocopy ".\defaultconfig" ".\config" /E /PURGE /NFL /NDL /NJH /NJS /NC
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

robocopy ".\defaultconfig" ".\out\bin\config" /E /PURGE /NFL /NDL /NJH /NJS /NC
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

echo dotnet build -c Release ".\Generator\generate.csproj"
dotnet build -c Release ".\Generator\generate.csproj"
IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%

robocopy ".\Generator\bin\Release\net9.0" ".\out\bin" /E /PURGE /NFL /NDL /NJH /NJS /NC /XD "config"
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%