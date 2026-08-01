/** Единственный эндпоинт, который уже существует на бэкенде. */
import { request } from './http';
import type { UserResponse } from './types';

export const usersApi = {
  getCurrentUser(signal?: AbortSignal): Promise<UserResponse> {
    return request<UserResponse>('/api/user/current-user', { signal });
  },
};
