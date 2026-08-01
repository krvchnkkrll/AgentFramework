# AgentFramework — фронтенд

Vue 3 + TypeScript + Vite. Чаты, история, отправка сообщений с вложениями,
авторизация через Keycloak, пять тем оформления.

---

## Быстрый старт

```bash
npm --prefix frontend install
```

```bash
npm --prefix frontend run dev
```

Откроется на <http://localhost:5173>. По умолчанию (`.env.development`) авторизация
выключена и данные берутся из мок-слоя — бэкенд и Keycloak запускать не нужно.

Другие команды:

```bash
npm --prefix frontend run build
```

```bash
npm --prefix frontend run typecheck
```

---

## Два переключателя, которые определяют режим работы

Оба живут в `.env.development` (общие значения) и `.env.local` (личные, в git не попадают).

| Переменная | `true` | `false` |
| --- | --- | --- |
| `VITE_AUTH_DISABLED` | Keycloak не нужен, работаем под фейковым `dev.user` | реальный логин через Keycloak |
| `VITE_USE_MOCKS` | чаты и сообщения генерируются в браузере | реальные запросы в `/api/chats/...` |

Переключатели независимы. Типичный путь: сначала оба `true`, потом включаешь Keycloak,
потом — по мере готовности эндпоинтов — выключаешь моки.

---

## Что уже ходит в реальный бэкенд

Единственный готовый эндпоинт — `GET /api/user/current-user`
([UserController.cs](../Web/Controllers/UserController.cs)). Фронт дёргает его при старте.
Если бэкенд не запущен, приложение не падает: профиль подставляется из токена,
а в меню пользователя загорается бейдж «бэкенд недоступен».

CORS настраивать не нужно: vite проксирует `/api` на `http://localhost:5175`
(профиль `http` из `launchSettings.json`), поэтому для браузера фронт и бэк — один origin.
Адрес меняется переменной `VITE_API_PROXY_TARGET`.

---

## Что закрыто заглушками

Мок-слой живёт в [src/api/mock/chats.mock.ts](src/api/mock/chats.mock.ts): пять готовых чатов
с перепиской, «печатающийся» по токенам ответ, загрузка вложений в память вкладки.
Данные сохраняются в `localStorage` (сбросить — в меню пользователя, «Сбросить мок-данные»).

Форма будущих эндпоинтов зафиксирована в [src/api/chats.real.ts](src/api/chats.real.ts):

```http
GET    /api/chats
POST   /api/chats                                 { title?, agentId? }
PATCH  /api/chats/{chatId}                        { title?, pinned?, agentId? }
DELETE /api/chats/{chatId}
GET    /api/chats/{chatId}/messages
POST   /api/chats/{chatId}/messages               { content, attachmentIds }
GET    /api/chats/{chatId}/messages/{id}/stream   text/event-stream
POST   /api/attachments                           multipart/form-data, поле "file"
DELETE /api/attachments/{attachmentId}
GET    /api/agents
```

Обе реализации подчиняются одному интерфейсу `ChatsApi`
([src/api/contract.ts](src/api/contract.ts)), а выбор делается в
[src/api/index.ts](src/api/index.ts). Поэтому переход на реальный бэкенд —
это один флаг в `.env.local`, компоненты и стор трогать не нужно.

Если на бэкенде получится другая форма ответа — правь `chats.real.ts` и `types.ts`,
TypeScript сам покажет все места, которые надо поправить.

### Про стриминг

`GET .../stream` ожидается как SSE. Каждое событие — строка `data:` с JSON.
Промежуточные куски: `{"delta":"текст"}`, финальное событие — целиком объект
`MessageResponse`. Разбор написан вручную (см. `streamAssistantMessage`), потому что
штатный `EventSource` не умеет слать заголовок `Authorization`.

Если стриминг делать не хочется — верни из этого эндпоинта одно финальное событие
с полным сообщением, фронт отработает корректно.

---

## Настройка Keycloak

Клиента для SPA в realm `workspace` пока нет. Нужно завести — в админке Keycloak,
**Clients → Create client**:

| Шаг | Поле | Значение |
| --- | --- | --- |
| General settings | Client type | `OpenID Connect` |
| | Client ID | `agentframework-web` |
| Capability config | Client authentication | **Off** (публичный клиент, secret не нужен) |
| | Authorization | Off |
| | Standard flow | **On** |
| | Direct access grants | Off |
| | Service accounts roles | Off |
| Login settings | Valid redirect URIs | `http://localhost:5173/*` |
| | Valid post logout redirect URIs | `http://localhost:5173/*` |
| | Web origins | `http://localhost:5173` |

После создания — вкладка **Advanced → Advanced settings**:
`Proof Key for Code Exchange Code Challenge Method` = **S256**.

Затем нужен маппер, чтобы в токене оказался audience API-клиента (иначе бэкенд
отклонит токен: в `Sso/DependencyInjection.cs` включена проверка `ValidAudience = agentframework-api`).

**Clients → agentframework-web → Client scopes → agentframework-web-dedicated →
Add mapper → By configuration → Audience**:

| Поле | Значение |
| --- | --- |
| Name | `agentframework-api-audience` |
| Included Client Audience | `agentframework-api` |
| Add to access token | **On** |

Бэкенд также ждёт в токене claim'ы `sub`, `email` и `preferred_username`
(см. `Sso/Services/CurrentUserService.cs`). Первые два дают стандартные scope
`profile` и `email` — убедись, что они назначены клиенту (Client scopes → Default).
У пользователя в Keycloak обязательно должен быть заполнен email, иначе
`GetCurrentUser` вернёт 403.

Когда клиент готов:

```bash
echo "VITE_AUTH_DISABLED=false" >> frontend/.env.local
```

### Самоподписанный сертификат Keycloak

`Authority` в `appsettings.json` — `https://192.168.0.14:8080`. Если сертификат
самоподписанный, браузер сначала должен ему доверять: открой этот адрес в отдельной
вкладке и прими исключение, иначе редирект на логин будет молча падать.

---

## Структура

```
src/
  api/            HTTP-клиент, контракты, реальная и мок-реализации
  auth/           Keycloak: инициализация, токены, профиль
  components/
    chat/         лента сообщений, пузыри, поле ввода, вложения
    sidebar/      список чатов, меню пользователя
    ui/           кнопки, иконки, выпадашки, тосты, диалоги
  router/         маршруты (/chat и /chat/:chatId)
  stores/         Pinia: chats, user, theme, toasts
  styles/         tokens.css (темы), base.css, markdown.css
  utils/          форматирование дат и размеров, рендер markdown
  config.ts       единственное место, где читается import.meta.env
```

Все цвета берутся из CSS-переменных в [src/styles/tokens.css](src/styles/tokens.css).
Чтобы добавить тему: скопируй блок `[data-theme='...']` и добавь запись в массив
`themes` в [src/stores/theme.ts](src/stores/theme.ts). Больше нигде править не нужно.

---

## Горячие клавиши

| Клавиши | Действие |
| --- | --- |
| `Enter` | отправить сообщение |
| `Shift`+`Enter` | перенос строки |
| `Ctrl`/`Cmd`+`B` | свернуть/развернуть панель чатов |
| `Ctrl`/`Cmd`+`K` | фокус в поиск по чатам |
