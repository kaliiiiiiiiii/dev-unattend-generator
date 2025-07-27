robocopy "./defaultconfig" "./config" /E /PURGE /NJH /NJS
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

robocopy "./defaultconfig" "./out/bin/config" /E /PURGE /NJH /NJS
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%

dotnet build -c Release "./Generator/generate.csproj"
IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%

robocopy "./Generator/bin/Release/net9.0" "./out/bin" /E /NJH /NJS /XD "config"
IF %ERRORLEVEL% GTR 3 EXIT /B %ERRORLEVEL%