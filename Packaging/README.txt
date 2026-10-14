AGS Spellchecker 1.2.1

INSTALL
Close AGS and run AGS-Spellchecker-Setup.exe. Select your detected AGS editor,
or uncheck it to choose another AGSEditor.exe on the same page.
Restart AGS and open AGS Spellchecker in the project tree.
Windows 10/11 x64; stable AGS 3.6.0, 3.6.1 or 3.6.2 (minimum 3.6.0.47).
Setup includes LanguageTool and private Java. Checking works offline.

Manual installation: extract AGS-Spellchecker-Portable.zip and copy its DLL
and complete Runtime folder beside AGSEditor.exe. Keep the included notices.

LANGUAGES AND BULK REVIEW
Choose Check language, then Scan. Automatic language guessing is removed.
Unassigned lines appear in every language. Use Ctrl/Shift to select rows,
choose a Set language value, and click Assign selected. Assigned lines appear
only in that language family (regional variants use their own checking rules).
Choose Unassigned to remove a label. Assignments survive scans and corrections.
Ignore selected accepts every issue on the selected lines, across languages.
The ordinary Ignore button still accepts only one issue. Both are remembered.
Only unchecked / unresolved hides completed and ignored rows.
Changing languages never starts a scan. Needs scan rows await an explicit scan.

PROJECT TRANSLATIONS
Add translations in AGS's Explore Project tree. In Show text in, select
Translations or a specific Translation: filename.trs, choose Check language,
then press Scan. Select All player-facing text to return to game text.
Scan checks the selected category. Switching categories does not start a scan.
Only nonblank translated entries are checked. Original entries, comments,
empty translations, encoding and line endings are preserved. Corrections
back up and save the translation source and refresh AGS's translation data.
Save pending translation edits in AGS first. Recompile translations in AGS
to include corrections in the game. Go to source opens the file in Notepad;
the plugin's context panel shows the selected line and surrounding text.

RESIZABLE PANELS
Drag the dividers between the main list and review area, the left/right
columns, the source/edit panels, or the three correction panels.
Correction buttons stay in a fixed row at the bottom right.

REVIEW
Red = unresolved issues; green = checked; blue = ignored. Rows remain visible
within the chosen language view. Filter by type, search, or sort columns.
Original text and Current text show changes. Go to source opens the script
line or relevant object property in AGS.

The three equal-sized right panels show issues, suggested fixes and a preview
of the full corrected line, with the replacement highlighted in yellow.
Use suggestion saves the selected fix and selects the next unresolved issue.
Apply & next saves manual edits. A line stays red until every issue is fixed
or ignored. Ignore accepts only the selected issue. Revert selected restores
that line's original text while preserving corrections to other lines.
Correcting a line checks only that line again, never the whole game.

DICTIONARY
Only words/phrases you explicitly add are accepted. Select draft text or an
issue, then Add to dictionary, or edit entries under History / settings.
Saved entries suppress notices within the word/phrase, including style notices.
Matching ignores case and respects word boundaries. Other sentence issues
remain checked. Informal words such as gonna and wanna are not built in.

BACKUPS
In game mode, Apply and Revert save the entire AGS project, including other pending edits.
Backups and review settings live under the game's .ags-proofreader folder.
Revert stops if the document has newer edits since the scan; scan again first.

Coverage varies across languages. Custom display wrappers and text assembled
from variables may need manual review. The offline helper can use up to 2 GB
of Java heap. No game text is sent online. See THIRD-PARTY-NOTICES.txt.

Created with assistance from OpenAI Codex.


