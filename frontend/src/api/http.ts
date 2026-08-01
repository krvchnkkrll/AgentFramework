/**
 * Тонкая обёртка над fetch: подставляет токен, разбирает ProblemDetails,
 * бросает типизированный ApiError.
 */
import { getAccessToken } from '@/auth/auth';
import { config } from '@/config';

import type { ProblemDetails } from './types';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isUnauthorized(): boolean {
    return this.status === 401 || this.status === 403;
  }

  /** Эндпоинта ещё нет на бэкенде — типичная ситуация на текущем этапе. */
  get isNotImplemented(): boolean {
    return this.status === 404 || this.status === 501;
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';
  /** Сериализуется в JSON. Взаимоисключающе с body. */
  json?: unknown;
  /** Сырое тело (например FormData). */
  body?: BodyInit;
  signal?: AbortSignal;
  headers?: Record<string, string>;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await rawRequest(path, options);

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** То же самое, но возвращает Response — нужно для стриминга тела ответа. */
export async function rawRequest(path: string, options: RequestOptions = {}): Promise<Response> {
  const { method = 'GET', json, body, signal, headers = {} } = options;

  const token = await getAccessToken();

  const finalHeaders: Record<string, string> = {
    Accept: 'application/json',
    ...headers,
  };

  if (token) {
    finalHeaders.Authorization = `Bearer ${token}`;
  }

  let finalBody = body;
  if (json !== undefined) {
    finalHeaders['Content-Type'] = 'application/json';
    finalBody = JSON.stringify(json);
  }

  let response: Response;
  try {
    response = await fetch(`${config.apiBaseUrl}${path}`, {
      method,
      headers: finalHeaders,
      body: finalBody,
      signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new ApiError(0, 'Сервер недоступен. Проверь, что бэкенд запущен.', 'Network');
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  return response;
}

async function toApiError(response: Response): Promise<ApiError> {
  let detail = response.statusText || `Ошибка ${response.status}`;
  let code: string | undefined;

  try {
    const problem = (await response.json()) as ProblemDetails;
    detail = problem.detail ?? problem.title ?? detail;
    code = problem.code;
  } catch {
    // тело не JSON — оставляем statusText
  }

  return new ApiError(response.status, detail, code);
}
