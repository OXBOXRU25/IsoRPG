; Установщик игры.
;
; Ставится всё сразу — лаунчер и игра, — а ярлык создаётся только на лаунчер.
; Он и есть точка входа: через него игра обновляется и через него видно, что
; изменилось. Ярлык прямо на игру означал бы, что половина игроков никогда не
; узнает про новую версию.
;
; Права администратора не запрашиваем. Игра — не системная программа, и
; запрос прав при установке пугает сильнее, чем помогает; с lowest установка
; идёт в папку пользователя и проходит без единого окна UAC.
;
; Версия и пути приходят снаружи, из скрипта сборки: держать их здесь значит
; править файл при каждом выпуске и однажды забыть.
;
; Файл сохранён в UTF-8 с BOM — иначе Inno Setup читает кириллицу как набор
; символов, и это видно только в готовом установщике.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef PackageDir
  #define PackageDir "..\..\Package\Adventures of Zhenya"
#endif

#ifndef OutputDir
  #define OutputDir "..\..\Package"
#endif

#define AppName "Adventures of Zhenya"
#define AppPublisher "OXBOX"
#define LauncherExe "Adventures of Zhenya.exe"

[Setup]
; Идентификатор установки. Постоянный: по нему Windows понимает, что новая
; версия — это обновление той же программы, а не вторая копия рядом.
AppId={{8F3A2C41-7B6D-4E19-9C05-A1D4E7B2F830}

AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion=1.0.0.0

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Без прав администратора: {autopf} тогда указывает в папку пользователя.
PrivilegesRequired=lowest

OutputDir={#OutputDir}
OutputBaseFilename=Установка {#AppName} {#AppVersion}
SetupIconFile=..\assets\launcher.ico

; Максимальное сжатие и одним блоком: игра — это сотни файлов, между
; которыми много общего, и solid-режим ужимает их заметно сильнее.
Compression=lzma2/max
SolidCompression=yes

WizardStyle=modern
UninstallDisplayIcon={app}\{#LauncherExe}
UninstallDisplayName={#AppName}

; Показываем, сколько места нужно, до начала копирования.
DirExistsWarning=no
AllowNoIcons=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; \
  GroupDescription: "Дополнительно:"

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#LauncherExe}"
Name: "{group}\Удалить {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#LauncherExe}"; \
  Tasks: desktopicon

[Run]
Filename: "{app}\{#LauncherExe}"; Description: "Запустить игру"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Файлы, которые появляются уже после установки: журнал лаунчера и настройки.
; Без этого папка остаётся после удаления, и в списке программ игра исчезает,
; а на диске нет.
Type: files; Name: "{app}\launcher.log"
Type: dirifempty; Name: "{app}"
