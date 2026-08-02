/**
 * Точка выбора реализации API.
 *
 * VITE_USE_MOCKS=true  -> данные из мок-слоя в браузере
 * VITE_USE_MOCKS=false -> реальные запросы к ASP.NET Core
 */
import { config } from '@/config';

import { realChatsApi } from './chats.real';
import type { ChatsApi } from './contract';
import { mockChatsApi } from './mock/chats.mock';

export const chatsApi: ChatsApi = config.useMocks ? mockChatsApi : realChatsApi;

export { usersApi } from './users';
export { ApiError } from './http';
export type * from './types';
export type {
  ChatsApi,
  DeltaHandler,
  SendMessageResult,
  StreamHandlers,
  ToolCallEvent,
} from './contract';
