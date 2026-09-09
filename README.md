# zatyshok-pms

Типова інформаційна система для мережі готелів «Затишок»: PMS (робоче місце персоналу)
та портал онлайн-бронювання. Комп'ютерний практикум з дисципліни «Проєктування і
розроблення ІС та технологій», КПІ, кафедра ІСТ, група ІТ-61м.
Команда: Коркішко Віктор, Геневський Максим.

Специфікація — у [docs/](docs) (вимоги — 03, сценарії — 05, архітектура — 07).
План поточного етапу і журнал помилок — у [PLAN.md](PLAN.md).

## Структура репозиторію

| Папка | Призначення |
|---|---|
| `src/Zatyshok.Domain` | Доменна модель: сутності, enum-и статусів, доменні правила (без залежностей) |
| `src/Zatyshok.Infrastructure` | EF Core: `ZatyshokDbContext`, конфігурації, міграції, seed |
| `src/Zatyshok.Api` | ASP.NET Core 8: контролери, DTO, Swagger |
| `src/Zatyshok.Worker` | Фонові завдання (листи з Outbox, неявки, нагадування) |
| `tests/Zatyshok.Tests` | xUnit-тести |
| `web/staff`, `web/guest` | React-клієнти (цей етап їх не чіпає) |

## Передумови

- .NET 8 SDK
- Docker Desktop (PostgreSQL 16 і поштова заглушка smtp4dev запускаються в контейнерах)

## Запуск з нуля

```bash
# 1. Змінні середовища для Docker (логін/пароль БД для розробки)
cp .env.example .env

# 2. PostgreSQL 16 (порт 5432) + smtp4dev (веб-перегляд листів на http://localhost:8025)
docker compose up -d

# 3. Локальні інструменти (dotnet-ef)
dotnet tool restore

# 4. Пароль БД для застосунку — один раз, значення як POSTGRES_PASSWORD у .env.
#    Альтернатива: змінна середовища DB_PASSWORD.
dotnet user-secrets set DB_PASSWORD zatyshok_dev --project src/Zatyshok.Api

# 5. Створити схему БД (міграції EF Core)
dotnet ef database update --project src/Zatyshok.Infrastructure --startup-project src/Zatyshok.Api

# 6. Запустити API
dotnet run --project src/Zatyshok.Api
```

Після запуску:

- Swagger: <http://localhost:5084/swagger>
- Перевірка стану (включно з БД): <http://localhost:5084/health> → `Healthy`
- Пошта розробника (smtp4dev): <http://localhost:8025>

При першому запуску в середовищі Development порожня БД наповнюється демо-даними:
5 готелів мережі (Київ, Львів, Одеса, Дніпро, Яремче), категорії Standard/Superior/Suite,
по 5 номерів на готель, базові тарифи для кожної пари «готель–категорія» і два сезонні
періоди (літо в Одесі, зимові свята в Карпатах).

## Тести

```bash
dotnet test
```

## Корисні команди

```bash
# psql усередині контейнера
docker exec -it zatyshok-postgres psql -U zatyshok -d zatyshok

# нова міграція
dotnet ef migrations add <Name> --project src/Zatyshok.Infrastructure --startup-project src/Zatyshok.Api

# скинути БД повністю (обережно: видаляє дані) — два окремі кроки,
# бо `&&` не працює у Windows PowerShell 5.1
docker compose down -v
docker compose up -d
```

## Конвенції

- Гілки `feature/<назва>`, злиття в `main` через pull request.
- Коміти — Conventional Commits англійською (`feat:`, `fix:`, `chore:`, `docs:`, `test:`).
- Перед комітом: `dotnet build` без помилок, `dotnet test` зелений.
