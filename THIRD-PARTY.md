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

## Search and translation service icons

The service-identification SVGs are embedded in MiniDeck.exe and rendered locally. Logos and trademarks belong to their respective owners; their inclusion does not imply endorsement. Collection license notices are bundled in `licenses/`.

| Asset | Source | Collection license / adaptation |
| --- | --- | --- |
| Google Search | [gilbarbara/logos, google-icon.svg](https://github.com/gilbarbara/logos/blob/a5b65275e761a8347a99eded1101c6b130a06e52/logos/google-icon.svg) | CC0; unchanged |
| Bing | [gilbarbara/logos, bing.svg](https://github.com/gilbarbara/logos/blob/a5b65275e761a8347a99eded1101c6b130a06e52/logos/bing.svg) | CC0; unchanged |
| Google Translate | [homarr-labs/dashboard-icons](https://github.com/homarr-labs/dashboard-icons/blob/36c0d7fb93374b79c1247a80249c6dda36476411/svg/google-translate.svg) | Apache-2.0 collection; unchanged |
| Naver | [Simple Icons, naver.svg](https://github.com/simple-icons/simple-icons/blob/f2365d33171bd1897a41aaae6c0b6e795bcc0483/icons/naver.svg) | CC0; set fill to Naver green `#03C75A` |

The generic magnifying-glass SVG for other engines was drawn for MiniDeck. Exact download URLs are recorded in `assets/engines/sources.json` in the source checkout.
