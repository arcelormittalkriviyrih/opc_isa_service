rem uninstall existing service
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /u "C:\Nikama\opc_isa_service\KEPSSenderService.exe"
rem copy new version
xcopy %WORKSPACE%\KEPSSenderService\bin\Release\*.* C:\Nikama\opc_isa_service /Y
rem install existing service
echo off
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /username=%ADMIN_USER% /password=%ADMIN_PASS% /unattended "C:\Nikama\opc_isa_service\KEPSSenderService.exe"
echo on
rem first run with administrator privileges
net start "ArcelorMittal.OPCCommandsSender"
net stop "ArcelorMittal.OPCCommandsSender"
sc.exe config "ArcelorMittal.OPCCommandsSender" obj=%PRINT_USER% password=%PRINT_PASS%
rem configure delayed service
sc.exe config "ArcelorMittal.OPCCommandsSender" start=delayed-auto
rem net stop "ArcelorMittal.OPCCommandsSender"
