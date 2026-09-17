# Bundled runtime libraries

Downloaded from the official NuGet flat-container registry. Build scripts restore the pinned archives into `vendor/` and verify SHA-256 hashes from `scripts/packages.json`. Redistributed license and attribution files are included in `licenses/`.

| Library | Version | License |
| --- | --- | --- |
| SVG.NET | 3.4.7 | MS-PL |
| ExCSS | 4.2.3 | MIT |
| System.Memory | 4.5.5 | MIT |
| System.Buffers | 4.5.1 | MIT |
| System.Runtime.CompilerServices.Unsafe | 4.5.3 | MIT |
| System.Numerics.Vectors | 4.5.0 | MIT |

The runtime DLL files beside MiniDeck.exe must accompany it when copied to another PC. .NET Framework 4.8 or newer is required by the bundled SVG dependency.

Bundled fonts: Pretendard 1.3.9 (https://github.com/orioncactus/pretendard) and Inter 4.1 (https://github.com/rsms/inter), both under SIL Open Font License 1.1. License and copyright notices are included in fonts/LICENSE.txt and fonts/Inter-LICENSE.txt. Fonts are registered privately for this process, not installed system-wide. Copy the fonts directory with the application.
