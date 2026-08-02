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

/** Что сейчас с вызовом инструмента. */
export type ToolCallStatus = 'running' | 'done' | 'failed';

/**
 * Вызов инструмента агентом — 1:1 с Application.Contracts/.../Responses/ToolCallResponse.cs
 * плюс status, который на клиенте выводится из пары событий toolCallStarted/toolCallCompleted.
 *
 * Живёт только на время генерации: бэкенд вызовы инструментов не сохраняет, поэтому после
 * перезагрузки страницы они пропадут, а текст ответа останется.
 */
export interface ToolCallResponse {
  id: string;
  name: string;
  arguments: string | null;
  status: ToolCallStatus;
  error?: string | null;
}

export interface MessageResponse {
  id: string;
  chatId: string;
  role: MessageRole;
  content: string;
  createdAt: string;
  status: MessageStatus;
  attachments: AttachmentResponse[];
  /** Инструменты, которые агент вызвал по ходу этого ответа. */
  toolCalls?: ToolCallResponse[];
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

/** Насколько усердно модель «думает» перед ответом. */
export type ReasoningEffort = 'Default' | 'None' | 'Low' | 'Medium' | 'High';

/** Скилл, доступный для выбора в конструкторе. Читается бэкендом из папок со скиллами. */
export interface SkillResponse {
  name: string;
  description: string;
}

/**
 * Агент из конструктора — 1:1 с Application.Contracts/Features/Agents/Responses/AgentResponse.cs.
 *
 * Встроенный агент сюда не приходит: он живёт в конфигурации бэкенда и не редактируется.
 * На клиенте он представлен константой BUILT_IN_AGENT с builtIn: true.
 */
export interface AgentResponse {
  id: string;
  name: string;
  description: string | null;
  /** Эмодзи или инициал для аватарки. */
  icon: string | null;
  /** Системный промпт в Markdown. */
  instructions: string | null;
  skills: string[];
  temperature: number;
  topP: number;
  topK: number;
  maxOutputTokens: number;
  frequencyPenalty: number;
  presencePenalty: number;
  reasoningEffortEnum: ReasoningEffort;
  createdAt: string;
  updatedAt: string;
  /** true только у псевдоагента, изображающего встроенного. Его нельзя править и удалять. */
  builtIn?: boolean;
}

/** Тело POST /api/agents и PUT /api/agents/{id} — форма конструктора целиком. */
export interface SaveAgentRequest {
  name: string;
  description: string | null;
  icon: string | null;
  instructions: string | null;
  skills: string[];
  temperature: number;
  topP: number;
  topK: number;
  maxOutputTokens: number;
  frequencyPenalty: number;
  presencePenalty: number;
  reasoningEffortEnum: ReasoningEffort;
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
