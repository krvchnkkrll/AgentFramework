/**
 * Реальная реализация ChatsApi.
 *
 * Бэкенд реализует чаты и сообщения (см. Web/Controllers/ConversationController.cs),
 * но в другой форме, чем изначально предполагалось здесь: список без lastMessagePreview,
 * PATCH только для title (pin/unpin — отдельные POST-эндпоинты), MessageResponse
 * с полями roleEnum/text вместо role/content, без вложений.
 * Вся эта разница транслируется тут — контракт ChatsApi и компоненты не меняются.
 *
 * Ответ ассистента генерирует LLM (Assistent/Agents/DefaultAgent.cs, пайплайн —
 * Application/Services/GenerationRegistryService.cs) и стримится не по HTTP/SSE, а по SignalR
 * (см. @/realtime/chatHub): sendMessage дожидается события messageStarted, чтобы узнать id
 * ещё не сохранённого сообщения, а streamAssistantMessage слушает
 * messageDelta/messageCompleted/messageFailed для этого id.
 *
 * Реализованные маршруты:
 *   GET    /api/chats
 *   POST   /api/chats                      { title }
 *   PATCH  /api/chats/{chatId}             { title }
 *   POST   /api/chats/{chatId}/pin
 *   POST   /api/chats/{chatId}/unpin
 *   DELETE /api/chats/{chatId}
 *   POST   /api/chats/{chatId}/stop
 *   GET    /api/chats/{chatId}/messages
 *   POST   /api/chats/{chatId}/messages    { text, attachmentIds }
 *   POST   /api/attachments                 multipart, только текстовые файлы
 *   DELETE /api/attachments/{id}
 *   WS     /hubs/chat                      messageStarted/messageDelta/messageCompleted/
 *                                          messageFailed/chatRenamed/
 *                                          toolCallStarted/toolCallCompleted
 *
 *   GET    /api/agents
 *   POST   /api/agents
 *   PUT    /api/agents/{agentId}
 *   DELETE /api/agents/{agentId}
 *   GET    /api/agents/skills
 *   PUT    /api/chats/{chatId}/agent      { agentId }
 *
 * Вложения пока живут в памяти процесса бэкенда: ни БД, ни объектного хранилища за ними нет,
 * после перезапуска приложения они пропадают.
 */
import type { ChatsApi, SendMessageResult, StreamHandlers } from './contract';
import { ApiError, request } from './http';
import type {
  AgentResponse,
  AttachmentResponse,
  ChatResponse,
  CreateChatRequest,
  MessageResponse,
  MessageRole,
  SaveAgentRequest,
  SendMessageRequest,
  SkillResponse,
  UpdateChatRequest,
} from './types';
import { joinChat, streamMessage, waitForMessageStarted } from '@/realtime/chatHub';
import type { HubMessageResponse } from '@/realtime/chatHub';

/** Форма ответа Application.Contracts/Features/Chats/Responses/ChatSummaryResponse.cs. */
interface BackendChatSummaryResponse {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
  agentId: string | null;
}

/** Форма ответа .../Responses/ChatResponse.cs — та же чат-запись, но с сообщениями. */
interface BackendChatResponse extends BackendChatSummaryResponse {
  messages: BackendMessageResponse[];
}

/** Форма ответа .../Responses/MessageResponse.cs (roleEnum — строка, JsonStringEnumConverter). */
type BackendMessageResponse = HubMessageResponse;

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
    agentId: chat.agentId,
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
    agentId: chat.agentId,
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
      // agentId: null — чат отвечает встроенным агентом.
      json: { title, agentId: body.agentId ?? null },
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

    if (body.agentId !== undefined) {
      return realChatsApi.setChatAgent(chatId, body.agentId, signal);
    }

    throw NOT_IMPLEMENTED('Обновление чата без единого поля');
  },

  setChatAgent(chatId: string, agentId: string | null, signal?: AbortSignal): Promise<ChatResponse> {
    return request<BackendChatResponse>(`/api/chats/${chatId}/agent`, {
      method: 'PUT',
      json: { agentId },
      signal,
    }).then(mapChat);
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
    // Обязательно вступаем в группу и начинаем ждать messageStarted ДО POST —
    // бэкенд может прислать событие раньше, чем HTTP-ответ на сам POST вернётся.
    await joinChat(chatId);
    const startedPromise = waitForMessageStarted(chatId);

    const userMessage = await request<BackendMessageResponse>(`/api/chats/${chatId}/messages`, {
      method: 'POST',
      // Именно здесь загруженные документы привязываются к чату и становятся видны агенту.
      json: { text: body.content, attachmentIds: body.attachmentIds },
      signal,
    }).then((message) => mapMessage(message, chatId));

    // Ответ ассистента стримится по SignalR отдельно от этого запроса — id сообщения
    // узнаём из messageStarted.
    const assistantMessageId = await startedPromise;

    const assistantMessage: MessageResponse = {
      id: assistantMessageId,
      chatId,
      role: 'assistant',
      content: '',
      createdAt: new Date().toISOString(),
      status: 'streaming',
      attachments: [],
    };

    return { userMessage, assistantMessage };
  },

  streamAssistantMessage(
    chatId: string,
    messageId: string,
    handlers: StreamHandlers,
    signal?: AbortSignal,
  ): Promise<MessageResponse> {
    return streamMessage(chatId, messageId, handlers, signal).then((message) =>
      mapMessage(message, chatId),
    );
  },

  stopGeneration(chatId: string, signal?: AbortSignal): Promise<void> {
    return request<void>(`/api/chats/${chatId}/stop`, { method: 'POST', signal });
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

  listSkills(signal) {
    return request<SkillResponse[]>('/api/agents/skills', { signal });
  },

  createAgent(body: SaveAgentRequest, signal) {
    return request<AgentResponse>('/api/agents', { method: 'POST', json: body, signal });
  },

  updateAgent(agentId: string, body: SaveAgentRequest, signal) {
    return request<AgentResponse>(`/api/agents/${agentId}`, { method: 'PUT', json: body, signal });
  },

  deleteAgent(agentId: string, signal) {
    return request<void>(`/api/agents/${agentId}`, { method: 'DELETE', signal });
  },
};
