# Third-party notices

KD.Avalonia.Rice's own source code is licensed under the MIT license in LICENSE.
External assets, dependencies and specification examples retain the terms below.
Including their notices does not relicense those components or imply endorsement.

## Library and package assets

| Component | Source | License / local copy |
| --- | --- | --- |
| Avalonia and Fluent theme, 12.1.3 | https://github.com/AvaloniaUI/Avalonia/tree/8eeda4f6f546165b3f72e63c9f42247abb306905 | MIT: licenses/Avalonia-MIT.txt; upstream component notices: licenses/Avalonia-NOTICE.txt |
| Material.Icons and Material.Icons.Avalonia, 3.0.2 | https://github.com/SKProCH/Material.Icons/tree/1d6a5fe9b15d7489212f79e1d16d05e61b7166ad | Wrapper code: licenses/Material.Icons-MIT.txt; icon collection: licenses/Pictogrammers-LICENSE.txt and licenses/Apache-2.0.txt |
| MicroCom.Runtime, 0.11.6 | https://github.com/kekekeks/MicroCom/tree/76785efcafd91b5902fd19dd11145f6dd655b7b4 | licenses/MicroCom-MIT.txt |
| Package icon: src/KD.Avalonia.Rice/Assets/icon.png | https://www.iconarchive.com/show/material-icons-by-pictogrammers/linux-icon.html | Pictogrammers Team, Apache-2.0: licenses/Apache-2.0.txt |

The package icon is a project-specific modified composition based on the
Pictogrammers Linux icon, rather than the unmodified upstream icon. The existing
repository records this source; the exact historical editing steps were not
recorded. The notice applies to the package icon, not the new demo illustration.

### Theme palette acknowledgements

The project's palette selections and light/dark adaptations draw on these color
schemes. Their upstream license texts are retained alongside the adapted palettes:

| Palette | Source | License copy |
| --- | --- | --- |
| Nord — Sven Greb | https://github.com/nordtheme/nord | licenses/Nord-MIT.txt |
| Solarized — Ethan Schoonover | https://github.com/altercation/solarized | licenses/Solarized-MIT.txt |
| Dracula — Dracula Theme | https://github.com/dracula/dracula-theme | licenses/Dracula-MIT.txt |
| Gruvbox — morhetz | https://github.com/morhetz/gruvbox | licenses/Gruvbox-MIT.txt |
| Catppuccin | https://github.com/catppuccin/palette | licenses/Catppuccin-MIT.txt |

The distribution-inspired theme names identify the palette inspiration. No
affiliation or endorsement by the named projects is claimed.

## Demo distributions

The demo additionally uses the components below. Its build and publish output
includes this file, LICENSE, and licenses/, including the demo subdirectory.

| Component | Source | License / notice copies |
| --- | --- | --- |
| Inter fonts, through Avalonia.Fonts.Inter 12.1.3 | https://github.com/rsms/inter | licenses/demo/Inter-OFL-1.1.txt; the OFL applies to the font, separately from Avalonia's MIT wrapper |
| SkiaSharp 3.119.4 and platform native assets | https://github.com/mono/SkiaSharp | licenses/demo/SkiaSharp-LICENSE.txt and licenses/demo/SkiaSharp-HarfBuzzSharp-THIRD-PARTY-NOTICES.txt |
| HarfBuzzSharp 8.3.1.3 and platform native assets | https://github.com/mono/SkiaSharp | licenses/demo/HarfBuzzSharp-LICENSE.txt and the shared notices above |
| ANGLE native assets 2.1.27548.20260419 | https://github.com/AvaloniaUI/angle/tree/1c89805903c1482166356d3b950d474973180e61 | licenses/demo/ANGLE-LICENSE.txt |
| Tmds.DBus.Protocol 0.94.1 | https://github.com/tmds/Tmds.DBus/tree/b4a7fed0b878f74cb54f7cca84d2889af4e596ba | licenses/demo/Tmds.DBus-MIT.txt |

The new demo rice-bowl illustration was generated for this project on
2026-10-16 with OpenAI's built-in image generation tool, using no reference image.
It replaces the previous demo icon of unknown origin. See
samples/KD.Avalonia.Rice.Demo/Assets/icon.provenance.md for the prompt and scope.
To the extent the project contributors hold rights in this illustration, they
make it available under the repository's MIT license. This provenance record is
not a claim of exclusive copyright in purely machine-generated material.

## Tests only: CommonMark / GitHub Flavored Markdown examples

The adapted specification examples are isolated in
`tests/KD.Avalonia.Rice.Tests/TestData/*-examples.json`, under CC-BY-SA-4.0.
See that directory's README.md and LICENSE.txt for sources, authors and changes.
They are embedded only in the test assembly, not in the library NuGet package or
the demo. The library and C# test runner retain their MIT license.

The test/build tooling is referenced through NuGet, not bundled as library code:
xUnit and its Visual Studio runner declare Apache-2.0; Microsoft.NET.Test.Sdk and
Nerdbank.GitVersioning declare MIT. Their dependencies retain their own licenses.

## Maintenance

When adding copied code/assets or changing dependencies, record the original
source, version, author, license and changes, and update the applicable license
copies. Check both NuGet contents and demo publish output before release. Keep
notices from native dependencies when producing platform-specific installers.
The library package references external dependencies rather than bundling them;
applications redistributing those dependencies must retain their notices too.
