rem uninstall existing service
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /u "C:\Nikama\opc_isa_service\KEPSSenderService.exe"
rem copy new version
xcopy %WORKSPACE%\KEPSSenderService\bin\Release\*.* C:\Nikama\opc_isa_service /Y
rem install existing service
echo off
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /username=%PRINT_USER% /password=%PRINT_PASS% /unattended "C:\Nikama\opc_isa_service\KEPSSenderService.exe"
echo on
rem start service
net start "ArcelorMittal.KEPServerSender"