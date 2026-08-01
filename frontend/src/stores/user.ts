/** Текущий пользователь: GET /api/user/current-user — единственный готовый эндпоинт. */
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

import { ApiError, usersApi } from '@/api';
import type { UserResponse } from '@/api';
import { getProfile, isAuthDisabled, logout as authLogout } from '@/auth/auth';

export const useUserStore = defineStore('user', () => {
  const user = ref<UserResponse | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  /** Есть ли связь с бэкендом. Пока его нет — работаем на данных из токена. */
  const backendAvailable = ref(true);

  const displayName = computed(() => user.value?.username ?? getProfile().username ?? 'Гость');
  const email = computed(() => user.value?.email ?? getProfile().email ?? '');

  async function load(): Promise<void> {
    loading.value = true;
    error.value = null;

    try {
      user.value = await usersApi.getCurrentUser();
      backendAvailable.value = true;
    } catch (e) {
      backendAvailable.value = false;

      // Бэкенд может быть просто не запущен — это не повод ронять UI.
      const profile = getProfile();
      user.value = {
        id: profile.subject || '00000000-0000-0000-0000-000000000000',
        username: profile.username || 'Гость',
        email: profile.email || '',
        createdAt: new Date().toISOString(),
        lastSeenAt: null,
      };

      error.value =
        e instanceof ApiError
          ? `Бэкенд недоступен (${e.message}). Профиль взят из токена.`
          : 'Не удалось загрузить профиль.';

      if (isAuthDisabled()) {
        error.value = 'Бэкенд недоступен, авторизация выключена — показан тестовый пользователь.';
      }
    } finally {
      loading.value = false;
    }
  }

  function logout(): Promise<void> {
    return authLogout();
  }

  return { user, loading, error, backendAvailable, displayName, email, load, logout };
});
