<script setup lang="ts">
import { computed } from 'vue';

import { resetMockDb } from '@/api/mock/chats.mock';
import { accountUrl, isAuthDisabled } from '@/auth/auth';
import AppAvatar from '@/components/ui/AppAvatar.vue';
import AppDropdown from '@/components/ui/AppDropdown.vue';
import AppIcon from '@/components/ui/AppIcon.vue';
import MenuItem from '@/components/ui/MenuItem.vue';
import { config } from '@/config';
import { useThemeStore } from '@/stores/theme';
import { useToastStore } from '@/stores/toasts';
import { useUserStore } from '@/stores/user';

const user = useUserStore();
const theme = useThemeStore();
const toasts = useToastStore();

const account = computed(() => accountUrl());
const authOff = isAuthDisabled();

function openAccount(): void {
  const url = account.value;
  if (url) window.open(url, '_blank', 'noopener');
}

function resetMocks(): void {
  resetMockDb();
  toasts.success('Мок-данные сброшены. Обнови страницу.');
}
</script>

<template>
  <AppDropdown align="left" up width="256px">
    <template #trigger>
      <button class="user-trigger">
        <AppAvatar :name="user.displayName" :size="30" />

        <span class="user-trigger__text">
          <span class="user-trigger__name">{{ user.displayName }}</span>
          <span class="user-trigger__email">{{ user.email || 'без почты' }}</span>
        </span>

        <AppIcon name="chevron" :size="15" class="user-trigger__chevron" />
      </button>
    </template>

    <template #panel="{ close }">
      <div class="user-panel__head">
        <AppAvatar :name="user.displayName" :size="38" />
        <div class="user-panel__identity">
          <p class="user-panel__name">{{ user.displayName }}</p>
          <p class="user-panel__email">{{ user.email || '—' }}</p>
        </div>
      </div>

      <div class="user-panel__flags">
        <span v-if="authOff" class="badge badge--warn">авторизация выключена</span>
        <span v-if="config.useMocks" class="badge">моки включены</span>
        <span v-if="!user.backendAvailable" class="badge badge--warn">бэкенд недоступен</span>
      </div>

      <div class="user-panel__divider" />

      <MenuItem :icon="theme.current.dark ? 'sun' : 'moon'" @click="theme.toggleLightDark()">
        {{ theme.current.dark ? 'Светлая тема' : 'Тёмная тема' }}
      </MenuItem>

      <MenuItem v-if="account" icon="user" @click="openAccount(); close()">
        Профиль в Keycloak
      </MenuItem>

      <MenuItem v-if="config.useMocks" icon="refresh" @click="resetMocks(); close()">
        Сбросить мок-данные
      </MenuItem>

      <div class="user-panel__divider" />

      <MenuItem icon="logout" danger @click="user.logout()">
        {{ authOff ? 'Перезагрузить' : 'Выйти' }}
      </MenuItem>
    </template>
  </AppDropdown>
</template>

<style scoped>
.user-trigger {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  transition: background-color var(--dur-fast) var(--ease-out);
}

.user-trigger:hover {
  background: var(--bg-hover);
}

.user-trigger__text {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
}

.user-trigger__name {
  max-width: 100%;
  font-size: var(--text-base);
  font-weight: 550;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-trigger__email {
  max-width: 100%;
  font-size: var(--text-xs);
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-trigger__chevron {
  flex-shrink: 0;
  color: var(--text-muted);
}

.user-panel__head {
  display: flex;
  align-items: center;
  gap: 11px;
  padding: 8px 10px 10px;
}

.user-panel__identity {
  min-width: 0;
}

.user-panel__name {
  font-size: var(--text-base);
  font-weight: 560;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-panel__email {
  font-size: var(--text-xs);
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-panel__flags {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  padding: 0 10px 8px;
}

.badge {
  padding: 2px 7px;
  border-radius: var(--radius-pill);
  font-size: 10.5px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  background: var(--accent-soft);
  color: var(--accent);
}

.badge--warn {
  background: var(--danger-soft);
  color: var(--danger);
}

.user-panel__divider {
  height: 1px;
  margin: 5px 0;
  background: var(--border);
}
</style>
