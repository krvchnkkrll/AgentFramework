<script setup lang="ts">
import { computed, watch } from 'vue';
import { useRouter } from 'vue-router';

import ChatComposer from '@/components/chat/ChatComposer.vue';
import ChatHeader from '@/components/chat/ChatHeader.vue';
import MessageList from '@/components/chat/MessageList.vue';
import WelcomeScreen from '@/components/chat/WelcomeScreen.vue';
import { useChatsStore } from '@/stores/chats';
import { useUserStore } from '@/stores/user';

const props = defineProps<{
  /** Из маршрута. undefined — экран нового чата. */
  chatId?: string;
  sidebarCollapsed: boolean;
}>();

const emit = defineEmits<{ toggleSidebar: [] }>();

const chats = useChatsStore();
const user = useUserStore();
const router = useRouter();

const title = computed(() => chats.activeChat?.title ?? 'Новый чат');
const showWelcome = computed(() => !props.chatId && chats.activeMessages.length === 0);

watch(
  () => props.chatId,
  (chatId) => void chats.selectChat(chatId ?? null),
  { immediate: true },
);

// Ссылка на удалённый (или чужой) чат не должна оставлять пустой экран.
watch(
  [() => props.chatId, () => chats.chatsLoading, () => chats.chats.length],
  ([chatId, loading]) => {
    if (!chatId || loading) return;
    if (chats.chats.some((chat) => chat.id === chatId)) return;

    void router.replace({ name: 'new-chat' });
  },
  { immediate: true },
);

/** Первое сообщение создало чат — переезжаем на его адрес без записи в историю. */
function onSent(chatId: string): void {
  if (props.chatId !== chatId) {
    void router.replace({ name: 'chat', params: { chatId } });
  }
}
</script>

<template>
  <section class="chat">
    <ChatHeader
      :title="title"
      :sidebar-collapsed="sidebarCollapsed"
      @toggle-sidebar="emit('toggleSidebar')"
    />

    <WelcomeScreen v-if="showWelcome" />

    <MessageList
      v-else
      :messages="chats.activeMessages"
      :user-name="user.displayName"
      :loading="chats.messagesLoading"
    />

    <ChatComposer @sent="onSent" />
  </section>
</template>

<style scoped>
.chat {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  height: 100%;
}
</style>
