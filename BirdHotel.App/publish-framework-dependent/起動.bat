@echo off
cd /d "%~dp0"
dotnet BirdHotelReservation.dll
if errorlevel 1 pause
