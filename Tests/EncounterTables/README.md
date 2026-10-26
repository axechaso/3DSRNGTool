# Encounter table regression checks

These checks run against the .NET Framework 4.5.2 application without a test framework or additional runtime dependencies.

The USUM fixtures cover both game versions and both times of day:

- Akala Outskirts: Natu's daytime slot becomes Wingull at night.
- Route 11: the first slot is Komala, not Gumshoos/Raticate.
- Poni Meadow: the final 10% is Cottonee in Ultra Sun or Petilil in Ultra Moon. Only the Misdreavus slot changes to Ribombee during the day.
- Mount Hokulani, Mount Lanakila and Lush Jungle retain their special time/version behavior, including Lush Jungle's final nighttime Trumbeak slot.
- Melemele Meadow, Route 6 and Poni Meadow use their local Oricorio forms; flower colors and East Sea Gastrodon are retained in the table and generated result. Static must attract Melemele's electric Pom-Pom Oricorio, and must not attract the psychic or ghost forms elsewhere.
- Ordinary Flabebe/Floette SOS encounters retain white/orange/caller-color slots. Ordinary Floette colors use the base SOS call rate; cosmetic personal entries containing zero must not disable calling.
- USUM Route 2 south is level 6–9, Hano Beach is 22–25, and Malie Garden is 25–28. Melemele Meadow is 9–12 in Ultra Sun and 10–12 in Ultra Moon, so its minimum and maximum cannot both be shifted by one.

Expected species and probabilities were checked against SciresM's extracted [Ultra Sun](https://gist.github.com/SciresM/a539739085e24af55dffdf443cb70eb2) and [Ultra Moon](https://gist.github.com/SciresM/deecdcf5fc49fc8191a29d111643c6b6) encounter data: maps 097/098 table 3, map 165 table 1, and map 273 table 1. Slot ordering was also cross-checked with the [Ultra Sun slot archive](https://shinyfinder.github.io/encounter-slots/ultra-sun/Land.html) and [Ultra Moon slot archive](https://shinyfinder.github.io/encounter-slots/ultra-moon/Land.html); the extracted data takes precedence for time-specific species and probabilities.

`USUM-normal-encounters.json` pins all 91 normal encounter areas to the extracted map/table IDs, for both versions and both times of day (364 cases). It independently specifies species, forms, total probabilities and level ranges. Tests run all 100 slot rolls and compare their species/form frequencies with those expectations. A WinForms integration check verifies that selecting the location loads the right version's levels and passes form-aware slots to the main generator.

Pinned source revisions: Ultra Sun `e9f9a551a7ad34a66f6812aa77e526cd21ee79f3`, Ultra Moon `5e146ae32a61b7f5baa8519120d6b948f62617a5`.

The explicit slot fixtures also exercise `Wild7.Generate()` with 128 deterministic RNG positions. All normal SM/USUM tables are checked for valid slots, source-table immutability and correct day/night refresh flags. Fixed forms consume no additional RNG; Minior retains its random-form branch.

The archived slot pages contain conflicting entries, including Brooklet Hill's water slots and mislabeled surfing areas. SciresM's 2017 dump groups repeated species, sorts by total probability, and reverses ties, as shown in [the corresponding pk3DS exporter](https://github.com/kwsch/pk3DS/blob/d78b95e7e20e3a9733ba8b59aa1e6d0cbdaf229b/pk3DS/Subforms/Gen7/Wild/EncounterTable.cs#L93). Its printed list is not raw slot order. The complete fixture check therefore verifies summed probabilities and forms; it does not claim to recover every raw slot position from those summaries. The existing Poni Plains area 4 slot order and rates are preserved.

To regenerate expectations, save the two linked SciresM text dumps locally and run:

```powershell
python Tests/EncounterTables/update_fixtures.py ultra-sun.txt ultra-moon.txt
```

This reads only the saved game-data summaries and the fixture's pinned map/table bindings. It never copies expected values from the application. The normal build and tests need no Python or network access. This audit covers normal encounters and the listed flower SOS cases; it is not a complete audit of all fishing, ambush or SOS caller-slot variants, and no hardware RNG validation has been performed.

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
