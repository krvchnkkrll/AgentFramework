/** Стартовые данные мок-слоя: несколько чатов с перепиской. */
import type { AgentResponse, ChatResponse, MessageResponse, SkillResponse } from '../types';

const now = Date.now();
const minutes = (n: number) => new Date(now - n * 60_000).toISOString();
const days = (n: number) => new Date(now - n * 86_400_000).toISOString();

/** Скиллы, загруженные пользователем. На бэкенде это записи в БД со ссылкой на архив. */
export const seedSkills: SkillResponse[] = [
  {
    id: 'skill-postgres-review',
    name: 'postgres-review',
    description:
      'Разбор и ускорение SQL-запросов к PostgreSQL. Использовать, когда просят посмотреть '
      + 'запрос, объяснить план выполнения, подобрать индекс.',
    sizeBytes: 4_812,
    hasScripts: false,
    shared: false,
    createdAt: days(12),
    updatedAt: days(12),
  },
  {
    id: 'skill-dotnet-conventions',
    name: 'dotnet-conventions',
    description:
      'Правила написания кода в этом проекте на C# и .NET. Использовать при написании '
      + 'и ревью кода бэкенда.',
    sizeBytes: 2_140,
    hasScripts: false,
    shared: false,
    createdAt: days(12),
    updatedAt: days(5),
  },
];

const skillByName = (name: string): SkillResponse =>
  seedSkills.find((skill) => skill.name === name)!;

export const seedAgents: AgentResponse[] = [
  {
    id: 'agent-coder',
    name: 'Код-ревьюер',
    description: 'Разбирает код, ищет баги, предлагает рефакторинг',
    icon: '⌘',
    instructions:
      '# Роль\n\nТы ревьюишь код на C# и TypeScript.\n\n' +
      '- Сначала находи ошибки, потом уже стиль.\n' +
      '- На каждое замечание показывай исправленный фрагмент.\n' +
      '- Если код в порядке — так и скажи, не выдумывай замечаний.',
    skills: [skillByName('dotnet-conventions')],
    temperature: 0.3,
    topP: 0.95,
    topK: 40,
    maxOutputTokens: 8192,
    frequencyPenalty: 0,
    presencePenalty: 0,
    reasoningEffortEnum: 'None',
    createdAt: days(9),
    updatedAt: days(2),
  },
  {
    id: 'agent-dba',
    name: 'DBA',
    description: 'Ускоряет запросы к PostgreSQL',
    icon: '◈',
    instructions: '# Роль\n\nТы разбираешь планы выполнения и подбираешь индексы.',
    skills: [skillByName('postgres-review')],
    temperature: 0.2,
    topP: 0.9,
    topK: 40,
    maxOutputTokens: 4096,
    frequencyPenalty: 0,
    presencePenalty: 0,
    reasoningEffortEnum: 'None',
    createdAt: days(4),
    updatedAt: days(1),
  },
];


export const seedChats: ChatResponse[] = [
  {
    id: 'chat-1',
    title: 'Схема БД для истории чатов',
    createdAt: minutes(90),
    updatedAt: minutes(12),
    lastMessagePreview: 'Тогда вынеси вложения в отдельную таблицу с FK на message.',
    pinned: true,
    agentId: 'agent-coder',
  },
  {
    id: 'chat-2',
    title: 'Настройка Keycloak для SPA',
    createdAt: minutes(400),
    updatedAt: minutes(220),
    lastMessagePreview: 'Public client + PKCE S256, secret не нужен.',
    pinned: false,
    agentId: null,
  },
  {
    id: 'chat-3',
    title: 'Идеи для конструктора агентов',
    createdAt: days(1),
    updatedAt: days(1),
    lastMessagePreview: 'Начни с системного промпта, инструментов и модели.',
    pinned: false,
    agentId: null,
  },
  {
    id: 'chat-4',
    title: 'Разбор отчёта за квартал',
    createdAt: days(4),
    updatedAt: days(3),
    lastMessagePreview: 'Выручка выросла на 12%, основной вклад — второй регион.',
    pinned: false,
    agentId: null,
  },
  {
    id: 'chat-5',
    title: 'Result pattern vs исключения',
    createdAt: days(12),
    updatedAt: days(11),
    lastMessagePreview: 'Исключения оставь для того, что действительно исключительно.',
    pinned: false,
    agentId: 'agent-coder',
  },
];

