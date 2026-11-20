# ANCAD SQLite

Редактор файлов SQLite `.db` для AutoCAD 2020–2024 на Windows x64. Команда
`DB_EDIT` открывает редактор в модальном окне AutoCAD.

## Установка

Скачайте `ANCAD_SQLite-manual-install-v<версия>.zip` из раздела [Релизы](https://github.com/veb86/ANCAD_SQLite/releases).
Для установки нужна среда .NET Framework 4.8. Закройте AutoCAD, распакуйте архив
и скопируйте папку `ANCAD_SQLite.bundle` целиком в `%APPDATA%\Autodesk\ApplicationPlugins`.
Откройте чертёж и выполните `DB_EDIT`. Шаги обновления и установки доверенного пути
описаны в [INSTALL.md](packaging/INSTALL.md), который также входит в архив.

## Редактирование

1. **Создать новую БД** создаёт файл и таблицу `Devices` с полями `Id`, `Name`,
   `Type`, `Model`, `Manufacturer` и `Description`. **Открыть БД** отображает
   таблицы существующего файла. Полный путь показан рядом с кнопками.
2. Выберите таблицу в списке или создайте её через **Создать таблицу**. Доступны
   типы `TEXT`, `INTEGER` и `REAL`. В новой таблице `Id` присваивается автоматически.
3. Дважды щёлкните ячейку для правки. Чтобы внести новую запись, нажмите **Добавить строку**.
   Для удаления выделите строки и нажмите `Delete` или **Удалить строку**.
   Поле **Поиск / фильтр** ищет подстроку по всем текстовым столбцам.
4. Нажмите **Сохранить изменения** для записи одной транзакцией. При ошибке правки
   остаются в редакторе. **Обновить** перечитывает файл и после подтверждения
   отменяет несохранённые правки.

Для существующих таблиц действуют их ограничения SQLite. Столбец `Id` и
вычисляемые столбцы доступны для чтения. Пустые поля новой строки получают
значения по умолчанию из схемы.

## Сборка и проверка

`AcadDevices.sln` содержит два проекта C# для .NET Framework 4.8. Библиотека
`AcadDevices.Core` выполняет операции SQLite и предоставляет снимок на основе
`DataTable` через `SqliteDatabase.LoadTable`. `SaveTable` сохраняет снимок и
возвращает новые данные с присвоенными значениями. `AcadDevices.DbEditor`
содержит команду AutoCAD и формы Windows Forms.

Ссылки на Autodesk AutoCAD 2020 (23.1) восстанавливаются из NuGet. AutoCAD
предоставляет свои DLL при загрузке. В выходную папку попадают управляемая
библиотека SQLite и её нативная зависимость `x64/SQLite.Interop.dll`.

```powershell
dotnet build AcadDevices.sln -c Release -p:Platform=x64
dotnet msbuild scripts/Package.proj
python tests/verify_package.py artifacts/ANCAD_SQLite-manual-install-v1.0.0.zip

dotnet build tests/AcadDevices.Tests -c Release -p:Platform=x64
./tests/AcadDevices.Tests/bin/x64/Release/AcadDevices.Tests.exe
```

Выходная папка плагина находится в `src/AcadDevices.DbEditor/bin/x64/Release`.
Тесты на Windows проверяют файл SQLite, создание таблиц, поиск, CRUD, откат
транзакции, конфликт одновременных правок и работу форм. Для ручной проверки в
каждой версии AutoCAD установите один и тот же ZIP, вызовите `DB_EDIT`, создайте
базу и таблицу, добавьте и измените запись, сохраните и откройте файл заново.

GitHub Actions собирает и проверяет архив. При успешной сборке основной ветки
репозитория новый `Version` из `Directory.Build.props` публикуется как релиз.
