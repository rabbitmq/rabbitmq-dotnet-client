To build the dotnet core components you need the `dotnet` command line utility installed.
It can be obtained for your chosen platform [here](https://www.microsoft.com/net/core#macosx).

 Run `./build.sh` (linux/osx) or `build.bat` (windows) to trigger code generation and complete a dotnet core build. 

 To run the tests simply run `run-tests.sh` (linux/osx) or `run-tests.bat` (windows). This will run the entire test suite. There is currently no way to only run a subset of tests. 

 If you get a warning saying `rabbitmqctl` cannot be found please set the `RABBITMQ_RABBITMQCLT_PATH` environment variable to point to where `rabbitmqctl` can be located.
