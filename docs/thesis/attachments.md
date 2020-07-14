# Attachments


| File name | Description |
|---------------|-----------------------|
| `src/` | Source code of the Coberec library |
| `deps/` | Cloned dependencies of Coberec -- ILSpy, GraphQL parser and the Big List of Naughty Strings |
| `docs/` and `README.md` | Project documentation in Markdown format |

The project may be also found on GitHub: \url{https://github.com/exyi/coberec}.

## Build Instructions

In order to compile and run the attached source code, .NET Core SDK is needed. The easiest way to break the dependency cycle is to download the `Coberec.CLI` package:

```
dotnet tool install -g Coberec.CLI
```

To get more up to date source codes, instead of using the attachments, it should be possible to clone our repository.

```
git clone git@github.com:exyi/coberec.git --recursive
```

The `dotnet test` command will run the unit tests.
For trying out the Expression API, we can recommend to add a new test case into the `Coberec.ExprCS.Tests` project.
To use the API in another project, a reference must be added to the MSBuild file (`.csproj`, `.fsproj`, ...):

```xml
<ProjectReference
    Include="./src/Coberec.ExprCS/Coberec.ExprCS.csproj" />
```
