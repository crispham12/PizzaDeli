@echo off
taskkill /IM PizzaDeli.exe /F 2>nul
taskkill /IM dotnet.exe /F 2>nul
echo Server stopped.
