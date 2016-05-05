@ECHO OFF
IF %0 == "" GOTO End
.paket\paket.bootstrapper.exe
.paket\paket.exe restore
packages\FAKE\tools\FAKE.exe build.fsx %0

:End
ECHO "no target specified"
