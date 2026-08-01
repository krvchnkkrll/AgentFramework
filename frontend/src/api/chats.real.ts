/**
 * Реальная реализация ChatsApi.
 *
 * Эндпоинтов пока нет — они появятся, когда ты допишешь бэкенд.
 * Здесь зафиксирована форма, на которую рассчитывает фронт;
 * если на бэкенде получится иначе — правь этот файл, компоненты трогать не придётся.
 *
 * Ожидаемые маршруты:
 *   GET    /api/chats
 *   POST   /api/chats                                  { title?, agentId? }
 *   PATCH  /api/chats/{chatId}                         { title?, pinned?, agentId? }
 *   DELETE /api/chats/{chatId}
 *   GET    /api/chats/{chatId}/messages
 *   POST   /api/chats/{chatId}/messages                { content, attachmentIds }
 *   GET    /api/chats/{chatId}/messages/{id}/stream    text/event-stream
 *   POST   /api/attachments                            multipart/form-data, поле "file"
 *   DELETE /api/attachments/{attachmentId}
 *   GET    /api/agents
 */
import type { ChatsApi, DeltaHandler, SendMessageResult } from './contract';
import { rawRequest, request } from './http';
import type {
  AgentResponse,
  AttachmentResponse,
  ChatResponse,
  CreateChatRequest,
  MessageResponse,
  SendMessageRequest,
  StreamChunk,
  UpdateChatRequest,
} from './types';

export const realChatsApi: ChatsApi = {
  listChats(signal) {
    return request<ChatResponse[]>('/api/chats', { signal });
  },

  createChat(body: CreateChatRequest, signal) {
    return request<ChatResponse>('/api/chats', { method: 'POST', json: body, signal });
  },

  updateChat(chatId: string, body: UpdateChatRequest, signal) {
    return request<ChatResponse>(`/api/chats/${chatId}`, { method: 'PATCH', json: body, signal });
  },

  deleteChat(chatId: string, signal) {
    return request<void>(`/api/chats/${chatId}`, { method: 'DELETE', signal });
  },

  listMessages(chatId: string, signal) {
    return request<MessageResponse[]>(`/api/chats/${chatId}/messages`, { signal });
  },

  sendMessage(chatId: string, body: SendMessageRequest, signal) {
    return request<SendMessageResult>(`/api/chats/${chatId}/messages`, {
      method: 'POST',
      json: body,
      signal,
    });
  },

  async streamAssistantMessage(
    chatId: string,
    messageId: string,
    onDelta: DeltaHandler,
    signal?: AbortSignal,
  ): Promise<MessageResponse> {
    const response = await rawRequest(`/api/chats/${chatId}/messages/${messageId}/stream`, {
      headers: { Accept: 'text/event-stream' },
      signal,
    });

    if (!response.body) {
      throw new Error('Сервер не вернул поток ответа.');
    }

    const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
    let buffer = '';
    let content = '';
    let final: MessageResponse | null = null;

    // Разбираем SSE вручную: EventSource не умеет слать заголовок Authorization.
    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;

      buffer += value;

      let boundary = buffer.indexOf('\n\n');
      while (boundary !== -1) {
        const rawEvent = buffer.slice(0, boundary);
        buffer = buffer.slice(boundary + 2);
        boundary = buffer.indexOf('\n\n');

        const payload = rawEvent
          .split('\n')
          .filter((line) => line.startsWith('data:'))
          .map((line) => line.slice(5).trim())
          .join('\n');

        if (!payload || payload === '[DONE]') continue;

        const parsed = JSON.parse(payload) as StreamChunk | MessageResponse;

        if ('delta' in parsed) {
          content += parsed.delta;
          onDelta(parsed.delta);
        } else {
          final = parsed;
        }
      }
    }

    return (
      final ?? {
        id: messageId,
        chatId,
        role: 'assistant',
        content,
        createdAt: new Date().toISOString(),
        status: 'complete',
        attachments: [],
      }
    );
  },

  async uploadAttachment(file: File, signal?: AbortSignal): Promise<AttachmentResponse> {
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
