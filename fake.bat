@ECHO OFF
IF %1 == "" GOTO End
.paket\paket.bootstrapper.exe
.paket\paket.exe restore
packages\FAKE\tools\FAKE.exe build.fsx %*

:End
ECHO "no target specified"
