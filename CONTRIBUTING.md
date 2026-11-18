# Contributing

Issues and pull requests are welcome on GitHub: https://github.com/rimhunter/factional-war

- Report a bug or a mod conflict as an issue. Say which RimWorld version (1.6 or 1.7) and which other mods you run, and attach `Player.log` if the game logged an error.
- Pull requests are welcome. Development happens in a separate repository where the build and the in-game test suite run; an accepted pull request is copied there with you kept as the commit author, and it reaches GitHub with the next release.
- The mod builds from this repository with the .NET SDK (`dotnet build Source/ModRimWorldFactionalWar.csproj -p:GameVersion=1.7` produces the 1.7 load folder, and `-p:GameVersion=1.6` the 1.6 one; `Build.proj` builds both and also needs the private runner), and `dotnet test --project Tests/FactionalWar.Tests` runs the unit tests. The in-game scenarios in `TestRunner/` need the shared test runner, which is a private dependency and is not part of this repository. Outside contributors can build the mod and run the unit tests, but can run the scenarios only if they are given access to the runner; the maintainers run them on every accepted change.
- Each change is listed with its date in `CHANGELOG.md`, as the Apache License 2.0 asks for a modified version. A pull request adds its own line there.
- By contributing you agree that your change is licensed under the same terms as the rest of the mod: the Apache License 2.0 (see `LICENSE`, `NOTICE` and `MODIFICATIONS`).
