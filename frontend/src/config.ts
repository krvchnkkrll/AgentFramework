/**
 * Единая точка чтения переменных окружения.
 * Нигде больше в коде не должно быть обращений к import.meta.env.
 */

function bool(value: string | undefined, fallback: boolean): boolean {
  if (value === undefined || value === '') return fallback;
  return value.toLowerCase() === 'true' || value === '1';
}

export const config = {
  /** Авторизация выключена — работаем под фейковым пользователем (Keycloak не нужен). */
  authDisabled: bool(import.meta.env.VITE_AUTH_DISABLED, false),

  /** Чаты/сообщения/вложения берём из мок-слоя вместо реального API. */
  useMocks: bool(import.meta.env.VITE_USE_MOCKS, false),

  keycloak: {
    url: import.meta.env.VITE_KEYCLOAK_URL ?? '',
    realm: import.meta.env.VITE_KEYCLOAK_REALM ?? '',
    clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? '',
  },

  /** Префикс API. Пустой — значит относительные пути и dev-прокси vite. */
  apiBaseUrl: '',
} as const;
