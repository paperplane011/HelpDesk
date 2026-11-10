# HelpDesk API

API сервиса технической поддержки: создание заявок, назначение исполнителя, комментарии и закрытие. Пользователь видит свои заявки, сотрудник поддержки - все. Доступны фильтрация, сортировка и постраничная выдача.

## Технологии

- C#, .NET 10, ASP.NET Core Web API
- Entity Framework Core, Microsoft SQL Server
- ASP.NET Core Identity
- Swagger / OpenAPI
- xUnit, SQLite для интеграционных тестов
- Docker Compose, GitHub Actions

## Запуск

Нужен Docker Desktop с Linux containers и Compose v2 (x86-64). Из корня проекта в PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/New-LocalSettings.ps1
docker compose up --build -d
```

Скрипт создаёт `.env` со случайными паролями; если файл уже существует, повторять первый шаг не нужно. При старте применяются миграции и добавляются демонстрационные данные.

Swagger UI: [localhost:5080/swagger](http://localhost:5080/swagger).

Для входа используйте `POST /api/auth/login` с `useCookies=false`:

- `alice@example.test` - пользователь;
- `support@example.test` - сотрудник поддержки.

Пароль - `HELPDESK_DEMO_PASSWORD` из `.env`. Вставьте полученный `accessToken` в **Authorize** без префикса `Bearer`. При изменении заявки передавайте актуальную `version` из последнего ответа.

Остановка: `docker compose down`.

## Тесты

Нужен .NET SDK 10:

```powershell
dotnet test --solution HelpDesk.sln -c Release
```

[Подробная инструкция](docs/maintainer-readme.md) · [Примеры запросов](docs/requests.http) · [Документация](docs/development-guide.md)
