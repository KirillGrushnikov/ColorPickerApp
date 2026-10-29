@echo off
rem EXPERIMENTAL: Native AOT build - fastest startup (no JIT, no .NET runtime files).
rem Requires Visual Studio workload "Desktop development with C++".
cd /d "%~dp0"
if exist publish-aot rmdir /s /q publish-aot
dotnet publish -c Release -r win-x64 -p:PublishAot=true -p:PublishReadyToRun=false -o ./publish-aot
pause
