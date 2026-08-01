<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';

import AppButton from '@/components/ui/AppButton.vue';
import AppIcon from '@/components/ui/AppIcon.vue';
import ConfirmDialog from '@/components/ui/ConfirmDialog.vue';
import { useChatsStore } from '@/stores/chats';

import ChatListItem from './ChatListItem.vue';
import UserMenu from './UserMenu.vue';

// Компонент рендерится только в развёрнутом состоянии — App.vue снимает его через v-if.
const emit = defineEmits<{ toggle: [] }>();

const chats = useChatsStore();
const router = useRouter();

const pendingDeleteId = ref<string | null>(null);
const pendingDeleteTitle = ref('');

function openChat(chatId: string): void {
  void router.push({ name: 'chat', params: { chatId } });
}

function newChat(): void {
  void router.push({ name: 'new-chat' });
}

function askDelete(chatId: string, title: string): void {
  pendingDeleteId.value = chatId;
  pendingDeleteTitle.value = title;
}

async function confirmDelete(): Promise<void> {
  const chatId = pendingDeleteId.value;
  pendingDeleteId.value = null;
  if (!chatId) return;

  const wasActive = chats.activeChatId === chatId;
  await chats.deleteChat(chatId);

  if (wasActive) void router.push({ name: 'new-chat' });
}
</script>

<template>
  <aside class="sidebar">
    <div class="sidebar__head">
      <div class="brand">
        <span class="brand__mark" aria-hidden="true">
          <AppIcon name="sparkles" :size="17" />
        </span>
        <span class="brand__name">AgentFramework</span>
      </div>

      <button class="sidebar__collapse" title="Свернуть панель" @click="emit('toggle')">
        <AppIcon name="sidebar" :size="17" />
      </button>
    </div>

    <div class="sidebar__actions">
      <AppButton variant="primary" icon="plus" class="sidebar__new" @click="newChat">
        Новый чат
      </AppButton>
    </div>

    <div class="sidebar__search">
      <AppIcon name="search" :size="15" class="sidebar__search-icon" />
      <input
        v-model="chats.searchQuery"
        type="search"
        class="sidebar__search-input"
        placeholder="Поиск по чатам"
        aria-label="Поиск по чатам"
      />
      <button
        v-if="chats.searchQuery"
        class="sidebar__search-clear"
        aria-label="Очистить поиск"
        @click="chats.searchQuery = ''"
      >
        <AppIcon name="x" :size="13" />
      </button>
    </div>

    <nav class="sidebar__list" aria-label="История чатов">
      <!-- Скелетоны на время загрузки -->
      <div v-if="chats.chatsLoading" class="skeletons">
        <div v-for="n in 6" :key="n" class="skeleton" />
      </div>

      <p v-else-if="chats.loadError" class="sidebar__empty sidebar__empty--error">
        <AppIcon name="alert" :size="18" />
        {{ chats.loadError }}
      </p>

      <p v-else-if="chats.groupedChats.length === 0" class="sidebar__empty">
        <AppIcon name="message" :size="20" />
        {{ chats.searchQuery ? 'Ничего не найдено' : 'Чатов пока нет' }}
      </p>

      <template v-else>
        <section v-for="group in chats.groupedChats" :key="group.key" class="group">
          <h2 class="group__label">{{ group.label }}</h2>
          <ul class="group__items">
            <ChatListItem
              v-for="chat in group.chats"
              :key="chat.id"
              :chat="chat"
              :active="chat.id === chats.activeChatId"
              :agent-glyph="chats.agentById(chat.agentId)?.icon ?? null"
              @select="openChat(chat.id)"
              @rename="(title) => chats.renameChat(chat.id, title)"
              @toggle-pin="chats.togglePin(chat.id)"
              @remove="askDelete(chat.id, chat.title)"
            />
          </ul>
        </section>
      </template>
    </nav>

    <div class="sidebar__foot">
      <UserMenu />
    </div>

    <ConfirmDialog
      :open="pendingDeleteId !== null"
      title="Удалить чат?"
      :message="`«${pendingDeleteTitle}» и вся переписка будут удалены безвозвратно.`"
      confirm-label="Удалить"
      danger
      @confirm="confirmDelete"
      @cancel="pendingDeleteId = null"
    />
  </aside>
</template>

<style scoped>
.sidebar {
  display: flex;
  flex-direction: column;
  width: var(--sidebar-width);
  flex-shrink: 0;
  height: 100%;
  background: var(--bg-sidebar);
  border-right: 1px solid var(--border);
  overflow: hidden;
}

.sidebar__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  height: var(--header-height);
  padding: 0 10px 0 16px;
  flex-shrink: 0;
}

.brand {
  display: flex;
  align-items: center;
  gap: 9px;
  min-width: 0;
}

.brand__mark {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  flex-shrink: 0;
  border-radius: var(--radius-xs);
  background: var(--accent-gradient);
  color: var(--accent-contrast);
  box-shadow: var(--shadow-accent);
}

.brand__name {
  font-size: var(--text-md);
  font-weight: 620;
  letter-spacing: -0.015em;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sidebar__collapse {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
  border-radius: var(--radius-xs);
  color: var(--text-muted);
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.sidebar__collapse:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.sidebar__actions {
  padding: 4px 12px 10px;
  flex-shrink: 0;
}

.sidebar__new {
  width: 100%;
}

.sidebar__search {
  position: relative;
  display: flex;
  align-items: center;
  margin: 0 12px 8px;
  flex-shrink: 0;
}

.sidebar__search-icon {
  position: absolute;
  left: 10px;
  color: var(--text-muted);
  pointer-events: none;
}

.sidebar__search-input {
  width: 100%;
  height: 34px;
  padding: 0 30px 0 32px;
  background: var(--bg-inset);
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  font-size: var(--text-sm);
  transition: border-color var(--dur-fast) var(--ease-out), background-color var(--dur-fast) var(--ease-out);
}

.sidebar__search-input::placeholder {
  color: var(--text-muted);
}

.sidebar__search-input:focus {
  border-color: var(--accent);
  background: var(--bg-surface);
}

.sidebar__search-input::-webkit-search-cancel-button {
  display: none;
}

.sidebar__search-clear {
  position: absolute;
  right: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border-radius: var(--radius-pill);
  color: var(--text-muted);
  background: var(--bg-hover);
}

.sidebar__search-clear:hover {
  color: var(--text-primary);
}

.sidebar__list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 0 8px 12px;
}

.group + .group {
  margin-top: 14px;
}

.group__label {
  padding: 8px 10px 5px;
  font-size: 10.5px;
  font-weight: 650;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--text-muted);
}

.group__items {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.sidebar__empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 40px 20px;
  text-align: center;
  font-size: var(--text-sm);
  color: var(--text-muted);
}

.sidebar__empty--error {
  color: var(--danger);
}

.skeletons {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 10px 4px;
}

.skeleton {
  height: 44px;
  border-radius: var(--radius-sm);
  background: linear-gradient(
    90deg,
    var(--bg-hover) 25%,
    var(--bg-active) 50%,
    var(--bg-hover) 75%
  );
  background-size: 200% 100%;
  animation: af-shimmer 1.4s var(--ease-in-out) infinite;
}

.sidebar__foot {
  flex-shrink: 0;
  padding: 8px;
  border-top: 1px solid var(--border);
}
</style>
