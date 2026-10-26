# Encounter table regression checks

These checks run against the .NET Framework 4.5.2 application without a test framework or additional runtime dependencies.

The USUM fixtures cover both game versions and both times of day:

- Akala Outskirts: Natu's daytime slot becomes Wingull at night.
- Route 11: the first slot is Komala, not Gumshoos/Raticate.
- Poni Meadow: the final 10% is Cottonee in Ultra Sun or Petilil in Ultra Moon. Only the Misdreavus slot changes to Ribombee during the day.
- Mount Hokulani, Mount Lanakila and Lush Jungle retain their special time/version behavior, including Lush Jungle's final nighttime Trumbeak slot.

Expected species and probabilities were checked against SciresM's extracted [Ultra Sun](https://gist.github.com/SciresM/a539739085e24af55dffdf443cb70eb2) and [Ultra Moon](https://gist.github.com/SciresM/deecdcf5fc49fc8191a29d111643c6b6) encounter data: maps 097/098 table 3, map 165 table 1, and map 273 table 1. Slot ordering was also cross-checked with the [Ultra Sun slot archive](https://shinyfinder.github.io/encounter-slots/ultra-sun/Land.html) and [Ultra Moon slot archive](https://shinyfinder.github.io/encounter-slots/ultra-moon/Land.html); the extracted data takes precedence for time-specific species and probabilities.

Each fixture also exercises `Wild7.Generate()` with 128 deterministic RNG positions. All normal SM/USUM tables are checked for valid slots, source-table immutability and correct day/night refresh flags.

From a Visual Studio developer shell with the .NET Framework 4.5.2 targeting pack:

```powershell
msbuild 3DSRNGTool.sln /p:Configuration=Release /verbosity:minimal
.\Tests\EncounterTables\bin\Release\EncounterTables.Tests.exe
```

On a machine without that targeting pack, install the reference assemblies locally and pass their root to MSBuild, as the CI workflow does:

```powershell
nuget install Microsoft.NETFramework.ReferenceAssemblies.net452 -Version 1.0.3 -OutputDirectory packages -NonInteractive
msbuild 3DSRNGTool.sln /p:Configuration=Release "/p:TargetFrameworkRootPath=$PWD\packages\Microsoft.NETFramework.ReferenceAssemblies.net452.1.0.3\build" /verbosity:minimal
.\Tests\EncounterTables\bin\Release\EncounterTables.Tests.exe
```
