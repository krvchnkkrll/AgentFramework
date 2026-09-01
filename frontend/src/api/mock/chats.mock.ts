/**
 * Мок-реализация ChatsApi: всё живёт в памяти вкладки.
 *
 * Включается флагом VITE_USE_MOCKS=true. Данные сохраняются в localStorage,
 * чтобы переписка не пропадала при перезагрузке страницы.
 * Когда появятся настоящие эндпоинты — просто поставь VITE_USE_MOCKS=false,
 * этот файл можно будет удалить целиком.
 */
import type { ChatsApi, SendMessageResult, StreamHandlers } from '../contract';
import type {
  AgentResponse,
  AttachmentResponse,
  ChatResponse,
  CreateChatRequest,
  MessageResponse,
  SaveAgentRequest,
  SendMessageRequest,
  SkillResponse,
  UpdateChatRequest,
} from '../types';
import { seedAgents, seedChats, seedMessages, seedSkills } from './seed';

const STORAGE_KEY = 'af.mock.db.v1';

interface MockDb {
  chats: ChatResponse[];
  messages: Record<string, MessageResponse[]>;
  agents: AgentResponse[];
  skills: SkillResponse[];
}

/** Вложения держим отдельно: blob:-ссылки живут только в текущей вкладке. */
const attachments = new Map<string, { meta: AttachmentResponse; file: File }>();

/** Черновики ответов, которые ждут вызова streamAssistantMessage. */
const pendingReplies = new Map<string, string>();

const db: MockDb = loadDb();

function loadDb(): MockDb {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as MockDb;
      // agents появились позже — у старых сохранений их нет, добираем из сидов.
      if (Array.isArray(parsed.chats) && parsed.messages) {
        parsed.agents ??= structuredClone(seedAgents);
        parsed.skills ??= structuredClone(seedSkills);
        return parsed;
      }
    }
  } catch {
    // повреждённый стейт — просто пересоздаём
  }

  return {
    chats: structuredClone(seedChats),
    messages: structuredClone(seedMessages),
    agents: structuredClone(seedAgents),
    skills: structuredClone(seedSkills),
  };
}

function persist(): void {
  try {
    // Вложения не сериализуем: blob-ссылки всё равно протухнут.
    const serialisable: MockDb = {
      chats: db.chats,
      agents: db.agents,
      skills: db.skills,
      messages: Object.fromEntries(
        Object.entries(db.messages).map(([chatId, list]) => [
          chatId,
          list.map((m) => ({ ...m, attachments: m.attachments.map((a) => ({ ...a, url: null })) })),
        ]),
      ),
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(serialisable));
  } catch {
    // quota exceeded — не критично для мока
  }
}

const delay = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

/**
 * Изображает работу инструментов перед ответом: на реальном бэкенде агент действительно
 * может уйти на десятки секунд в load_skill или grep, и именно эту паузу показывает UI.
 * Последний вызов намеренно падает — иначе состояние ошибки негде проверить.
 */
async function simulateToolCalls(handlers: StreamHandlers, signal?: AbortSignal): Promise<void> {
  const script: { name: string; arguments: string | null; ms: number; error: string | null }[] = [
    { name: 'get_current_time', arguments: '{"timeZone":"Europe/Moscow"}', ms: 500, error: null },
    { name: 'load_skill', arguments: '{"name":"postgres-review"}', ms: 1400, error: null },
    {
      name: 'search_knowledge_base',
      arguments: '{"query":"регламент выката"}',
      ms: 900,
      error: 'Индекс knowledge недоступен.',
    },
  ];

  for (const step of script) {
    if (signal?.aborted) return;

    const id = newId();
    handlers.onToolCallStarted?.({ id, name: step.name, arguments: step.arguments });

    await delay(step.ms);
    if (signal?.aborted) return;

    handlers.onToolCallCompleted?.(id, step.error);
  }
}
const newId = () => crypto.randomUUID();

function requireChat(chatId: string): ChatResponse {
  const chat = db.chats.find((c) => c.id === chatId);
  if (!chat) throw new Error(`Чат ${chatId} не найден.`);
  return chat;
}

function preview(content: string): string {
  const clean = content.replace(/\s+/g, ' ').trim();
  return clean.length <= 90 ? clean : `${clean.slice(0, 90).trimEnd()}…`;
}

