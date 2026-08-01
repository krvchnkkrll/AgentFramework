/**
 * Авторизация через Keycloak (Authorization Code Flow + PKCE).
 *
 * Модуль намеренно не зависит от Vue: это просто набор функций,
 * которые дёргает main.ts при старте и http.ts перед каждым запросом.
 *
 * Если config.authDisabled === true, весь Keycloak выключается и подставляется
 * фейковый пользователь. Это нужно, пока клиента в Keycloak ещё не завели.
 */
import Keycloak from 'keycloak-js';

import { config } from '@/config';

/** Профиль, вытащенный из access-токена. */
export interface AuthProfile {
  subject: string;
  username: string;
  email: string;
  roles: string[];
}

const FAKE_PROFILE: AuthProfile = {
  subject: '00000000-0000-0000-0000-000000000001',
  username: 'dev.user',
  email: 'dev.user@localhost',
  roles: ['user'],
};

let keycloak: Keycloak | null = null;
let profile: AuthProfile = FAKE_PROFILE;

/** Токен обновляем, если ему осталось жить меньше стольких секунд. */
const MIN_TOKEN_VALIDITY_SECONDS = 30;

export function isAuthDisabled(): boolean {
  return config.authDisabled;
}

/**
 * Инициализация до монтирования приложения.
 * При включённой авторизации редиректит на страницу логина Keycloak,
 * если пользователь ещё не залогинен.
 */
export async function initAuth(): Promise<void> {
  if (config.authDisabled) {
    console.warn(
      '[auth] VITE_AUTH_DISABLED=true — авторизация выключена, работаем под фейковым пользователем.',
    );
    profile = FAKE_PROFILE;
    return;
  }

  assertKeycloakConfig();

  keycloak = new Keycloak({
    url: config.keycloak.url,
    realm: config.keycloak.realm,
    clientId: config.keycloak.clientId,
  });

  const authenticated = await keycloak.init({
    onLoad: 'login-required',
    pkceMethod: 'S256',
    checkLoginIframe: false,
    // Убираем ?code=...&state=... из адресной строки после возврата от Keycloak.
    responseMode: 'query',
  });

  if (!authenticated) {
    await keycloak.login();
    // login() уходит редиректом — сюда управление не вернётся.
    return new Promise<void>(() => {});
  }

  profile = readProfile(keycloak);

  // Фоновое продление сессии: раз в 20 секунд проверяем, не пора ли обновить токен.
  window.setInterval(() => {
    void keycloak?.updateToken(60).catch(() => {
      console.warn('[auth] Не удалось обновить токен, разлогиниваемся.');
      void logout();
    });
  }, 20_000);
}

/** Актуальный access-токен для заголовка Authorization. null — если авторизация выключена. */
export async function getAccessToken(): Promise<string | null> {
  if (config.authDisabled || !keycloak) return null;

  try {
    await keycloak.updateToken(MIN_TOKEN_VALIDITY_SECONDS);
  } catch {
    await logout();
    return null;
  }

  return keycloak.token ?? null;
}

export function getProfile(): AuthProfile {
  return profile;
}

export async function logout(): Promise<void> {
  if (config.authDisabled || !keycloak) {
    window.location.reload();
    return;
  }

  await keycloak.logout({ redirectUri: window.location.origin });
}

/** Ссылка на страницу управления аккаунтом в Keycloak. */
export function accountUrl(): string | null {
  if (config.authDisabled || !keycloak) return null;
  return keycloak.createAccountUrl();
}

function readProfile(kc: Keycloak): AuthProfile {
  const token = kc.tokenParsed as
    | (Record<string, unknown> & {
        sub?: string;
        preferred_username?: string;
        email?: string;
        realm_access?: { roles?: string[] };
      })
    | undefined;

  return {
    subject: token?.sub ?? '',
    username: token?.preferred_username ?? '',
    email: token?.email ?? '',
    roles: token?.realm_access?.roles ?? [],
  };
}

function assertKeycloakConfig(): void {
  const missing = (['url', 'realm', 'clientId'] as const).filter((key) => !config.keycloak[key]);

  if (missing.length > 0) {
    throw new Error(
      `[auth] Не заданы переменные окружения Keycloak: ${missing.join(', ')}. ` +
        'Заполни .env.local (см. .env.example) или поставь VITE_AUTH_DISABLED=true.',
    );
  }
}
