#define AppVersion "1.2.1"
[Setup]
AppId={{C52EC40E-23BF-4F3F-8FD2-7BDCCB56B840}
AppName=AGS Spellchecker
AppVersion={#AppVersion}
AppPublisher=AGS Spellchecker contributors
DefaultDirName={autopf}\AGS Spellchecker
DisableProgramGroupPage=yes
OutputDir=..\deliverables
OutputBaseFilename=AGS-Spellchecker-Setup
SetupIconFile=..\Assets\spellchecker.ico
UninstallDisplayIcon={app}\spellchecker.ico
Compression=lzma2/fast
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
InfoBeforeFile=..\offline\INSTALL-NOTES.txt
[Files]
Source: "..\offline\AGS.Plugin.Spellchecker.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\offline\Runtime\*"; DestDir: "{app}\Runtime"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\offline\README.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\offline\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\offline\DEPENDENCIES.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Assets\spellchecker.ico"; DestDir: "{app}"; Flags: ignoreversion
[Registry]
Root: HKLM; Subkey: "SOFTWARE\AGS Spellchecker"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
[Code]
var
  Targets: TStringList;
  Selection: TInputOptionWizardPage;
  ManualPath: TNewEdit; ManualLabel: TNewStaticText; Browse: TNewButton;

function Supported(const Folder: String): Boolean;
var MS, LS: Cardinal;
begin
  Result := GetVersionNumbers(AddBackslash(Folder) + 'AGSEditor.exe', MS, LS);
  if Result then Result := (MS = $00030006) and (LS >= 47);
end;

procedure AddTarget(const Folder: String);
begin
  if Supported(Folder) and (Targets.IndexOf(Folder) < 0) then Targets.Add(Folder);
end;

procedure Discover(const Root: String);
var Entry: TFindRec;
begin
  if FindFirst(AddBackslash(Root) + 'Adventure Game Studio*', Entry) then begin
    try repeat
      if Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then AddTarget(AddBackslash(Root) + Entry.Name);
    until not FindNext(Entry); finally FindClose(Entry); end;
  end;
end;

procedure UpdateManualPath(Sender: TObject);
var I: Integer; ShowManual: Boolean;
begin
  ShowManual := Targets.Count = 0;
  for I := 0 to Targets.Count-1 do if not Selection.Values[I] then ShowManual := True;
  if ExpandConstant('{param:AGSDIR|}') <> '' then ShowManual := True;
  ManualPath.Visible := ShowManual; ManualLabel.Visible := ShowManual; Browse.Visible := ShowManual;
end;

procedure BrowseClick(Sender: TObject);
var FileName: String;
begin
  FileName := AddBackslash(ManualPath.Text) + 'AGSEditor.exe';
  if GetOpenFileName('Choose AGSEditor.exe', FileName, '', 'AGS editor|AGSEditor.exe', 'exe') then ManualPath.Text := ExtractFileDir(FileName);
end;
procedure InitializeWizard;
var I: Integer; Keys: TArrayOfString; Folder, Key: String;
begin
  Targets := TStringList.Create;
  Discover(ExpandConstant('{pf32}'));
  Discover(ExpandConstant('{pf64}'));
  if RegGetSubkeyNames(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', Keys) then
    for I := 0 to GetArrayLength(Keys)-1 do begin
      Key := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + Keys[I];
      if RegQueryStringValue(HKLM, Key, 'InstallLocation', Folder) then AddTarget(RemoveBackslashUnlessRoot(Folder));
    end;
  Selection := CreateInputOptionPage(wpSelectDir, 'Choose AGS installations', 'Install the plugin into the selected editors.', 'Select your editor below. Uncheck a detected location to browse for AGSEditor.exe here.', False, False);
  for I := 0 to Targets.Count-1 do begin Selection.Add(Targets[I]); Selection.Values[I] := True; end;
  Selection.CheckListBox.Height := ScaleY(105);
  Selection.CheckListBox.OnClickCheck := @UpdateManualPath;
  ManualLabel := TNewStaticText.Create(WizardForm); ManualLabel.Parent := Selection.Surface;
  ManualLabel.Top := Selection.CheckListBox.Top + Selection.CheckListBox.Height + ScaleY(12);
  ManualLabel.Caption := 'Choose another editor folder (containing AGSEditor.exe):';
  ManualLabel.AutoSize := True;
  ManualPath := TNewEdit.Create(WizardForm); ManualPath.Parent := Selection.Surface;
  ManualPath.Top := ManualLabel.Top + ScaleY(22); ManualPath.Width := Selection.SurfaceWidth - ScaleX(90);
  ManualPath.Text := ExpandConstant('{param:AGSDIR|}');
  Browse := TNewButton.Create(WizardForm); Browse.Parent := Selection.Surface;
  Browse.Caption := 'Browse...'; Browse.Width := ScaleX(80);
  Browse.Left := Selection.SurfaceWidth - Browse.Width; Browse.Top := ManualPath.Top;
  Browse.Height := ManualPath.Height; Browse.OnClick := @BrowseClick;
  UpdateManualPath(nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var I, Count: Integer;
begin
  Result := True;
  if CurPageID = Selection.ID then begin
    Count := 0;
    for I := 0 to Targets.Count-1 do if Selection.Values[I] then Count := Count+1;
    if ManualPath.Visible and (ManualPath.Text <> '') then begin
      if not Supported(ManualPath.Text) then begin MsgBox('Select an AGS 3.6 editor folder (minimum stable version 3.6.0.47).', mbError, MB_OK); Result := False; Exit; end;
      Count := Count+1;
    end;
    if Count = 0 then begin MsgBox('Select at least one AGS editor installation.', mbError, MB_OK); Result := False; end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var I: Integer; Folder, Target, Installed: String; Chosen: TStringList;
begin
  if CurStep <> ssPostInstall then Exit;
  Chosen := TStringList.Create;
  try
    for I := 0 to Targets.Count-1 do if Selection.Values[I] then Chosen.Add(Targets[I]);
    if ManualPath.Visible and (ManualPath.Text <> '') and (Chosen.IndexOf(ManualPath.Text) < 0) then Chosen.Add(ManualPath.Text);
    Installed := '';
    for I := 0 to Chosen.Count-1 do begin
      Folder := Chosen[I]; Target := AddBackslash(Folder) + 'AGS.Plugin.Spellchecker.dll';
      if FileExists(Target) then begin
        ForceDirectories(ExpandConstant('{app}\PreviousPlugins'));
        FileCopy(Target, ExpandConstant('{app}\PreviousPlugins\') + IntToStr(I) + '-AGS.Plugin.Spellchecker.dll', False);
      end;
      if not FileCopy(ExpandConstant('{app}\AGS.Plugin.Spellchecker.dll'), Target, False) then
        MsgBox('The plugin could not be copied to ' + Folder + '. Close AGS and run Setup again. The offline engine has been installed.', mbError, MB_OK)
      else Installed := Installed + Target + #13#10;
    end;
    SaveStringToFile(ExpandConstant('{app}\installed-plugins.txt'), Installed, False);
  finally Chosen.Free; end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Lines: TArrayOfString; I: Integer; Current: String;
begin
  if CurUninstallStep <> usUninstall then Exit;
  if LoadStringsFromFile(ExpandConstant('{app}\installed-plugins.txt'), Lines) then
    for I := 0 to GetArrayLength(Lines)-1 do begin
      Current := Lines[I];
      if (ExtractFileName(Current) = 'AGS.Plugin.Spellchecker.dll') and FileExists(Current) and
         (GetSHA256OfFile(Current) = GetSHA256OfFile(ExpandConstant('{app}\AGS.Plugin.Spellchecker.dll'))) then
        if not DeleteFile(Current) then MsgBox('Close AGS and remove this plugin file manually: ' + Current, mbInformation, MB_OK);
    end;
end;
[UninstallDelete]
Type: files; Name: "{app}\installed-plugins.txt"