export const seedMessages: Record<string, MessageResponse[]> = {
  'chat-1': [
    {
      id: 'msg-1-1',
      chatId: 'chat-1',
      role: 'user',
      content:
        'Проектирую хранение истории чатов в Postgres. Сейчас думаю держать вложения прямо в таблице сообщений в jsonb. Норм идея?',
      createdAt: minutes(90),
      status: 'complete',
      attachments: [],
    },
    {
      id: 'msg-1-2',
      chatId: 'chat-1',
      role: 'assistant',
      agentId: 'agent-coder',
      content: `Идея рабочая, но у неё есть цена. Разберём по пунктам.

**Что ломается с jsonb**

1. Нельзя навесить FK и каскадное удаление на файлы в блоб-хранилище.
2. Поиск по имени файла превращается в скан с распаковкой json.
3. Любое изменение формы вложения — это миграция данных, а не схемы.

**Что предлагаю**

\`\`\`sql
create table attachments (
    id           uuid primary key,
    message_id   uuid not null references messages (id) on delete cascade,
    file_name    text not null,
    content_type text not null,
    size_bytes   bigint not null,
    storage_key  text not null,
    created_at   timestamptz not null default now()
);

create index ix_attachments_message_id on attachments (message_id);
\`\`\`

Тогда вынеси вложения в отдельную таблицу с FK на message.`,
      createdAt: minutes(12),
      status: 'complete',
      attachments: [],
    },
  ],

  'chat-2': [
    {
      id: 'msg-2-1',
      chatId: 'chat-2',
      role: 'user',
      content: 'Какой тип клиента заводить в Keycloak для Vue-приложения?',
      createdAt: minutes(400),
      status: 'complete',
      attachments: [],
    },
    {
      id: 'msg-2-2',
      chatId: 'chat-2',
      role: 'assistant',
      agentId: null,
      content: `Для SPA — **public client** с Authorization Code Flow и PKCE.

| Параметр | Значение |
| --- | --- |
| Client authentication | Off |
| Standard flow | On |
| Direct access grants | Off |
| PKCE method | S256 |

Public client + PKCE S256, secret не нужен.`,
      createdAt: minutes(220),
      status: 'complete',
      attachments: [],
    },
  ],

  'chat-3': [
    {
      id: 'msg-3-1',
      chatId: 'chat-3',
      role: 'user',
      content: 'Что минимально должно быть в конструкторе агентов?',
      createdAt: days(1),
      status: 'complete',
      attachments: [],
    },
    {
      id: 'msg-3-2',
      chatId: 'chat-3',
      role: 'assistant',
      agentId: null,
      content: `Минимальный набор, с которого можно стартовать:

- **Имя и иконка** — чтобы отличать агентов в списке.
- **Системный промпт** — главное, что отличает одного агента от другого.
- **Модель и температура** — параметры генерации.
- **Инструменты** — список включённых tool'ов.
- **Доступ** — личный агент или общий для команды.

Начни с системного промпта, инструментов и модели. Остальное нарастишь позже.`,
      createdAt: days(1),
      status: 'complete',
      attachments: [],
    },
  ],

  'chat-4': [
    {
      id: 'msg-4-1',
      chatId: 'chat-4',
      role: 'user',
      content: 'Посмотри отчёт, что там по выручке?',
      createdAt: days(4),
      status: 'complete',
      attachments: [
        {
          id: 'att-4-1',
          fileName: 'quarterly-report.pdf',
          contentType: 'application/pdf',
          size: 428_112,
          url: null,
        },
      ],
    },
    {
      id: 'msg-4-2',
      chatId: 'chat-4',
      role: 'assistant',
      agentId: null,
      content:
        'Выручка выросла на 12% квартал к кварталу. Основной вклад дал второй регион (+31%), первый почти не изменился (+2%), третий просел на 4%.',
      createdAt: days(3),
      status: 'complete',
      attachments: [],
    },
  ],

  'chat-5': [
    {
      id: 'msg-5-1',
      chatId: 'chat-5',
      role: 'user',
      content: 'Стоит ли везде возвращать Result вместо исключений?',
      createdAt: days(12),
      status: 'complete',
      attachments: [],
    },
    {
      id: 'msg-5-2',
      chatId: 'chat-5',
      role: 'assistant',
      agentId: 'agent-coder',
      content:
        'Result хорош для ожидаемых сценариев: не найдено, нет прав, не прошла валидация. Исключения оставь для того, что действительно исключительно — потеря соединения с БД, битая конфигурация, нарушенный инвариант домена.',
      createdAt: days(11),
      status: 'complete',
      attachments: [],
    },
  ],
};
