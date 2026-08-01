/**
 * Мок-реализация ChatsApi: всё живёт в памяти вкладки.
 *
 * Включается флагом VITE_USE_MOCKS=true. Данные сохраняются в localStorage,
 * чтобы переписка не пропадала при перезагрузке страницы.
 * Когда появятся настоящие эндпоинты — просто поставь VITE_USE_MOCKS=false,
 * этот файл можно будет удалить целиком.
 */
import type { ChatsApi, DeltaHandler, SendMessageResult } from '../contract';
import type {
  AgentResponse,
  AttachmentResponse,
  ChatResponse,
  CreateChatRequest,
  MessageResponse,
  SendMessageRequest,
  UpdateChatRequest,
} from '../types';
import { seedAgents, seedChats, seedMessages } from './seed';

const STORAGE_KEY = 'af.mock.db.v1';

interface MockDb {
  chats: ChatResponse[];
  messages: Record<string, MessageResponse[]>;
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
      if (Array.isArray(parsed.chats) && parsed.messages) return parsed;
    }
  } catch {
    // повреждённый стейт — просто пересоздаём
  }

  return {
    chats: structuredClone(seedChats),
    messages: structuredClone(seedMessages),
  };
}

function persist(): void {
  try {
    // Вложения не сериализуем: blob-ссылки всё равно протухнут.
    const serialisable: MockDb = {
      chats: db.chats,
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
      agentId: body.agentId ?? seedAgents[0].id,
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
    onDelta: DeltaHandler,
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

    let content = '';
    for (const token of tokens) {
      if (signal?.aborted) break;

      content += token;
      onDelta(token);
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
    return structuredClone(seedAgents);
  },
};

/** Сбросить мок-данные к исходным. Вызывается из настроек. */
export function resetMockDb(): void {
  localStorage.removeItem(STORAGE_KEY);
  db.chats = structuredClone(seedChats);
  db.messages = structuredClone(seedMessages);
  persist();
}
