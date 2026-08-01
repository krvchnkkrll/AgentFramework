<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';

import type { MessageResponse } from '@/api';
import AppIcon from '@/components/ui/AppIcon.vue';
import { useChatsStore } from '@/stores/chats';

import MessageBubble from './MessageBubble.vue';

const props = defineProps<{
  messages: MessageResponse[];
  userName: string;
  loading: boolean;
}>();

const chats = useChatsStore();

const scroller = ref<HTMLElement | null>(null);
/** Пользователь отлистал вверх — тогда не дёргаем его автопрокруткой. */
const pinnedToBottom = ref(true);

const BOTTOM_THRESHOLD_PX = 90;

function onScroll(): void {
  const element = scroller.value;
  if (!element) return;

  const distance = element.scrollHeight - element.scrollTop - element.clientHeight;
  pinnedToBottom.value = distance < BOTTOM_THRESHOLD_PX;
}

function scrollToBottom(behavior: ScrollBehavior = 'smooth'): void {
  const element = scroller.value;
  if (!element) return;

  element.scrollTo({ top: element.scrollHeight, behavior });
  pinnedToBottom.value = true;

  // Плавная прокрутка не гарантирует событие scroll ровно в конечной точке,
  // поэтому пересчитываем положение, когда она закончится.
  window.setTimeout(onScroll, 450);
}

// Новый токен или новое сообщение — догоняем низ, если пользователь и так там.
watch(
  () => [props.messages.length, props.messages.at(-1)?.content.length] as const,
  async ([length], [previousLength]) => {
    if (!pinnedToBottom.value) return;
    await nextTick();
    scrollToBottom(length > previousLength ? 'smooth' : 'auto');
  },
);

// Сменился чат — прыгаем в конец мгновенно, без анимации.
watch(
  () => chats.activeChatId,
  async () => {
    await nextTick();
    scrollToBottom('auto');
  },
);

onMounted(() => {
  scrollToBottom('auto');
  scroller.value?.addEventListener('scrollend', onScroll);
});

onBeforeUnmount(() => scroller.value?.removeEventListener('scrollend', onScroll));
</script>

<template>
  <div class="list-shell">
    <div class="list" ref="scroller" @scroll.passive="onScroll">
      <div class="list__inner">
        <div v-if="loading" class="list__loading">
          <span class="spinner" />
          Загружаю переписку…
        </div>

        <MessageBubble
          v-for="message in messages"
          :key="message.id"
          :message="message"
          :user-name="userName"
          :agent="chats.agentById(message.agentId)"
          :streaming="chats.streamingMessageId === message.id"
        />
      </div>
    </div>

    <Transition name="fab">
      <button v-if="!pinnedToBottom" class="to-bottom" title="Вниз" @click="scrollToBottom()">
        <AppIcon name="arrow-down" :size="18" />
      </button>
    </Transition>
  </div>
</template>

<style scoped>
.list-shell {
  position: relative;
  display: flex;
  flex: 1;
  min-height: 0;
}

.list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
}

.list__inner {
  display: flex;
  flex-direction: column;
  gap: 14px;
  width: 100%;
  max-width: var(--content-max-width);
  margin: 0 auto;
  padding: 24px 24px 8px;
}

.list__loading {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 9px;
  padding: 30px;
  font-size: var(--text-sm);
  color: var(--text-muted);
}

.spinner {
  width: 15px;
  height: 15px;
  border: 2px solid var(--text-muted);
  border-top-color: transparent;
  border-radius: 50%;
  animation: af-spin 0.7s linear infinite;
}

.to-bottom {
  position: absolute;
  bottom: 12px;
  left: 50%;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  margin-left: -18px;
  border-radius: var(--radius-pill);
  background: var(--bg-elevated);
  border: 1px solid var(--border-strong);
  color: var(--text-secondary);
  box-shadow: var(--shadow-md);
  transition: transform var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.to-bottom:hover {
  color: var(--text-primary);
  transform: translateY(-2px);
}

.fab-enter-active,
.fab-leave-active {
  transition: opacity var(--dur-base) var(--ease-out), transform var(--dur-base) var(--ease-out);
}

.fab-enter-from,
.fab-leave-to {
  opacity: 0;
  transform: scale(0.8);
}

@media (max-width: 640px) {
  .list__inner {
    padding: 16px 14px 8px;
  }
}
</style>
