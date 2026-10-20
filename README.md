# zatyshok-pms

Типова інформаційна система для мережі готелів «Затишок»: PMS (робоче місце персоналу)
та портал онлайн-бронювання. Комп'ютерний практикум з дисципліни «Проєктування і
розроблення ІС та технологій», КПІ, кафедра ІСТ, група ІТ-61м.
Команда: Коркішко Віктор, Геневський Максим.

Специфікація — звіт 1 у [docs/](docs): вимоги — розділ 6, сценарії — розділ 8,
архітектура і сутності БД — розділ 9.

## Структура репозиторію

| Папка | Призначення |
|---|---|
| `src/Zatyshok.Domain` | Доменна модель: сутності, enum-и статусів, доменні правила (без залежностей) |
| `src/Zatyshok.Infrastructure` | EF Core: `ZatyshokDbContext`, конфігурації, міграції, seed |
| `src/Zatyshok.Api` | ASP.NET Core 8: контролери, DTO, Swagger |
| `src/Zatyshok.Worker` | Фонові завдання (листи з Outbox, неявки, нагадування) |
| `tests/Zatyshok.Tests` | xUnit-тести: модульні, інтеграційні (PostgreSQL) і архітектурні |
| `deploy/sql` | SQL-скрипти для розгортання (ролі й права БД) |
| `web/staff`, `web/guest` | React-клієнти (цей етап їх не чіпає) |

## Передумови

- .NET 8 SDK
- Docker Desktop (PostgreSQL 16 і поштова заглушка smtp4dev запускаються в контейнерах;
  інтеграційні тести теж запускають PostgreSQL у контейнері, див. «Тести»)

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

# 5. Створити схему БД (міграції EF Core InitialRooms, FullDomainModel, ReportingViewsAndAuditGuard)
dotnet ef database update --project src/Zatyshok.Infrastructure --startup-project src/Zatyshok.Api

