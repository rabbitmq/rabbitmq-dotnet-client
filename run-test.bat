@ECHO OFF

dotnet restore .\projects\client\RabbitMQ.Client || exit /b
dotnet build .\projects\client\RabbitMQ.Client -f netstandard1.5 || exit /b
dotnet restore .\projects\client\Unit || exit /b
dotnet build .\projects\client\Unit || exit /b
CD .\projects\client\Unit
dotnet test -f netcoreapp2.0 --where="cat != RequireSMP & cat != LongRunning & cat != GCTest"
CD ..\..\..
