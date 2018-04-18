@echo off
dotnet restore .\projects\client\RabbitMQ.Client || exit /b
dotnet build .\projects\client\RabbitMQ.Client -f netstandard1.5 || exit /b
dotnet restore .\projects\client\Unit || exit /b
dotnet build .\projects\client\Unit || exit /b
cd .\projects\client\Unit
dotnet test -f netcoreapp2.0 --filter="testcategory != requiresmp & testcategory != longrunning & testcategory != gctest"
cd ..\..\..
