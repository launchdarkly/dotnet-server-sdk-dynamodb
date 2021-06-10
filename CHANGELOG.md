# Change log

All notable changes to the LaunchDarkly .NET SDK DynamoDB integration will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

## [2.0.0] - 2021-06-09
This release is for use with versions 6.0.0 and higher of [`LaunchDarkly.ServerSdk`](https://github.com/launchdarkly/dotnet-server-sdk).

For more information about changes in the SDK database integrations, see the [5.x to 6.0 migration guide](https://docs-stg.launchdarkly.com/252/sdk/server-side/dotnet/migration-5-to-6).

### Changed:
- The namespace is now `LaunchDarkly.Sdk.Server.Integrations`.
- The entry point is now `LaunchDarkly.Sdk.Server.Integrations.DynamoDB` rather than `LaunchDarkly.Client.Integrations.DynamoDB` (or, in earlier versions, `LaunchDarkly.Client.DynamoDB.DynamoDBComponents`).
- If you pass in an existing DynamoDB client instance with `DynamoDBDataStoreBuilder.ExistingClient`, the SDK will no longer dispose of the client on shutdown; you are responsible for its lifecycle.
- The logger name is now `LaunchDarkly.Sdk.DataStore.DynamoDB` rather than `LaunchDarkly.Client.DynamoDB.DynamoDBFeatureStoreCore`.

### Removed:
- Removed the deprecated `DynamoDBComponents` entry point and `DynamoDBFeatureStoreBuilder`.
- The package no longer has a dependency on `Common.Logging` but instead integrates with the SDK&#39;s logging mechanism.

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
