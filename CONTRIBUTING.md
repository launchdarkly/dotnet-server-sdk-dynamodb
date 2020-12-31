# Contributing to this library

The source code for this library is [here](https://github.com/launchdarkly/dotnet-server-sdk-dynamodb). We encourage pull-requests and other contributions from the community. Since this library is meant to be used in conjunction with the LaunchDarkly .NET SDK, you may want to look at the [.NET SDK source code](https://github.com/launchdarkly/dotnet-server-sdk) and our [SDK contributor's guide](http://docs.launchdarkly.com/docs/sdk-contributors-guide).

## Submitting bug reports and feature requests
 
The LaunchDarkly SDK team monitors the [issue tracker](https://github.com/launchdarkly/dotnet-server-sdk-dynamodb/issues) in this repository. Bug reports and feature requests specific to this project should be filed in the issue tracker. The SDK team will respond to all newly filed issues within two business days.
 
## Submitting pull requests
 
We encourage pull requests and other contributions from the community. Before submitting pull requests, ensure that all temporary or unintended code is removed. Don't worry about adding reviewers to the pull request; the LaunchDarkly SDK team will add themselves. The SDK team will acknowledge all pull requests within two business days.
 
## Build instructions
 
### Prerequisites

This project has two targets: .NET Standard 2.0 and .NET Framework 4.5.2. In Windows, you can build both; outside of Windows, you will need to [download .NET Core and follow the instructions](https://dotnet.microsoft.com/download) (make sure you have 2.0 or higher) and can only build the .NET Standard target.

The unit test project uses code from the `dotnet-server-sdk-shared-tests` repository which is imported as a subtree. See the `README.md` file in that directory for more information.

### Building

To install all required packages:

```
dotnet restore
```

To build all targets of the project without running any tests:

```
dotnet build src/LaunchDarkly.ServerSdk.DynamoDB
```

Or, to build only the .NET Standard 2.0 target:

```
dotnet build src/LaunchDarkly.ServerSdk.DynamoDB -f netstandard2.0
```

### Testing

To run all unit tests, for all targets:

```
dotnet test test/LaunchDarkly.ServerSdk.DynamoDB.Tests
```

Or, to run tests only for the .NET Standard 2.0 target (using the .NET Core 2.1 runtime):

```
dotnet test test/LaunchDarkly.ServerSdk.DynamoDB.Tests -f netcoreapp2.1
```

The tests expect you to have a local DynamoDB server running on port 8000. One way to do this is with Docker:

```bash
docker run -p 8000:8000 amazon/dynamodb-local
```
