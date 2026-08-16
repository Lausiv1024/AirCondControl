@echo off

dotnet publish -c Release -r linux-arm -p:PublishReadyToRun=true -p:PublishSingleFile=true --self-contained true -p:IncludeNativeLibrariesForSelfExtract=true

pause
