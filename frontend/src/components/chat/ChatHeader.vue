<script setup lang="ts">
import AppDropdown from '@/components/ui/AppDropdown.vue';
import AppIcon from '@/components/ui/AppIcon.vue';
import MenuItem from '@/components/ui/MenuItem.vue';
import ThemePicker from '@/components/ui/ThemePicker.vue';
import { useChatsStore } from '@/stores/chats';

defineProps<{
  title: string;
  sidebarCollapsed: boolean;
}>();

const emit = defineEmits<{ toggleSidebar: [] }>();

const chats = useChatsStore();

function pickAgent(agentId: string): void {
  const chatId = chats.activeChatId;
  if (chatId) void chats.setAgent(chatId, agentId);
}
</script>

<template>
  <header class="header surface-glass">
    <button
      v-if="sidebarCollapsed"
      class="header__icon-btn"
      title="Показать панель"
      @click="emit('toggleSidebar')"
    >
      <AppIcon name="sidebar" :size="18" />
    </button>

    <div class="header__title-area">
      <h1 class="header__title">{{ title }}</h1>
      <span v-if="chats.activeChat" class="header__badge">
        {{ chats.agentById(chats.activeChat.agentId)?.name ?? 'Без агента' }}
      </span>
    </div>

    <div class="header__spacer" />

    <!-- Выбор агента — заглушка под будущий конструктор -->
    <AppDropdown align="right" width="270px">
      <template #trigger>
        <button class="header__icon-btn" title="Агент">
          <AppIcon name="sparkles" :size="17" />
        </button>
      </template>

      <template #panel="{ close }">
        <p class="panel__title">Агент</p>

        <MenuItem
          v-for="agent in chats.agents"
          :key="agent.id"
          :active="agent.id === chats.activeChat?.agentId"
          @click="
            pickAgent(agent.id);
            close();
          "
        >
          <span class="agent">
            <span class="agent__glyph">{{ agent.icon }}</span>
            <span class="agent__text">
              <span class="agent__name">{{ agent.name }}</span>
              <span class="agent__desc">{{ agent.description }}</span>
            </span>
          </span>
        </MenuItem>

        <div class="panel__divider" />
        <p class="panel__note">Конструктор агентов появится позже.</p>
      </template>
    </AppDropdown>

    <ThemePicker />
  </header>
</template>

<style scoped>
.header {
  display: flex;
  align-items: center;
  gap: 8px;
  height: var(--header-height);
  flex-shrink: 0;
  padding: 0 12px 0 16px;
  border-bottom: 1px solid var(--border);
  z-index: var(--z-header);
}

.header__icon-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  flex-shrink: 0;
  border-radius: var(--radius-sm);
  color: var(--text-secondary);
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.header__icon-btn:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.header__title-area {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.header__title {
  font-size: var(--text-md);
  font-weight: 600;
  letter-spacing: -0.015em;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.header__badge {
  flex-shrink: 0;
  padding: 2px 9px;
  border-radius: var(--radius-pill);
  background: var(--accent-soft);
  color: var(--accent);
  font-size: var(--text-xs);
  font-weight: 550;
}

.header__spacer {
  flex: 1;
}

.panel__title {
  padding: 6px 10px 8px;
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-muted);
}

.panel__divider {
  height: 1px;
  margin: 5px 0;
  background: var(--border);
}

.panel__note {
  padding: 2px 10px 6px;
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.agent {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.agent__glyph {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  flex-shrink: 0;
  border-radius: var(--radius-xs);
  background: var(--bg-surface-2);
  border: 1px solid var(--border);
}

.agent__text {
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.agent__name {
  font-size: var(--text-base);
  font-weight: 540;
  color: var(--text-primary);
}

.agent__desc {
  font-size: var(--text-xs);
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 640px) {
  .header__badge {
    display: none;
  }
}
</style>
