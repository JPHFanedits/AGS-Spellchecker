# AGS Spellchecker

![AGS Spellchecker blue mug and checkmark](assets/spellchecker.png)

An **offline Adventure Game Studio editor plugin** for reviewing player-facing text in the currently open game.

## Install

1. Download and extract the plugin ZIP from this repository's Releases page.
2. Close AGS and copy `AGS.Plugin.Spellchecker.dll` beside `AGSEditor.exe`.
3. Restart AGS, open your game, and select **Spellchecker > Open AGS Spellchecker**.

This is an editor extension, not a runtime plugin. AGS discovers it automatically. Windows may require administrator permission to copy files into Program Files.

**Test release:** start with a copy of your game. Built against AGS 3.6.1 on Windows/.NET Framework. Compilation, assembly loading, icon resource loading and review-panel initialization were verified. The complete scan/edit/save/undo workflow has not yet been verified inside a loaded AGS game. Newer AGS versions are not verified.

## Features

- Offline English spelling plus rules for common grammar, capitalization and punctuation issues.
- Review flagged lines, filter by category, search and sort columns.
- Inspect source context and jump to a script/dialogue line or AGS property.
- Preview suggestions before applying a correction.
- Automatically advance after applying, ignoring or adding a dictionary word.
- Persist ignored text and dictionary words per game.
- Automatic backups, project saving and guarded undo.
- Blue mug spellcheck icon embedded in the plugin DLL.

**Applying a correction saves the entire AGS project, including other pending edits.** Backups and review state are stored in `<game>/.ags-proofreader/AGS Spellchecker/`. Undo refuses to overwrite subsequent changes to the same document.

See [the user guide](docs/USER-GUIDE.txt) for coverage, limitations, backups and uninstall instructions. Grammar rules are suggestions, not a complete linguistic analysis. Custom display wrappers, dynamically assembled strings, custom properties and translation files are not covered in this preview. No online checking, account or API key is used.

## Build

Requires Windows, .NET Framework's C# compiler, and an installed AGS 3.6.1 editor SDK (`AGS.Types.dll`). From the repository root:

```powershell
New-Item -ItemType Directory -Force dist | Out-Null
.\Source\build.ps1 -AgsPath 'C:\Program Files (x86)\Adventure Game Studio 3.6.1' -OutputPath "$PWD\dist"
```

The compiler embeds `Source/Assets/spellchecker.ico`. The editable SVG and a standalone C# icon-generation utility are included. The icon deliberately omits lettering at the smallest sizes to keep the mug and checkmark readable. AGS SDK assemblies are referenced locally and are not redistributed.

## Publish this package on GitHub

1. Extract the source ZIP and upload the **contents** of the `AGS-Spellchecker` directory to your repository, or commit and push those files with Git.
2. Create a release marked **Pre-release** and attach `AGS-Spellchecker-plugin.zip` from the separate release package.
3. Copy the text from `RELEASE-NOTES.md` into the release description.
4. Choose a license before inviting reuse or contributions; this package does not select a license on the author's behalf.

The source package contains no game files, game text, review history, local backups, AGS SDK binaries or testing copies.

## Attribution

Adventure Game Studio and Microsoft Word are referenced descriptively. This is an independent plugin, not an official release from either project. The mug/checkmark artwork was drawn for this plugin and does not embed their application assets.

[Official AGS editor-plugin documentation](https://adventuregamestudio.github.io/ags-manual/v3/EditorPlugins.html)
