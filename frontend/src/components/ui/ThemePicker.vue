<script setup lang="ts">
import { useThemeStore } from '@/stores/theme';

import AppDropdown from './AppDropdown.vue';
import AppIcon from './AppIcon.vue';

const theme = useThemeStore();
</script>

<template>
  <AppDropdown align="right" width="228px">
    <template #trigger>
      <button class="theme-trigger" :title="`Тема: ${theme.current.name}`" aria-label="Выбрать тему">
        <AppIcon name="palette" :size="17" />
      </button>
    </template>

    <template #panel="{ close }">
      <p class="theme-panel__title">Тема оформления</p>

      <button
        v-for="item in theme.themes"
        :key="item.id"
        class="theme-option"
        :class="{ 'theme-option--active': item.id === theme.themeId }"
        role="menuitemradio"
        :aria-checked="item.id === theme.themeId"
        @click="
          theme.setTheme(item.id);
          close();
        "
      >
        <span class="theme-option__swatch">
          <i v-for="(color, index) in item.swatch" :key="index" :style="{ background: color }" />
        </span>
        <span class="theme-option__name">{{ item.name }}</span>
        <AppIcon v-if="item.id === theme.themeId" name="check" :size="15" class="theme-option__check" />
      </button>
    </template>
  </AppDropdown>
</template>

<style scoped>
.theme-trigger {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: var(--radius-sm);
  color: var(--text-secondary);
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.theme-trigger:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.theme-panel__title {
  padding: 6px 10px 8px;
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-muted);
}

.theme-option {
  display: flex;
  align-items: center;
  gap: 11px;
  width: 100%;
  padding: 8px 10px;
  border-radius: var(--radius-xs);
  color: var(--text-secondary);
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.theme-option:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.theme-option--active {
  color: var(--text-primary);
}

.theme-option__swatch {
  display: flex;
  flex-shrink: 0;
  width: 44px;
  height: 20px;
  border-radius: var(--radius-pill);
  overflow: hidden;
  border: 1px solid var(--border-strong);
}

.theme-option__swatch i {
  flex: 1;
}

.theme-option__name {
  flex: 1;
  text-align: left;
  font-size: var(--text-base);
}

.theme-option__check {
  color: var(--accent);
}
</style>
