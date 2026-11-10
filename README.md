# HelpDesk API: учебный проект с нуля

Это сервис заявок в поддержку. Пользователь сообщает о проблеме, сотрудник берёт её в работу, пишет комментарии и закрывает после решения. Проект написан на C# с .NET 10, ASP.NET Core, EF Core, SQL Server и xUnit. Отправлять запросы можно через Swagger UI.

## С чего начать обучение

1. Откройте [учебник PDF](output/pdf/HelpDesk-Development-Guide-RU.pdf) или [его текст](docs/development-guide.md). Начните с главы 1, а не с большого Program.cs.
2. Разберите основы C# и HTTP, затем запустите пример по главе 6.
3. Пройдите [карту чтения кода](docs/reading-map.md). Она ведёт через конкретные методы.
4. После этого обращайтесь к [техническому справочнику](docs/technical-reference.md) и [контракту API](docs/api.md).

Учебник объясняет задачу каждого инструмента, причину выбора, альтернативы и работу без него. В нём есть упражнения с ответами. Не нужно знать все термины заранее.

## Первый запуск через Docker

Нужен установленный и запущенный Docker Desktop с Linux containers и Compose v2. Для SQL Server нужен x86-64 компьютер; выделите Docker минимум 4 ГБ памяти. Откройте PowerShell в папке с этим README и выполняйте по одной команде:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/New-LocalSettings.ps1
docker compose up --build -d
docker compose ps
```

Первый скрипт создаёт .env со случайными локальными паролями. Если файл уже есть, скрипт не переписывает его: используйте существующий. Не публикуйте .env. Дождитесь успешного ответа [проверки готовности](http://localhost:5080/health/ready), затем откройте [Swagger](http://localhost:5080/swagger).

В POST /api/auth/login войдите как alice@example.test с паролем HELPDESK_DEMO_PASSWORD из своего .env. useCookies оставьте false. Скопируйте accessToken в Authorize без слова Bearer. GET /api/tickets покажет свои заявки. support@example.test видит все, bob@example.test - только свои. У демопользователей один локальный демопароль.

При изменении передавайте свежую version из последнего ответа. Закрытую заявку сначала откройте командой reopen. Пошаговый полный сценарий - в главе 6 учебника.

Остановка с сохранением данных: docker compose down. Добавление -v удаляет базу и ключи, поэтому для обычной остановки оно не нужно. Если возникла ошибка, смотрите docker compose logs api db.

[Подробный запуск без Docker и диагностика](docs/maintainer-readme.md). Инструкции там выполняются из корня проекта. Обычный dotnet run не читает .env автоматически.

## Проверки для следующего этапа

При установленном SDK .NET 10 из корня:

```powershell
dotnet restore HelpDesk.sln --locked-mode
dotnet build HelpDesk.sln -c Release --no-restore
dotnet test --solution HelpDesk.sln -c Release --no-build
```

Сначала загружаются библиотеки, затем собирается код, затем выполняются тесты. Локально прошёл 41 тест на SQLite. Docker и настоящий SQL Server в исходной среде не запускались: они не установлены. [Результаты и ограничения проверки](docs/verification.md).

## Где искать остальное

- [Примеры HTTP-запросов](docs/requests.http).
- [Снимок описания API](docs/openapi.json).
- [Расширенный маршрут чтения](docs/reading-map-advanced.md).
- [Прежний подробный README](docs/maintainer-readme.md): схема данных, роли, миграции, CI и ограничения рабочего развёртывания.

Это локальный учебный проект. Реальная доставка email, мгновенный отзыв access-токенов и настройки публичного сервера в его объём не входят. Начните с одного сценария заявки и расширяйте знания по мере чтения кода.
