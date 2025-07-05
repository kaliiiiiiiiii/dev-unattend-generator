robocopy "./defaultconfig" "./config" /E /PURGE
robocopy "./defaultconfig" "./out/bin/config" /E /PURGE
dotnet build -c Release "./Generator/generate.csproj"
robocopy "./Generator/bin/Release/net9.0" "./out/bin" /E