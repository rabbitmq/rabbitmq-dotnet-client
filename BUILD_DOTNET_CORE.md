To build the dotnet core components you need the `dotnet` command line utility installed.
It can be obtained for your chosen platform [here](https://www.microsoft.com/net/core#macosx).

 * Run `./fake.sh GenerateApi` (linux/osx) or `fake.bat GenerateApi` (windows) to trigger code generation and complete a standard mono/.NET build.
 * cd into the `projects/client/RabbitMQ.Client` directory
 * Run `dotnet restore`
 * Run `dotnet build`
 * To run the tests cd into `projects/client/Unit.Runner`
 * Run `dotnet restore`
 * Run `dotnet run`. This will run the entire test suite. There is currently no way to only run a subset of tests. 
 If you get a warning saying `rabbitmqctl` cannot be found please set the `RABBITMQ_RABBITMQCLT_PATH` environment variable to point to where `rabbitmqctl` can be located.
