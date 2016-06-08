To build the dotnet core components you need the `dotnet` command line utility installed.
It can be obtained for your chosen platform [here](https://www.microsoft.com/net/core#macosx).

* First run build.sh (linux/osx) or build.bat (windows) to trigger code generation and complete a standard mono/.NET build.
* cd into the `projects/client/RabbitMQ.Client` directory and run `dotnet build`. 
* To run the tests cd into `projects/client/Unit.Runner` and run `dotnet run`. This will run the entire test suite. There is currently no way to only run a subset of tests.
