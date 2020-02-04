@echo off
dotnet test --no-build --logger "console;verbosity=detailed" ./RabbitMQDotNetClient.sln