# 6. Запустити API
dotnet run --project src/Zatyshok.Api
```

Після запуску:

- Swagger: <http://localhost:5084/swagger>
- Перевірка стану (включно з БД): <http://localhost:5084/health> → `Healthy`
- Пошта розробника (smtp4dev): <http://localhost:8025>

### Демо-дані

При запуску в середовищі Development порожня БД наповнюється демо-даними
(`DevelopmentDataSeeder`). Seed має три частини, і кожна пропускається, якщо вже має рядки
її перша таблиця: готелі — `Hotels`, послуги — `Services`, люди — `Users` (гості й
корпоративний клієнт додаються лише разом з користувачами):

- 5 готелів мережі (Київ, Львів, Одеса, Дніпро, Яремче), категорії Standard/Superior/Suite,
  по 5 номерів на готель, базові тарифи для кожної пари «готель–категорія» і два сезонні
  періоди (літо в Одесі, зимові свята в Карпатах);
- 3 послуги: сніданок, паркування, трансфер;
- 9 користувачів, по одному на кожну роль (таблиця нижче);
- 3 гості: Петренко (пов'язаний з обліковим записом гостя; документа немає, тож заселення
  його попросить), Коваленко (паспорт), Smith (закордонний паспорт);
- 1 корпоративний клієнт: ТОВ «Приклад Консалт» (ЄДРПОУ 12345678, відстрочка оплати 14 днів).

Самі 9 ролей заносить міграція `FullDomainModel` у будь-якому середовищі, з id
`66666666-6666-6666-6666-6666666666NN`, де NN — номер ролі з таблиці. Id користувача —
`77777777-7777-7777-7777-7777777777NN` з тим самим номером.

| NN | Роль (`Code`) | Пошта | Готель |
|---|---|---|---|
| 01 | Гість (`Guest`) | ivan.petrenko@example.com | — |
| 02 | Оператор рецепції (`Receptionist`) | receptionist@zatyshok.ua | Затишок Київ |
| 03 | Оператор відділу бронювання (`ReservationAgent`) | reservations@zatyshok.ua | уся мережа |
| 04 | Покоївка (`Housekeeper`) | housekeeper@zatyshok.ua | Затишок Київ |
| 05 | Старша покоївка (`HeadHousekeeper`) | head.housekeeper@zatyshok.ua | Затишок Київ |
| 06 | Менеджер готелю (`HotelManager`) | manager@zatyshok.ua | Затишок Київ |
| 07 | Бухгалтер (`Accountant`) | accountant@zatyshok.ua | уся мережа |
| 08 | Керівництво мережі (`Executive`) | executive@zatyshok.ua | уся мережа |
| 09 | Адміністратор системи (`Administrator`) | admin@zatyshok.ua | уся мережа |

> **Увійти під цими користувачами поки неможливо.** Хешування паролів і вхід з'являться
> в T4, тому в `PasswordHash` усіх користувачів seed записано заглушку
> `DevelopmentDataSeeder.UnusablePasswordHash`, яка не є хешем жодного пароля. Тестовий
> пароль буде задокументовано тут разом із T4.

Id усіх рядків seed детерміновані й однакові після кожного скидання БД: готелі
`22222222-…-2222222222NN`, категорії `11111111-…-1111111111NN`, номери
`33333333-…-33333333HHRR` і тарифи `44444444-…-44444444HHCC` (HH — індекс готелю від 00,
RR/CC — індекс номера/категорії від 00), послуги `88888888-…-8888888888NN`, гості
`99999999-…-9999999999NN`, корпоративний клієнт `aaaaaaaa-…-aaaaaaaaaa01`.

БД, засіяну ще в T1, варто перестворити (див. «Корисні команди»): seed не чіпає наявні
готелі, тож у ній залишаться старі id номерів і тарифів.

## Тести

```bash
dotnet test
```

У проєкті `tests/Zatyshok.Tests` три види тестів:

- **модульні** (`Modules/…`) — доменні правила, зокрема всі дозволені й заборонені переходи
  станів таблиць 12.25–12.28 звіту 2; БД не потрібна;
- **архітектурні** (`Architecture/…`, ArchUnitNET) — напрям залежностей проєктів і межі
  модулів (п. 9.4.1 звіту 2); БД не потрібна;
- **інтеграційні** (`Integration/…`, позначені `Category=Integration`) — працюють з
  PostgreSQL: міграції й відкат до `InitialRooms`, часткові унікальні індекси, блокування
  тарифу при двох одночасних бронюваннях і рахунку при двох одночасних платежах,
  завантаження колекцій бронювання й рахунку без `Include`, тригер і права журналу аудиту,
  представлення схеми `reporting`, генерована сума позиції рахунку, CHECK переліків,
  правило порівняння `uk-UA-x-icu`, seed на порожній БД і на БД з даними T1. Кожен клас
  тестів створює власну тимчасову БД `zatyshok_test_…`, застосовує до неї міграції і
  видаляє її після своїх тестів.

Де інтеграційні тести беруть PostgreSQL:

- **Запущено Docker Desktop** — налаштовувати нічого не треба: тести самі піднімають
  контейнер `postgres:16` (Testcontainers) і видаляють його після прогону. Перший запуск
  завантажує образ.
- **Без Docker або з уже запущеним сервером** — задайте змінну `ZATYSHOK_TEST_PG`: рядок
  підключення до сервера PostgreSQL 16 з правами суперкористувача, без імені бази. Тести
  створюють і видаляють лише власні бази й ролі з префіксом `zatyshok_test_`, базу
  `zatyshok` не чіпають. Наприклад, для PostgreSQL з `docker compose` (користувач з `.env`
  у цьому контейнері — суперкористувач):

  ```powershell
  # PowerShell
  $env:ZATYSHOK_TEST_PG = "Host=localhost;Port=5432;Username=zatyshok;Password=zatyshok_dev"
  dotnet test
  ```

  ```bash
  # bash
  ZATYSHOK_TEST_PG="Host=localhost;Port=5432;Username=zatyshok;Password=zatyshok_dev" dotnet test
  ```

Лише модульні й архітектурні тести, без PostgreSQL:

```bash
dotnet test --filter "Category!=Integration"
```

## Ролі БД для розгортання

У середовищі розроблення застосунок працює під власником БД. У робочому середовищі права
розділено (п. 12.8.4 звіту 2): міграції застосовує власник `zatyshok_owner`, а API і Worker
працюють під `zatyshok_app` з обмеженими правами (на журнал аудиту — лише SELECT, INSERT і
DELETE, причому тригер дозволяє видаляти тільки записи, старші за 3 роки). Ролі й права
створює скрипт `deploy/sql/roles-and-grants.sql`; його запускає суперкористувач після
міграцій, порядок кроків описано на початку скрипту:

```bash
psql -h <host> -U postgres -d zatyshok -v ON_ERROR_STOP=1 -f deploy/sql/roles-and-grants.sql
```

Після відкату міграцій і повторного застосування скрипт треба запустити знову: разом зі
схемами зникають і права, тож створені заново таблиці будуть недоступні для `zatyshok_app`.

## Корисні команди

```bash
# psql усередині контейнера
docker exec -it zatyshok-postgres psql -U zatyshok -d zatyshok

# нова міграція
dotnet ef migrations add <Name> --project src/Zatyshok.Infrastructure --startup-project src/Zatyshok.Api

# скинути БД повністю (обережно: видаляє дані) — окремими командами,
# бо `&&` не працює у Windows PowerShell 5.1; після `up -d` БД порожня,
# тож схему треба створити знову, інакше `dotnet run` впаде
docker compose down -v
docker compose up -d
dotnet ef database update --project src/Zatyshok.Infrastructure --startup-project src/Zatyshok.Api
```

## Конвенції

- Гілки `feature/<назва>`, злиття в `main` через pull request.
- Коміти — короткі описові повідомлення англійською без префіксів (`add Tests project`, `update README`).
- Перед комітом: `dotnet build` без помилок, `dotnet test` зелений.
