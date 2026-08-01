/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue';
  const component: DefineComponent<Record<string, unknown>, Record<string, unknown>, unknown>;
  export default component;
}

interface ImportMetaEnv {
  /** Базовый URL Keycloak, например https://192.168.0.14:8080 */
  readonly VITE_KEYCLOAK_URL: string;
  /** Realm, например workspace */
  readonly VITE_KEYCLOAK_REALM: string;
  /** Публичный (public) клиент SPA, например agentframework-web */
  readonly VITE_KEYCLOAK_CLIENT_ID: string;
  /** 'true' — полностью выключить авторизацию и работать под фейковым пользователем */
  readonly VITE_AUTH_DISABLED?: string;
  /** 'true' — чаты/сообщения/вложения берутся из мок-слоя вместо реального API */
  readonly VITE_USE_MOCKS?: string;
  /** Куда vite проксирует /api в dev-режиме */
  readonly VITE_API_PROXY_TARGET?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
