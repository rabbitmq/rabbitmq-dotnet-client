@ECHO OFF
IF %1 == "" GOTO End
.paket\paket.bootstrapper.exe
.paket\paket.exe restore
packages\FAKE\tools\FAKE.exe build.fsx %1

:End
ECHO "no target specified"
