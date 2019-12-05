@echo off
cd .\projects\client\Unit
dotnet test -f netcoreapp2.1 --filter="testcategory != requiresmp & testcategory != longrunning & testcategory != gctest"
cd ..\..\..
