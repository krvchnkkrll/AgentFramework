import {fileURLToPath, URL} from 'node:url';

import vue from '@vitejs/plugin-vue';
import {defineConfig, loadEnv} from 'vite';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      strictPort: true,
      // Всё, что начинается с /api, проксируется на ASP.NET Core.
      // Благодаря этому браузер считает, что фронт и бэк на одном origin,
      // и CORS на бэкенде настраивать не нужно. /hubs — то же самое для
      // SignalR (ws: true нужен, иначе proxy не апгрейдит соединение до WebSocket).
      proxy: {
        '/api': {
          target: env.VITE_API_PROXY_TARGET ?? 'http://localhost:5175',
          changeOrigin: true,
          secure: false,
        },
        '/hubs': {
          target: env.VITE_API_PROXY_TARGET ?? 'http://localhost:5175',
          changeOrigin: true,
          secure: false,
          ws: true,
        },
      },
    },
  };
});
