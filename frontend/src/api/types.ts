/**
 * Контракты API.
 *
 * UserResponse — 1:1 с Application.Contracts/Features/Users/Responses/UserResponse.cs.
 * Остальное — предполагаемая форма будущих эндпоинтов; когда напишешь бэкенд,
 * правь эти типы, и TypeScript сам покажет все места, которые надо поправить.
 */

/** GET /api/user/current-user */
export interface UserResponse {
  id: string;
  email: string;
  username: string;
  createdAt: string;
  lastSeenAt: string | null;
}

export type MessageRole = 'user' | 'assistant' | 'system';

export type MessageStatus = 'complete' | 'streaming' | 'failed';

export interface AttachmentResponse {
  id: string;
  fileName: string;
  contentType: string;
  /** Размер в байтах. */
  size: number;
  /** URL для скачивания/предпросмотра. У мок-слоя это blob:-ссылка. */
  url: string | null;
}

export interface MessageResponse {
  id: string;
  chatId: string;
  role: MessageRole;
  content: string;
  createdAt: string;
  status: MessageStatus;
  attachments: AttachmentResponse[];
  /** Какой агент/модель отвечал. Пригодится, когда появится конструктор агентов. */
  agentId?: string | null;
  error?: string | null;
}

export interface ChatResponse {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  /** Обрезанный текст последнего сообщения — для списка слева. */
  lastMessagePreview: string | null;
  pinned: boolean;
  agentId: string | null;
}

/** Заглушка под будущий конструктор агентов. */
export interface AgentResponse {
  id: string;
  name: string;
  description: string;
  /** Эмодзи или инициал для аватарки. */
  icon: string;
  builtIn: boolean;
}

/** Тело POST /api/chats */
export interface CreateChatRequest {
  title?: string;
  agentId?: string | null;
}

/** Тело PATCH /api/chats/{id} */
export interface UpdateChatRequest {
  title?: string;
  pinned?: boolean;
  agentId?: string | null;
}

/** Тело POST /api/chats/{id}/messages */
export interface SendMessageRequest {
  content: string;
  attachmentIds: string[];
}

/** Кусочек ответа при стриминге. */
export interface StreamChunk {
  /** Дописываемый фрагмент текста. */
  delta: string;
}

/** Ошибка в формате ProblemDetails (см. AppController.ToProblem). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}
