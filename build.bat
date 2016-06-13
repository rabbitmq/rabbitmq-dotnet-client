@ECHO OFF
.paket\paket.bootstrapper.exe
.paket\paket.exe restore
packages\FAKE\tools\FAKE.exe build.fsx GenerateApi

dotnet restore .\projects\client\RabbitMQ.Client
dotnet build .\projects\client\RabbitMQ.Client
