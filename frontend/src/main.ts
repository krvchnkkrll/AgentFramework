import { createPinia } from 'pinia';
import { createApp } from 'vue';

import App from './App.vue';
import { initAuth } from './auth/auth';
import { router } from './router';
import './styles/base.css';
import './styles/markdown.css';

async function bootstrap(): Promise<void> {
  try {
    // Пока не отработает Keycloak, приложение не монтируем:
    // иначе первые запросы уйдут без токена.
    await initAuth();
  } catch (error) {
    renderFatal(error);
    return;
  }

  createApp(App).use(createPinia()).use(router).mount('#app');
}

/** Если Keycloak не сконфигурирован — показываем понятную ошибку вместо белого экрана. */
function renderFatal(error: unknown): void {
  const message = error instanceof Error ? error.message : String(error);

  const root = document.getElementById('app');
  if (!root) return;

  root.innerHTML = `
    <div style="display:flex;align-items:center;justify-content:center;height:100%;padding:24px;font-family:system-ui,sans-serif">
      <div style="max-width:540px">
        <h1 style="font-size:20px;margin:0 0 10px">Приложение не запустилось</h1>
        <pre style="white-space:pre-wrap;font-size:13px;line-height:1.6;opacity:.8;margin:0"></pre>
      </div>
    </div>`;

  // textContent, а не innerHTML — текст ошибки не должен исполняться как разметка.
  const pre = root.querySelector('pre');
  if (pre) pre.textContent = message;

  console.error(error);
}

void bootstrap();
