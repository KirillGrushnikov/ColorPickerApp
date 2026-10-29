; Inno Setup Script для Avalonia .NET 8 (self-contained)
; ======================================================
; Инструкция по подготовке файлов:
; 1. Откройте проект в консоли и выполните публикацию:
;    !publish.bat  (или publish-aot.bat - тогда SourcePath = "..\publish-aot\")
; 2. Укажите ниже правильный путь к папке publish (переменная SourcePath)
; 3. При необходимости добавьте license.txt, app.ico в папку со скриптом

#define MyAppName      "Color Picker Pro"
#define MyAppVersion   "2.5.2"
#define MyAppPublisher "NeffeX"
#define MyAppExeName   "ColorPickerApp.exe"               ; имя exe-файла после публикации

; Путь к папке с опубликованными файлами (относительно расположения скрипта)
#define SourcePath     "..\publish-aot\"

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
; Установщик предупредит, если программа запущена (мьютекс создаётся в SingleInstance.cs)
AppMutex=ColorPickerPro-E1AAE4FC-2D5A-4ADE-B2E8-D1E3B4BAC5A5-mutex

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[InstallDelete]
; При обновлении удаляем DLL старой версии (часть зависимостей больше не используется)
Type: files; Name: "{app}\*.dll"

[Files]
; Рекурсивное копирование всех файлов из папки публикации (без отладочных .pdb)
Source: "{#SourcePath}\*"; Excludes: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Автозапуск хранится в HKCU\...\Run (см. StartupManager.cs) - удаляем значение при деинсталляции
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#MyAppName}"; Flags: uninsdeletevalue dontcreatekey

[UninstallDelete]
; Ярлык автозапуска от старых версий
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall nowait skipifsilent