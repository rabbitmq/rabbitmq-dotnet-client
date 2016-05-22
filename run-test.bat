@ECHO OFF
.paket\paket.bootstrapper.exe
.paket\paket.exe restore
packages\FAKE\tools\FAKE.exe build.fsx Test
