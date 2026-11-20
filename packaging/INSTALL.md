# ANCAD SQLite · ручная установка

Нужны AutoCAD 2020–2024 для Windows x64 и среда .NET Framework 4.8.
Для копирования плагина достаточно прав текущего пользователя.

1. Закройте AutoCAD.
2. Если в свойствах скачанного ZIP есть кнопка **Разблокировать**, используйте её перед распаковкой.
3. Распакуйте ZIP. Откройте через `Win+R` папку `%APPDATA%\Autodesk\ApplicationPlugins` и скопируйте в неё папку `ANCAD_SQLite.bundle` целиком. Создайте папку `ApplicationPlugins`, если её ещё нет. При обновлении замените старую папку `ANCAD_SQLite.bundle`.
4. Запустите AutoCAD, откройте чертёж и введите `DB_EDIT`. AutoCAD регистрирует команду по файлу `PackageContents.xml` и загружает плагин при первом вызове.
5. Если настройки безопасности требуют доверенного пути, добавьте полный путь к папке `ANCAD_SQLite.bundle\Contents\Win64` в **Параметры → Файлы → Надёжные расположения** (`TRUSTEDPATHS`). При управляемой политике AutoCAD применяются разрешённые администратором пути.

Установленный пакет имеет следующую структуру.

```text
%APPDATA%\Autodesk\ApplicationPlugins\ANCAD_SQLite.bundle\
├── PackageContents.xml
└── Contents\Win64\
    ├── AcadDevices.DbEditor.dll
    ├── AcadDevices.Core.dll
    ├── System.Data.SQLite.dll
    └── x64\SQLite.Interop.dll
```

Чтобы удалить плагин, закройте AutoCAD и удалите папку `ANCAD_SQLite.bundle`. Файлы `.db` хранятся по выбранным вами путям.
