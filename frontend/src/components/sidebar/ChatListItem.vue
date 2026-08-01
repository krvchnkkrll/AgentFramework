<script setup lang="ts">
import { nextTick, ref } from 'vue';

import type { ChatResponse } from '@/api';
import AppDropdown from '@/components/ui/AppDropdown.vue';
import AppIcon from '@/components/ui/AppIcon.vue';
import MenuItem from '@/components/ui/MenuItem.vue';
import { formatRelative } from '@/utils/format';

const props = defineProps<{
  chat: ChatResponse;
  active: boolean;
  /** Символ агента, к которому привязан чат. */
  agentGlyph: string | null;
}>();

const emit = defineEmits<{
  select: [];
  rename: [title: string];
  togglePin: [];
  remove: [];
}>();

const renaming = ref(false);
const renameValue = ref('');
const renameInput = ref<HTMLInputElement | null>(null);

async function startRename(): Promise<void> {
  renameValue.value = props.chat.title;
  renaming.value = true;
  await nextTick();
  renameInput.value?.focus();
  renameInput.value?.select();
}

function commitRename(): void {
  if (!renaming.value) return;
  renaming.value = false;

  const title = renameValue.value.trim();
  if (title && title !== props.chat.title) emit('rename', title);
}

function cancelRename(): void {
  renaming.value = false;
}
</script>

<template>
  <li class="chat-item" :class="{ 'chat-item--active': active }">
    <button v-if="!renaming" class="chat-item__body" @click="emit('select')">
      <span class="chat-item__glyph" aria-hidden="true">{{ agentGlyph ?? '◇' }}</span>

      <span class="chat-item__text">
        <span class="chat-item__title">
          <AppIcon v-if="chat.pinned" name="pin" :size="12" class="chat-item__pin" />
          <!-- Отдельный span: text-overflow не работает на flex-контейнере -->
          <span class="chat-item__title-text">{{ chat.title }}</span>
        </span>
        <span class="chat-item__preview">{{ chat.lastMessagePreview ?? 'Пока пусто' }}</span>
      </span>
    </button>

    <input
      v-else
      ref="renameInput"
      v-model="renameValue"
      class="chat-item__rename"
      maxlength="120"
      @blur="commitRename"
      @keydown.enter.prevent="commitRename"
      @keydown.esc.prevent="cancelRename"
    />

    <AppDropdown v-if="!renaming" align="right" width="200px" class="chat-item__menu">
      <template #trigger>
        <!-- Без .stop: клик должен дойти до обёртки AppDropdown, которая открывает меню.
             Кнопка выбора чата — соседний элемент, так что всплытие ей не мешает. -->
        <button class="chat-item__menu-trigger" aria-label="Действия с чатом">
          <AppIcon name="more" :size="16" :stroke-width="2.4" />
        </button>
      </template>

      <template #panel="{ close }">
        <p class="chat-item__meta">Изменён {{ formatRelative(chat.updatedAt) }}</p>

        <MenuItem
          :icon="chat.pinned ? 'pin-off' : 'pin'"
          @click="
            emit('togglePin');
            close();
          "
        >
          {{ chat.pinned ? 'Открепить' : 'Закрепить' }}
        </MenuItem>

        <MenuItem
          icon="pencil"
          @click="
            close();
            startRename();
          "
        >
          Переименовать
        </MenuItem>

        <MenuItem
          icon="trash"
          danger
          @click="
            emit('remove');
            close();
          "
        >
          Удалить
        </MenuItem>
      </template>
    </AppDropdown>
  </li>
</template>

<style scoped>
.chat-item {
  position: relative;
  display: flex;
  align-items: center;
  border-radius: var(--radius-sm);
  transition: background-color var(--dur-fast) var(--ease-out);
}

.chat-item:hover {
  background: var(--bg-hover);
}

.chat-item--active {
  background: var(--bg-active);
}

.chat-item--active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  width: 3px;
  height: 22px;
  border-radius: 0 3px 3px 0;
  background: var(--accent);
  transform: translateY(-50%);
}

.chat-item__body {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 6px 9px 11px;
  text-align: left;
}

.chat-item__glyph {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: var(--radius-xs);
  background: var(--bg-surface-2);
  border: 1px solid var(--border);
  color: var(--text-secondary);
  font-size: 13px;
}

.chat-item--active .chat-item__glyph {
  background: var(--accent-soft);
  border-color: transparent;
  color: var(--accent);
}

.chat-item__text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.chat-item__title {
  display: flex;
  align-items: center;
  gap: 5px;
  min-width: 0;
  font-size: var(--text-base);
  font-weight: 500;
  color: var(--text-secondary);
}

.chat-item__title-text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chat-item--active .chat-item__title {
  color: var(--text-primary);
}

.chat-item__pin {
  flex-shrink: 0;
  color: var(--accent);
}

.chat-item__preview {
  font-size: var(--text-xs);
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chat-item__rename {
  flex: 1;
  min-width: 0;
  margin: 5px 8px;
  padding: 7px 10px;
  background: var(--bg-inset);
  border: 1px solid var(--accent);
  border-radius: var(--radius-xs);
  font-size: var(--text-base);
}

.chat-item__menu {
  margin-right: 6px;
}

.chat-item__menu-trigger {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: var(--radius-xs);
  color: var(--text-muted);
  opacity: 0;
  transition: opacity var(--dur-fast) var(--ease-out), background-color var(--dur-fast) var(--ease-out);
}

.chat-item:hover .chat-item__menu-trigger,
.chat-item__menu-trigger:focus-visible {
  opacity: 1;
}

.chat-item__menu-trigger:hover {
  background: var(--bg-active);
  color: var(--text-primary);
}

.chat-item__meta {
  padding: 6px 10px 8px;
  font-size: var(--text-xs);
  color: var(--text-muted);
  border-bottom: 1px solid var(--border);
  margin-bottom: 4px;
}

/* На тач-устройствах меню видно всегда — hover там не работает. */
@media (hover: none) {
  .chat-item__menu-trigger {
    opacity: 1;
  }
}
</style>
