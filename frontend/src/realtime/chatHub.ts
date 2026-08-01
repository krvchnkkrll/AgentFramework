/**
 * Общее SignalR-подключение к ChatHub (Web/Hubs/ChatHub.cs).
 *
 * Стриминг ответа ассистента и живое обновление названия чата идут через него,
 * а не через REST/SSE — сервер сам решает, когда и что прислать.
 */
import * as signalR from '@microsoft/signalr';

import { getAccessToken } from '@/auth/auth';

/** Форма ответа Application.Contracts/Features/Chats/Responses/MessageResponse.cs. */
export interface HubMessageResponse {
  id: string;
  roleEnum: 'System' | 'User' | 'Assistant' | 'Tool';
  text: string;
  createdAt: string;
}

interface MessageStartedPayload {
  chatId: string;
  messageId: string;
}

interface MessageDeltaPayload {
  chatId: string;
  messageId: string;
  delta: string;
}

interface MessageCompletedPayload {
  chatId: string;
  message: HubMessageResponse;
}

interface ChatRenamedPayload {
  chatId: string;
  title: string;
}

let connectionPromise: Promise<signalR.HubConnection> | null = null;
const joinedChats = new Set<string>();

function getConnection(): Promise<signalR.HubConnection> {
  connectionPromise ??= (async () => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chat', {
        accessTokenFactory: async () => (await getAccessToken()) ?? '',
      })
      .withAutomaticReconnect()
      .build();

    // При реконнекте сервер забывает, в каких группах была эта connection —
    // перевступаем во все чаты, к которым уже подключались.
    connection.onreconnected(() => {
      for (const chatId of joinedChats) void connection.invoke('JoinChat', chatId);
    });

    await connection.start();
    return connection;
  })();

  return connectionPromise;
}

/** Идемпотентно: повторный вызов для того же чата ничего не делает. */
export async function joinChat(chatId: string): Promise<void> {
  const connection = await getConnection();
  if (joinedChats.has(chatId)) return;

  await connection.invoke('JoinChat', chatId);
  joinedChats.add(chatId);
}

/**
 * Ждёт событие о том, что ассистент начал отвечать, и возвращает id сообщения —
 * бэкенд генерирует его сам асинхронно, до этого момента фронт его не знает.
 */
export async function waitForMessageStarted(chatId: string, timeoutMs = 10_000): Promise<string> {
  const connection = await getConnection();

  return new Promise<string>((resolve, reject) => {
    const timeout = window.setTimeout(() => {
      connection.off('messageStarted', handler);
      reject(new Error('Ассистент не начал отвечать вовремя.'));
    }, timeoutMs);

    function handler(payload: MessageStartedPayload): void {
      if (payload.chatId !== chatId) return;
      window.clearTimeout(timeout);
      connection.off('messageStarted', handler);
      resolve(payload.messageId);
    }

    connection.on('messageStarted', handler);
  });
}

/** Копит дельты для одного сообщения и резолвится финальным MessageResponse. */
export async function streamMessage(
  chatId: string,
  messageId: string,
  onDelta: (delta: string) => void,
  signal?: AbortSignal,
): Promise<HubMessageResponse> {
  const connection = await getConnection();

  return new Promise<HubMessageResponse>((resolve, reject) => {
    const cleanup = (): void => {
      connection.off('messageDelta', onDeltaReceived);
      connection.off('messageCompleted', onCompleted);
      signal?.removeEventListener('abort', onAbort);
    };

    function onDeltaReceived(payload: MessageDeltaPayload): void {
      if (payload.chatId !== chatId || payload.messageId !== messageId) return;
      onDelta(payload.delta);
    }

    function onCompleted(payload: MessageCompletedPayload): void {
      if (payload.chatId !== chatId || payload.message.id !== messageId) return;
      cleanup();
      resolve(payload.message);
    }

    function onAbort(): void {
      cleanup();
      reject(new DOMException('Aborted', 'AbortError'));
    }

    connection.on('messageDelta', onDeltaReceived);
    connection.on('messageCompleted', onCompleted);
    signal?.addEventListener('abort', onAbort);
  });
}

/** Живое переименование чата (например, когда мок-ассистент назвал его по первому сообщению). */
export function onChatRenamed(handler: (chatId: string, title: string) => void): () => void {
  let unsubscribe: (() => void) | null = null;
  let disposed = false;

  void getConnection().then((connection) => {
    if (disposed) return;

    const wrapped = (payload: ChatRenamedPayload): void => handler(payload.chatId, payload.title);
    connection.on('chatRenamed', wrapped);
    unsubscribe = () => connection.off('chatRenamed', wrapped);
  });

  return () => {
    disposed = true;
    unsubscribe?.();
  };
}
