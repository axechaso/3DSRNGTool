# TinyFinder integration

Source: https://github.com/Bambo-Rambo/TinyFinder

Pinned commit: `2133fe761958094ed69709aead9d7b162673e737`.

The original tracked source files, window designers, resources, location/species databases, guides, and included license notices are retained without edits. `TinyFinder.Embedded.csproj` builds the same sources as a .NET Framework 4.5.2 library.

Added integration files:

- `WildIntegration.cs` exposes the original encounter generators and databases without creating a window, and converts appended form identifiers to the host's species/form encoding.
- `EmbeddedIntegration.cs` supplies searcher input, selected-result transfer, and window/thread cleanup without replacing the original layout or generators.
- `MTIntegration.cs` supplies a direct MT seed/PID/EC/IV/horde/RTC search entry, host seed transfer, initial thread-count validation, and owned-window cleanup.

The host keeps its existing wild-search layout and results table, with an additional options group. Searchers are separate windows within the same application; native searches do not redirect to them.

Original guide destinations remain in `TinyFinder/Database/Data.cs` and the original `Guides` menu.