/** Правдоподобный ответ ассистента — форма важнее содержания. */
function composeReply(question: string, attached: AttachmentResponse[]): string {
  const parts: string[] = [];

  if (attached.length > 0) {
    const list = attached.map((a) => `\`${a.fileName}\``).join(', ');
    parts.push(`Принял вложения: ${list}. Разбор будет, когда появится реальный бэкенд.`);
  }

  parts.push(
    `Это ответ мок-слоя — настоящая модель пока не подключена. Вот что ты спросил:\n\n> ${preview(question)}`,
  );

  parts.push(
    `**Что уже работает во фронте**

- список чатов, поиск, закрепление, переименование и удаление;
- отправка сообщений с построчным «печатанием» ответа;
- вложения с предпросмотром картинок и drag & drop;
- переключение тем и авторизация через Keycloak.

**Что подключается со стороны бэкенда**

\`\`\`http
GET  /api/chats
POST /api/chats/{chatId}/messages
GET  /api/chats/{chatId}/messages/{messageId}/stream
\`\`\`

Как эти маршруты появятся — поставь \`VITE_USE_MOCKS=false\` в \`.env.local\`, и фронт пойдёт в реальный API без правок в компонентах.`,
  );

  return parts.join('\n\n');
}

export const mockChatsApi: ChatsApi = {
  async listChats(): Promise<ChatResponse[]> {
    await delay(180);
    return structuredClone(db.chats);
  },

  async createChat(body: CreateChatRequest): Promise<ChatResponse> {
    await delay(120);

    const timestamp = new Date().toISOString();
    const chat: ChatResponse = {
      id: newId(),
      title: body.title?.trim() || 'Новый чат',
      createdAt: timestamp,
      updatedAt: timestamp,
      lastMessagePreview: null,
      pinned: false,
      // null — чат отвечает встроенным агентом, ровно как на бэкенде.
      agentId: body.agentId ?? null,
    };

    db.chats.unshift(chat);
    db.messages[chat.id] = [];
    persist();

    return structuredClone(chat);
  },

  async updateChat(chatId: string, body: UpdateChatRequest): Promise<ChatResponse> {
    await delay(90);

    const chat = requireChat(chatId);
    if (body.title !== undefined) chat.title = body.title.trim() || chat.title;
    if (body.pinned !== undefined) chat.pinned = body.pinned;
    if (body.agentId !== undefined) chat.agentId = body.agentId;
    chat.updatedAt = new Date().toISOString();
    persist();

    return structuredClone(chat);
  },

  async deleteChat(chatId: string): Promise<void> {
    await delay(90);

    db.chats = db.chats.filter((c) => c.id !== chatId);
    delete db.messages[chatId];
    persist();
  },

  async listMessages(chatId: string): Promise<MessageResponse[]> {
    await delay(150);
    return structuredClone(db.messages[chatId] ?? []);
  },

  async sendMessage(chatId: string, body: SendMessageRequest): Promise<SendMessageResult> {
    await delay(120);

    const chat = requireChat(chatId);
    const list = (db.messages[chatId] ??= []);

    const attached = body.attachmentIds
      .map((id) => attachments.get(id)?.meta)
      .filter((meta): meta is AttachmentResponse => meta !== undefined);

    const userMessage: MessageResponse = {
      id: newId(),
      chatId,
      role: 'user',
      content: body.content,
      createdAt: new Date().toISOString(),
      status: 'complete',
      attachments: attached,
    };

    const assistantMessage: MessageResponse = {
      id: newId(),
      chatId,
      role: 'assistant',
      agentId: chat.agentId,
      content: '',
      createdAt: new Date().toISOString(),
      status: 'streaming',
      attachments: [],
    };

    list.push(userMessage, assistantMessage);

    chat.updatedAt = userMessage.createdAt;
    chat.lastMessagePreview = preview(body.content);
    persist();

    pendingReplies.set(assistantMessage.id, composeReply(body.content, attached));

    return {
      userMessage: structuredClone(userMessage),
      assistantMessage: structuredClone(assistantMessage),
    };
  },

  async streamAssistantMessage(
    chatId: string,
    messageId: string,
    handlers: StreamHandlers,
    signal?: AbortSignal,
  ): Promise<MessageResponse> {
    const list = db.messages[chatId] ?? [];
    const message = list.find((m) => m.id === messageId);
    if (!message) throw new Error(`Сообщение ${messageId} не найдено.`);

    const full = pendingReplies.get(messageId) ?? '';
    pendingReplies.delete(messageId);

    // Режем на «токены» вместе с пробелами, чтобы текст собирался обратно один в один.
    const tokens = full.match(/\S+\s*/g) ?? [];

    await delay(420); // «агент думает»

    await simulateToolCalls(handlers, signal);

    let content = '';
    for (const token of tokens) {
      if (signal?.aborted) break;

      content += token;
      handlers.onDelta(token);
      await delay(token.includes('\n') ? 34 : 18);
    }

    // Прервали на середине — сохраняем то, что успело «напечататься».
    message.content = content;
    message.status = 'complete';

    const chat = db.chats.find((c) => c.id === chatId);
    if (chat) {
      chat.updatedAt = new Date().toISOString();
      chat.lastMessagePreview = preview(content);
    }
    persist();

    return structuredClone(message);
  },

  // В моке ответ «печатается» прямо в браузере и обрывается по AbortSignal —
  // останавливать на сервере нечего.
  async stopGeneration(): Promise<void> {},

  async uploadAttachment(file: File): Promise<AttachmentResponse> {
    // Имитируем сетевую задержку пропорционально размеру файла.
    await delay(Math.min(1200, 260 + file.size / 20_000));

    const meta: AttachmentResponse = {
      id: newId(),
      fileName: file.name,
      contentType: file.type || 'application/octet-stream',
      size: file.size,
      url: URL.createObjectURL(file),
    };

    attachments.set(meta.id, { meta, file });
    return { ...meta };
  },

  async deleteAttachment(attachmentId: string): Promise<void> {
    const stored = attachments.get(attachmentId);
    if (stored?.meta.url) URL.revokeObjectURL(stored.meta.url);
    attachments.delete(attachmentId);
  },

  async listAgents(): Promise<AgentResponse[]> {
    await delay(80);
    return structuredClone(db.agents);
  },

  async listSkills(): Promise<SkillResponse[]> {
    await delay(60);
    return structuredClone(db.skills);
  },

  /**
   * Мок не разбирает архив — просто заводит карточку по имени файла. На бэкенде имя
   * и описание берутся из frontmatter внутри SKILL.md.
   */
  async uploadSkill(file: File): Promise<SkillResponse> {
    await delay(240);

    const name = file.name.replace(/\.zip$/i, '');

    if (db.skills.some((skill) => skill.name === name)) {
      throw new Error(`Скилл «${name}» уже загружен.`);
    }

    const now = new Date().toISOString();
    const skill: SkillResponse = {
      id: newId(),
      name,
      description: `Скилл, загруженный из ${file.name}.`,
      sizeBytes: file.size,
      hasScripts: false,
      shared: false,
      createdAt: now,
      updatedAt: now,
    };

    db.skills.push(skill);
    persist();

    return structuredClone(skill);
  },

  async deleteSkill(skillId: string): Promise<void> {
    await delay(150);

    db.skills = db.skills.filter((skill) => skill.id !== skillId);

    // Удалённый скилл пропадает и у агентов — на бэкенде это делает каскад.
    for (const agent of db.agents) {
      agent.skills = agent.skills.filter((skill) => skill.id !== skillId);
    }

    persist();
  },

  async createAgent(body: SaveAgentRequest): Promise<AgentResponse> {
    await delay(180);

    const now = new Date().toISOString();
    // Форма присылает идентификаторы, а наружу агент отдаётся с карточками скиллов —
    // ровно как на бэкенде.
    const { skillIds, ...rest } = body;
    const agent: AgentResponse = {
      ...rest,
      skills: resolveSkills(skillIds),
      id: newId(),
      createdAt: now,
      updatedAt: now,
    };

    db.agents.push(agent);
    persist();

    return structuredClone(agent);
  },

  async updateAgent(agentId: string, body: SaveAgentRequest): Promise<AgentResponse> {
    await delay(180);

    const agent = db.agents.find((a) => a.id === agentId);
    if (!agent) throw new Error(`Агент ${agentId} не найден.`);

    const { skillIds, ...rest } = body;
    Object.assign(agent, rest, {
      skills: resolveSkills(skillIds),
      updatedAt: new Date().toISOString(),
    });
    persist();

    return structuredClone(agent);
  },

  async deleteAgent(agentId: string): Promise<void> {
    await delay(150);

    db.agents = db.agents.filter((a) => a.id !== agentId);

    // Чаты удалённого агента не пропадают — они возвращаются к встроенному, как и на бэкенде.
    for (const chat of db.chats) {
      if (chat.agentId === agentId) chat.agentId = null;
    }

    persist();
  },

  async setChatAgent(chatId: string, agentId: string | null): Promise<ChatResponse> {
    await delay(120);

    const chat = db.chats.find((c) => c.id === chatId);
    if (!chat) throw new Error(`Чат ${chatId} не найден.`);

    chat.agentId = agentId;
    chat.updatedAt = new Date().toISOString();
    persist();

    return structuredClone(chat);
  },
};

/** Карточки выбранных скиллов по их идентификаторам. Несуществующие отбрасываются. */
function resolveSkills(skillIds: string[]): SkillResponse[] {
  return skillIds
    .map((id) => db.skills.find((skill) => skill.id === id))
    .filter((skill): skill is SkillResponse => skill !== undefined)
    .map((skill) => structuredClone(skill));
}

/** Сбросить мок-данные к исходным. Вызывается из настроек. */
export function resetMockDb(): void {
  localStorage.removeItem(STORAGE_KEY);
  db.chats = structuredClone(seedChats);
  db.messages = structuredClone(seedMessages);
  db.skills = structuredClone(seedSkills);
  persist();
}
