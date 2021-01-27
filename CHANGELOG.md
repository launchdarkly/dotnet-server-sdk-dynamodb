# Change log

All notable changes to the LaunchDarkly .NET SDK DynamoDB integration will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

## [1.1.0] - 2021-01-26
### Added:
- New classes `LaunchDarkly.Client.Integrations.DynamoDB` and `LaunchDarkly.Client.Integrations.DynamoDBStoreBuilder`, which serve the same purpose as the previous classes but are designed to work with the newer persistent data store API introduced in .NET SDK 5.14.0.

### Deprecated:
- The old API in the `LaunchDarkly.Client.DynamoDB` namespace.

## [1.0.1] - 2019-05-10
### Changed:
- Corresponding to the SDK package name change from `LaunchDarkly.Client` to `LaunchDarkly.ServerSdk`, this package is now called `LaunchDarkly.ServerSdk.DynamoDB`. The functionality of the package, including the namespaces and class names, has not changed.

## [1.0.0] - 2019-01-11

Initial release.
