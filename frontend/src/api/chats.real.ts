/**
 * Реальная реализация ChatsApi.
 *
 * Бэкенд реализует чаты и сообщения (см. Web/Controllers/ConversationController.cs),
 * но в другой форме, чем изначально предполагалось здесь: список без lastMessagePreview,
 * PATCH только для title (pin/unpin — отдельные POST-эндпоинты), MessageResponse
 * с полями roleEnum/text вместо role/content, без ассистента/вложений/агентов.
 * Вся эта разница транслируется тут — контракт ChatsApi и компоненты не меняются.
 *
 * Реализованные маршруты:
 *   GET    /api/chats
 *   POST   /api/chats                      { title }
 *   PATCH  /api/chats/{chatId}             { title }
 *   POST   /api/chats/{chatId}/pin
 *   POST   /api/chats/{chatId}/unpin
 *   DELETE /api/chats/{chatId}
 *   GET    /api/chats/{chatId}/messages
 *   POST   /api/chats/{chatId}/messages    { text }
 *
 * Ещё не реализованы на бэкенде (см. описания у методов ниже, деградируют мягко
 * через ApiError.isNotImplemented): ответ ассистента и его стриминг, вложения, агенты.
 */
import type { ChatsApi, DeltaHandler, SendMessageResult } from './contract';
import { ApiError, request } from './http';
import type {
  AgentResponse,
  AttachmentResponse,
  ChatResponse,
  CreateChatRequest,
  MessageResponse,
  MessageRole,
  SendMessageRequest,
  UpdateChatRequest,
} from './types';

/** Форма ответа Application.Contracts/Features/Chats/Responses/ChatSummaryResponse.cs. */
interface BackendChatSummaryResponse {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
}

/** Форма ответа .../Responses/ChatResponse.cs — та же чат-запись, но с сообщениями. */
interface BackendChatResponse extends BackendChatSummaryResponse {
  messages: BackendMessageResponse[];
}

/** Форма ответа .../Responses/MessageResponse.cs (roleEnum — строка, JsonStringEnumConverter). */
interface BackendMessageResponse {
  id: string;
  roleEnum: 'System' | 'User' | 'Assistant' | 'Tool';
  text: string;
  createdAt: string;
}

function mapRole(roleEnum: BackendMessageResponse['roleEnum']): MessageRole {
  switch (roleEnum) {
    case 'User':
      return 'user';
    case 'System':
      return 'system';
    case 'Assistant':
    case 'Tool':
      // Фронт не различает assistant/tool отдельным пузырём.
      return 'assistant';
  }
}

function mapMessage(message: BackendMessageResponse, chatId: string): MessageResponse {
  return {
    id: message.id,
    chatId,
    role: mapRole(message.roleEnum),
    content: message.text,
    createdAt: message.createdAt,
    status: 'complete',
    attachments: [],
  };
}

function previewOf(messages: BackendMessageResponse[]): string | null {
  const last = messages.at(-1);
  if (!last) return null;
  return last.text.replace(/\s+/g, ' ').trim().slice(0, 90);
}

function mapChatSummary(chat: BackendChatSummaryResponse): ChatResponse {
  return {
    id: chat.id,
    title: chat.title,
    createdAt: chat.createdAt,
    updatedAt: chat.updatedAt,
    // Список отдаёт GET /api/chats без сообщений — превью пока взять неоткуда.
    lastMessagePreview: null,
    pinned: chat.isPinned,
    agentId: null,
  };
}

function mapChat(chat: BackendChatResponse): ChatResponse {
  return {
    id: chat.id,
    title: chat.title,
    createdAt: chat.createdAt,
    updatedAt: chat.updatedAt,
    lastMessagePreview: previewOf(chat.messages),
    pinned: chat.isPinned,
    agentId: null,
  };
}

const NOT_IMPLEMENTED = (feature: string) =>
  new ApiError(501, `${feature} ещё не реализовано на бэкенде.`, 'NotImplemented');

export const realChatsApi: ChatsApi = {
  listChats(signal) {
    return request<BackendChatSummaryResponse[]>('/api/chats', { signal }).then((chats) =>
      chats.map(mapChatSummary),
    );
  },

  createChat(body: CreateChatRequest, signal) {
    const title = body.title?.trim() || 'Новый чат';

    return request<BackendChatResponse>('/api/chats', {
      method: 'POST',
      json: { title },
      signal,
    }).then(mapChat);
  },

  async updateChat(chatId: string, body: UpdateChatRequest, signal): Promise<ChatResponse> {
    if (body.title !== undefined) {
      return mapChat(
        await request<BackendChatResponse>(`/api/chats/${chatId}`, {
          method: 'PATCH',
          json: { title: body.title },
          signal,
        }),
      );
    }

    if (body.pinned !== undefined) {
      const action = body.pinned ? 'pin' : 'unpin';
      return mapChat(
        await request<BackendChatResponse>(`/api/chats/${chatId}/${action}`, {
          method: 'POST',
          signal,
        }),
      );
    }

    // Только agentId без title/pinned — сменить агента у чата бэкенд пока не умеет.
    throw NOT_IMPLEMENTED('Выбор агента');
  },

  deleteChat(chatId: string, signal) {
    return request<void>(`/api/chats/${chatId}`, { method: 'DELETE', signal });
  },

  listMessages(chatId: string, signal) {
    return request<BackendMessageResponse[]>(`/api/chats/${chatId}/messages`, { signal }).then(
      (messages) => messages.map((message) => mapMessage(message, chatId)),
    );
  },

  async sendMessage(chatId: string, body: SendMessageRequest, signal): Promise<SendMessageResult> {
    // attachmentIds игнорируются: вложений на бэкенде ещё нет.
    const userMessage = await request<BackendMessageResponse>(`/api/chats/${chatId}/messages`, {
      method: 'POST',
      json: { text: body.content },
      signal,
    }).then((message) => mapMessage(message, chatId));

    // На бэкенде нет агента, который бы ответил, — отдаём «печатающуюся» болванку.
    // streamAssistantMessage ниже сразу провалит её в status: 'failed' понятной ошибкой
    // вместо того, чтобы стор вечно ждал ответа.
    const assistantMessage: MessageResponse = {
      id: `pending-${userMessage.id}`,
      chatId,
      role: 'assistant',
      content: '',
      createdAt: new Date().toISOString(),
      status: 'streaming',
      attachments: [],
    };

    return { userMessage, assistantMessage };
  },

  streamAssistantMessage(_chatId: string, _messageId: string, _onDelta: DeltaHandler): Promise<MessageResponse> {
    return Promise.reject(NOT_IMPLEMENTED('Ответ ассистента'));
  },

  uploadAttachment(file: File, signal?: AbortSignal): Promise<AttachmentResponse> {
    const form = new FormData();
    form.append('file', file, file.name);

    return request<AttachmentResponse>('/api/attachments', {
      method: 'POST',
      body: form,
      signal,
    });
  },

  deleteAttachment(attachmentId: string, signal) {
    return request<void>(`/api/attachments/${attachmentId}`, { method: 'DELETE', signal });
  },

  listAgents(signal) {
    return request<AgentResponse[]>('/api/agents', { signal });
  },
};
