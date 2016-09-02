rem uninstall existing service
sc.exe \\krr-app-palbp01 Delete "ArcelorMittal.OPCCommandsSender"
rem copy new version
xcopy %WORKSPACE%\PrintLabelService\bin\Production\*.* \\krr-app-palbp01\Nikama\opc_isa_service /Y
rem install existing service
rem echo off
sc.exe \\krr-app-palbp01 Create "ArcelorMittal.OPCCommandsSender" binPath="C:\Nikama\opc_isa_service\PrintLabelService.exe" start=demand obj=%PRINT_USER% password=%PRINT_PASS%
