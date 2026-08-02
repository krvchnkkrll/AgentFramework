<script setup lang="ts">
import { computed, ref } from 'vue';

import type { AgentResponse, MessageResponse } from '@/api';
import AppAvatar from '@/components/ui/AppAvatar.vue';
import AppIcon from '@/components/ui/AppIcon.vue';
import { formatFullTimestamp, formatTimestamp } from '@/utils/format';
import { renderMarkdown } from '@/utils/markdown';

import AttachmentChip from './AttachmentChip.vue';
import ToolCallList from './ToolCallList.vue';

const props = defineProps<{
  message: MessageResponse;
  /** Имя пользователя — для аватарки своих сообщений. */
  userName: string;
  agent: AgentResponse | null;
  /** Это сообщение прямо сейчас «печатается». */
  streaming: boolean;
}>();

const isUser = computed(() => props.message.role === 'user');

/**
 * Сообщения пользователя показываем как обычный текст (никакого markdown —
 * человек написал ровно то, что написал). Ответ модели рендерим как markdown.
 */
const html = computed(() => (isUser.value ? '' : renderMarkdown(props.message.content)));

const copied = ref(false);

async function copy(): Promise<void> {
  try {
    await navigator.clipboard.writeText(props.message.content);
    copied.value = true;
    window.setTimeout(() => (copied.value = false), 1600);
  } catch {
    // clipboard недоступен (http без localhost) — молча игнорируем
  }
}
</script>

<template>
  <article class="message" :class="isUser ? 'message--user' : 'message--assistant'">
    <div class="message__avatar">
      <AppAvatar
        v-if="isUser"
        :name="userName"
        :size="30"
      />
      <AppAvatar
        v-else
        :name="agent?.name ?? 'Агент'"
        :glyph="agent?.icon ?? '✦'"
        :size="30"
        accent
      />
    </div>

    <div class="message__column">
      <header class="message__head">
        <span class="message__author">{{ isUser ? userName : (agent?.name ?? 'Ассистент') }}</span>
        <time class="message__time" :title="formatFullTimestamp(message.createdAt)">
          {{ formatTimestamp(message.createdAt) }}
        </time>
      </header>

      <div class="message__bubble">
        <!--
          Инструменты идут над текстом: агент вызывает их до того, как начнёт отвечать,
          и без этой плашки пауза в несколько десятков секунд выглядит зависанием.
        -->
        <ToolCallList v-if="!isUser && message.toolCalls?.length" :calls="message.toolCalls" />

        <!-- Текст пользователя: без markdown, переносы строк сохраняем через CSS -->
        <p v-if="isUser" class="message__plain">{{ message.content }}</p>

        <!-- Ответ модели: markdown, очищенный DOMPurify (см. utils/markdown.ts) -->
        <div v-else-if="message.content" class="markdown-body" v-html="html" />

        <!-- Ждём первый токен -->
        <div v-else-if="streaming" class="thinking" aria-label="Агент печатает">
          <span /><span /><span />
        </div>

        <p v-if="message.status === 'failed'" class="message__error">
          <AppIcon name="alert" :size="15" />
          {{ message.error ?? 'Не удалось получить ответ.' }}
        </p>

        <span v-if="streaming && message.content" class="stream-caret" />

        <div v-if="message.attachments.length > 0" class="message__attachments">
          <AttachmentChip
            v-for="attachment in message.attachments"
            :key="attachment.id"
            :attachment="attachment"
          />
        </div>
      </div>

      <div class="message__tools">
        <button class="tool" :title="copied ? 'Скопировано' : 'Копировать'" @click="copy">
          <AppIcon :name="copied ? 'check' : 'copy'" :size="14" />
          <span>{{ copied ? 'Скопировано' : 'Копировать' }}</span>
        </button>
      </div>
    </div>
  </article>
</template>

<style scoped>
.message {
  display: flex;
  gap: 13px;
  padding: 4px 0;
  animation: af-rise-in var(--dur-slow) var(--ease-out) both;
}

.message--user {
  flex-direction: row-reverse;
}

.message__avatar {
  flex-shrink: 0;
  padding-top: 22px;
}

.message__column {
  min-width: 0;
  max-width: min(100%, 680px);
  display: flex;
  flex-direction: column;
}

.message--user .message__column {
  align-items: flex-end;
}

.message__head {
  display: flex;
  align-items: baseline;
  gap: 8px;
  padding: 0 4px 4px;
}

.message__author {
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--text-secondary);
}

.message__time {
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.message__bubble {
  position: relative;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--bubble-agent);
  border: 1px solid var(--border);
  box-shadow: var(--shadow-sm);
}

.message--user .message__bubble {
  background: var(--bubble-user);
  border-color: transparent;
  color: var(--bubble-user-text);
  border-bottom-right-radius: var(--radius-xs);
  box-shadow: var(--shadow-accent);
}

.message--assistant .message__bubble {
  border-bottom-left-radius: var(--radius-xs);
}

.message__plain {
  white-space: pre-wrap;
  word-break: break-word;
  overflow-wrap: anywhere;
}

.message__error {
  display: flex;
  align-items: center;
  gap: 7px;
  margin-top: 8px;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  background: var(--danger-soft);
  color: var(--danger);
  font-size: var(--text-sm);
}

.message__attachments {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.message--user .message__attachments :deep(.chip) {
  background: rgba(255, 255, 255, 0.16);
  border-color: rgba(255, 255, 255, 0.28);
}

.message--user .message__attachments :deep(.chip__name),
.message--user .message__attachments :deep(.chip__size),
.message--user .message__attachments :deep(.chip__ext) {
  color: var(--bubble-user-text);
}

.message--user .message__attachments :deep(.chip__thumb) {
  background: rgba(255, 255, 255, 0.2);
}

/* Индикатор «печатает» */
.thinking {
  display: flex;
  align-items: center;
  gap: 5px;
  height: 20px;
}

.thinking span {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--text-muted);
  animation: af-thinking 1.2s var(--ease-in-out) infinite;
}

.thinking span:nth-child(2) {
  animation-delay: 0.16s;
}

.thinking span:nth-child(3) {
  animation-delay: 0.32s;
}

@keyframes af-thinking {
  0%,
  60%,
  100% {
    opacity: 0.3;
    transform: translateY(0);
  }
  30% {
    opacity: 1;
    transform: translateY(-4px);
  }
}

/* Панелька действий под сообщением */
.message__tools {
  display: flex;
  gap: 4px;
  height: 26px;
  padding: 0 4px;
  opacity: 0;
  transition: opacity var(--dur-base) var(--ease-out);
}

.message:hover .message__tools,
.message__tools:focus-within {
  opacity: 1;
}

.tool {
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 4px 8px;
  border-radius: var(--radius-xs);
  font-size: var(--text-xs);
  color: var(--text-muted);
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.tool:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

@media (hover: none) {
  .message__tools {
    opacity: 1;
  }
}

@media (max-width: 640px) {
  .message__avatar {
    display: none;
  }

  .message__column {
    max-width: 100%;
    flex: 1;
  }
}
</style>
