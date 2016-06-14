@ECHO OFF

dotnet restore .\projects\client\RabbitMQ.Client || exit /b
dotnet build .\projects\client\RabbitMQ.Client || exit /b
dotnet restore .\projects\client\Unit || exit /b
dotnet build .\projects\client\Unit || exit /b
CD .\projects\client\Unit 
dotnet test -f netcoreapp1.0 --where="cat != RequireSMP & cat != LongRunning & cat != GCTest"
CD ..\..\.. 
