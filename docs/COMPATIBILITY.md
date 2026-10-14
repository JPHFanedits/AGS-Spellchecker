# AGS 3.6 compatibility

Version 1.1.0 keeps the same 3.6 baseline. This update was tested only in AGS 3.6.1.32 using the existing Tumbleweed game, as requested. The cross-version table below records earlier 0.2.0 checks; those checks were not repeated for 1.1.0. See RELEASE-NOTES.md for current validation.

The plugin targets all **stable AGS 3.6 release lines**: 3.6.0, 3.6.1 and 3.6.2, including their stable patches. Minimum editor version: **3.6.0.47**, the first stable 3.6.0 release. Development alphas/betas/RCs before that release and future unreleased editors are not supported by this statement.

The distributed DLL is compiled against the original 3.6.0.47 SDK, rather than a newer SDK. Its required-version attribute was lowered from 3.6.1.0 to 3.6.0.47. One DLL is used for all three release lines.

| Actual editor binaries tested | API checks | Same DLL: 209 methods JIT-compiled | Embedded icon and native panel |
| --- | --- | --- | --- |
| 3.6.0.47 | Pass | Pass | Pass |
| 3.6.1.32 | Pass | Pass | Pass |
| 3.6.2.21 | Pass | Pass | Pass |

API checks cover access to live script controls, native script replacement, project saving, editor singleton access, object/property navigation and property selection. The host API surface required by the plugin is available in all three tested versions. Tests use each editor's own assemblies and the same baseline-compiled plugin binary.

These are compatibility smoke tests; every historical patch was not individually exercised. A native AGS 3.6.1.32 integration test used the existing, author-designated Tumbleweed game: 271 player-facing lines scanned, 124 flagged lines displayed, two consecutive corrections in the same script (including escaped quotes), native saves, two exact undos, cached-row restoration and source navigation all passed. Apply and Undo left the scan generation unchanged and did not start a scan. Test corrections were undone. No new test game project was created. The local engine also passed English spelling/grammar/punctuation, French, German, Spanish, dictionary and placeholder checks. Full native game editing was tested on 3.6.1; 3.6.0 and 3.6.2 received the compatibility checks above.

Run the included checker on Windows:

```powershell
.\Tests\Test-Compatibility.ps1 -AgsPaths @('C:\Tools\AGS-3.6.0.47', 'C:\Tools\AGS-3.6.1', 'C:\Tools\AGS-3.6.2') -PluginPath '.\dist\AGS.Plugin.Spellchecker.dll'
```

For a release build, pass the original 3.6.0.47 editor directory to `Source/build.ps1 -AgsPath`. SDK binaries are not redistributed in this repository.

Official baseline: https://github.com/adventuregamestudio/ags/releases/tag/v.3.6.0.47
Official tested 3.6.2: https://github.com/adventuregamestudio/ags/releases/tag/v3.6.2.21



