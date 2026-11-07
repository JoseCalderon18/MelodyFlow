# Local secrets in launch profiles

For this local development setup, keep secrets in each host's ignored `Properties/launchSettings.json` under the profile's `environmentVariables` section. Use `ConnectionStrings__MelodyFlow` for PostgreSQL in both `MelodyFlowAPI` and `MelodyFlow.Worker`. The API and Worker are separate processes, so each needs its own value. Do not add credentials to tracked `appsettings*.json`, source code, or documentation.

`launchSettings.json` is ignored by Git and is not included in clones. After cloning, create local profile files from the .NET launch settings template or restore them from a secure local backup, then add the required values. These profiles are for local development only; production secrets belong in the deployment environment's secret store.
