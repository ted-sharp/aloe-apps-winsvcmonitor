@echo off

cd /d %~dp0

REM Completely delete publish folder (Warning: all contents will be deleted)
rmdir /s /q publish

REM Publish WindowsServiceMonitor related projects

echo Publishing WindowsServiceMonitorServer...
dotnet publish .\Aloe\Apps\WindowsServiceMonitor\Aloe.Apps.WindowsServiceMonitorServer\Aloe.Apps.WindowsServiceMonitorServer.csproj -c Release -r win-x64 --self-contained true -o .\publish\WindowsServiceMonitorServer

echo Publishing WindowsServiceMonitorClient...
dotnet publish .\Aloe\Apps\WindowsServiceMonitor\Aloe.Apps.WindowsServiceMonitorClient\Aloe.Apps.WindowsServiceMonitorClient.csproj -c Release -r win-x64 --self-contained true -o .\publish\WindowsServiceMonitorClient

echo Publishing DummyService...
dotnet publish .\Aloe\Apps\WindowsServiceMonitor\Aloe.Apps.DummyService\Aloe.Apps.DummyService.csproj -c Release -r win-x64 --self-contained true -o .\publish\DummyService

echo.
echo Completed.
pause
