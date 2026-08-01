/**
 * Контракт клиента API чатов.
 *
 * Реализаций две — реальная (chats.real.ts) и мок (mock/chats.mock.ts).
 * Компоненты и стор работают только с этим интерфейсом и не знают, какая из них подключена.
 */
import type {
  AgentResponse,
  AttachmentResponse,
  ChatResponse,
  CreateChatRequest,
  MessageResponse,
  SendMessageRequest,
  UpdateChatRequest,
} from './types';

/** Колбэк, в который прилетают куски ответа ассистента. */
export type DeltaHandler = (delta: string) => void;

export interface SendMessageResult {
  /** Сообщение пользователя, каким его сохранил сервер. */
  userMessage: MessageResponse;
  /** Пустая «болванка» ответа ассистента со status: 'streaming'. */
  assistantMessage: MessageResponse;
}

export interface ChatsApi {
  listChats(signal?: AbortSignal): Promise<ChatResponse[]>;
  createChat(request: CreateChatRequest, signal?: AbortSignal): Promise<ChatResponse>;
  updateChat(chatId: string, request: UpdateChatRequest, signal?: AbortSignal): Promise<ChatResponse>;
  deleteChat(chatId: string, signal?: AbortSignal): Promise<void>;

  listMessages(chatId: string, signal?: AbortSignal): Promise<MessageResponse[]>;
  sendMessage(
    chatId: string,
    request: SendMessageRequest,
    signal?: AbortSignal,
  ): Promise<SendMessageResult>;

  /**
   * Дочитывает ответ ассистента по кусочкам.
   * Возвращает финальное состояние сообщения.
   */
  streamAssistantMessage(
    chatId: string,
    messageId: string,
    onDelta: DeltaHandler,
    signal?: AbortSignal,
  ): Promise<MessageResponse>;

  uploadAttachment(file: File, signal?: AbortSignal): Promise<AttachmentResponse>;
  deleteAttachment(attachmentId: string, signal?: AbortSignal): Promise<void>;

  listAgents(signal?: AbortSignal): Promise<AgentResponse[]>;
}
