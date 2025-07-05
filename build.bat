robocopy "./defaultconfig" "./config" /E /PURGE /NJH /NJS
IF %ERRORLEVEL% GTR 2 EXIT /B %ERRORLEVEL%

robocopy "./defaultconfig" "./out/bin/config" /E /PURGE /NJH /NJS
IF %ERRORLEVEL% GTR 2 EXIT /B %ERRORLEVEL%

dotnet build -c Release "./Generator/generate.csproj"

robocopy "./Generator/bin/Release/net9.0" "./out/bin" /E /NJH /NJS /XD "config"
IF %ERRORLEVEL% GTR 2 EXIT /B %ERRORLEVEL%