; Inno Setup Script для Avalonia .NET 8 (self-contained)
; ======================================================
; Инструкция по подготовке файлов:
; 1. Откройте проект в консоли и выполните публикацию:
;    dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
; 2. Укажите ниже правильный путь к папке publish (переменная SourcePath)
; 3. При необходимости добавьте license.txt, app.ico в папку со скриптом

#define MyAppName      "Color Picker Pro"
#define MyAppVersion   "2.3.0"
#define MyAppPublisher "NeffeX"
#define MyAppExeName   "ColorPickerApp.exe"               ; имя exe-файла после публикации

; Путь к папке с опубликованными файлами (относительно расположения скрипта)
#define SourcePath     "..\publish\"

[Setup]
; Уникальный идентификатор приложения (сгенерируйте свой через GUID Generator)
AppId={{E1AAE4FC-2D5A-4ADE-B2E8-D1E3B4BAC5A5}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.\Output
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=app.ico
ShowLanguageDialog=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
; Рекурсивное копирование всех файлов из папки публикации
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Исключить отладочные .pdb (раскомментируйте при необходимости):
; Source: "{#SourcePath}\*"; Excludes: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall nowait skipifsilent