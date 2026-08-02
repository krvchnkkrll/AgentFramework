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
  SaveAgentRequest,
  SendMessageRequest,
  SkillResponse,
  UpdateChatRequest,
} from './types';

/** Колбэк, в который прилетают куски ответа ассистента. */
export type DeltaHandler = (delta: string) => void;

/** Вызов инструмента в том виде, в каком он приходит с сервера (без клиентского status). */
export interface ToolCallEvent {
  id: string;
  name: string;
  arguments: string | null;
}

/**
 * Колбэки на события одной генерации. Обязателен только onDelta: остальное подписывается
 * по желанию, поэтому мок может ничего не эмитить и всё продолжит работать.
 */
export interface StreamHandlers {
  onDelta: DeltaHandler;
  /** Агент начал вызывать инструмент — текста в этот момент ещё нет. */
  onToolCallStarted?: (toolCall: ToolCallEvent) => void;
  /** Инструмент отработал; error не пустой, если он упал. */
  onToolCallCompleted?: (toolCallId: string, error: string | null) => void;
}

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
   * Дочитывает ответ ассистента по кусочкам и сообщает о вызовах инструментов.
   * Возвращает финальное состояние сообщения.
   */
  streamAssistantMessage(
    chatId: string,
    messageId: string,
    handlers: StreamHandlers,
    signal?: AbortSignal,
  ): Promise<MessageResponse>;

  /**
   * Просит сервер прекратить генерацию ответа в чате.
   * Без этого abort на клиенте только перестаёт слушать — модель продолжает считать,
   * а чат остаётся занятым и не принимает новые сообщения.
   */
  stopGeneration(chatId: string, signal?: AbortSignal): Promise<void>;

  uploadAttachment(file: File, signal?: AbortSignal): Promise<AttachmentResponse>;
  deleteAttachment(attachmentId: string, signal?: AbortSignal): Promise<void>;

  listAgents(signal?: AbortSignal): Promise<AgentResponse[]>;

  /** Скиллы, из которых собирается агент. Список приходит с бэкенда, а не хранится на клиенте. */
  listSkills(signal?: AbortSignal): Promise<SkillResponse[]>;

  createAgent(request: SaveAgentRequest, signal?: AbortSignal): Promise<AgentResponse>;

  updateAgent(agentId: string, request: SaveAgentRequest, signal?: AbortSignal): Promise<AgentResponse>;

  deleteAgent(agentId: string, signal?: AbortSignal): Promise<void>;

  /** Меняет агента чата. null — вернуть чат встроенному агенту. */
  setChatAgent(chatId: string, agentId: string | null, signal?: AbortSignal): Promise<ChatResponse>;
}
