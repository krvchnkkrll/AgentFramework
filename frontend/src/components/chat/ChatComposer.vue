<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';

import AppIcon from '@/components/ui/AppIcon.vue';
import { useChatsStore } from '@/stores/chats';

import AttachmentChip from './AttachmentChip.vue';

const emit = defineEmits<{ sent: [chatId: string] }>();

const chats = useChatsStore();

const textarea = ref<HTMLTextAreaElement | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);
const dragging = ref(false);

/** Счётчик dragenter/dragleave — иначе подсветка мигает над дочерними элементами. */
let dragDepth = 0;

const MIN_TEXTAREA_HEIGHT = 38;
const MAX_TEXTAREA_HEIGHT = 220;

const draft = computed(() => chats.activeDraft);
const streaming = computed(() => chats.streamingMessageId !== null);

function autosize(): void {
  const element = textarea.value;
  if (!element) return;

  // Сначала схлопываем в 0 — иначе scrollHeight вернёт текущую (уже растянутую) высоту.
  element.style.height = '0px';
  const next = Math.max(MIN_TEXTAREA_HEIGHT, Math.min(element.scrollHeight, MAX_TEXTAREA_HEIGHT));
  element.style.height = `${next}px`;
}

function onInput(event: Event): void {
  chats.setDraftText((event.target as HTMLTextAreaElement).value);
  autosize();
}

function onKeydown(event: KeyboardEvent): void {
  // Enter отправляет, Shift+Enter — перевод строки.
  // Во время IME-композиции (китайский, японский) Enter не трогаем.
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault();
    void send();
  }
}

async function send(): Promise<void> {
  if (!chats.canSend) return;

  const chatId = await chats.sendDraft();
  await nextTick();
  autosize();
  textarea.value?.focus();

  if (chatId) emit('sent', chatId);
}

function pickFiles(): void {
  fileInput.value?.click();
}

async function onFilesPicked(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement;
  const files = [...(input.files ?? [])];
  input.value = ''; // чтобы тот же файл можно было выбрать повторно

  if (files.length > 0) await chats.attachFiles(files);
}

// ── Drag & drop ────────────────────────────────────────────────────────
function onDragEnter(event: DragEvent): void {
  if (!event.dataTransfer?.types.includes('Files')) return;
  dragDepth++;
  dragging.value = true;
}

function onDragLeave(): void {
  dragDepth = Math.max(0, dragDepth - 1);
  if (dragDepth === 0) dragging.value = false;
}

async function onDrop(event: DragEvent): Promise<void> {
  dragDepth = 0;
  dragging.value = false;

  const files = [...(event.dataTransfer?.files ?? [])];
  if (files.length > 0) await chats.attachFiles(files);
}

/** Вставка скриншота из буфера обмена. */
async function onPaste(event: ClipboardEvent): Promise<void> {
  const files = [...(event.clipboardData?.files ?? [])];
  if (files.length === 0) return;

  event.preventDefault();
  await chats.attachFiles(files);
}

// Переключились на другой чат — подставляем его черновик и пересчитываем высоту.
watch(
  () => chats.activeChatId,
  async () => {
    await nextTick();
    autosize();
    textarea.value?.focus();
  },
);

watch(() => draft.value.text, () => void nextTick(autosize));

onMounted(() => {
  // Второй замер после первой отрисовки: на момент mounted стили ещё могут не примениться.
  autosize();
  requestAnimationFrame(autosize);
  textarea.value?.focus();
});
</script>

<template>
  <div class="composer-wrap">
    <div
      class="composer"
      :class="{ 'composer--dragging': dragging }"
      @dragenter.prevent="onDragEnter"
      @dragover.prevent
      @dragleave="onDragLeave"
      @drop.prevent="onDrop"
    >
      <div v-if="dragging" class="composer__dropzone">
        <AppIcon name="download" :size="22" />
        Отпусти файлы, чтобы прикрепить
      </div>

      <div v-if="draft.attachments.length > 0 || chats.uploadingCount > 0" class="composer__attachments">
        <AttachmentChip
          v-for="attachment in draft.attachments"
          :key="attachment.id"
          :attachment="attachment"
          removable
          @remove="chats.removeAttachment(attachment.id)"
        />

        <div v-for="n in chats.uploadingCount" :key="`up-${n}`" class="uploading">
          <span class="uploading__spinner" />
          <span class="uploading__text">Загрузка…</span>
        </div>
      </div>

      <div class="composer__row">
        <button
          class="composer__attach"
          title="Прикрепить файл"
          aria-label="Прикрепить файл"
          @click="pickFiles"
        >
          <AppIcon name="paperclip" :size="19" />
        </button>

        <textarea
          ref="textarea"
          class="composer__input"
          rows="1"
          placeholder="Спроси что-нибудь…"
          :value="draft.text"
          @input="onInput"
          @keydown="onKeydown"
          @paste="onPaste"
        />

        <button
          v-if="streaming"
          class="composer__send composer__send--stop"
          title="Остановить ответ"
          aria-label="Остановить ответ"
          @click="chats.stopStreaming()"
        >
          <AppIcon name="stop" :size="18" />
        </button>

        <button
          v-else
          class="composer__send"
          :disabled="!chats.canSend"
          title="Отправить"
          aria-label="Отправить"
          @click="send"
        >
          <span v-if="chats.sending" class="composer__spinner" />
          <AppIcon v-else name="send" :size="18" />
        </button>
      </div>

      <!--
        Пока принимаются только текстовые файлы: бэкенд читает их как текст и кладёт в память.
        Двоичные форматы (pdf, docx) потребуют извлечения текста — этого ещё нет.
      -->
      <input
        ref="fileInput"
        type="file"
        multiple
        accept=".txt,.md,.csv,.log,.json,.xml,.yaml,.yml,text/plain"
        class="visually-hidden"
        @change="onFilesPicked"
      />
    </div>

    <p class="composer__hint">
      <kbd>Enter</kbd> — отправить · <kbd>Shift</kbd>+<kbd>Enter</kbd> — новая строка · файлы можно
      перетащить сюда
    </p>
  </div>
</template>

<style scoped>
.composer-wrap {
  flex-shrink: 0;
  width: 100%;
  max-width: var(--content-max-width);
  margin: 0 auto;
  padding: 8px 24px 16px;
}

.composer {
  position: relative;
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  background: var(--bg-surface);
  box-shadow: var(--shadow-md);
  transition: border-color var(--dur-base) var(--ease-out), box-shadow var(--dur-base) var(--ease-out);
}

.composer:focus-within {
  border-color: var(--accent);
  box-shadow: var(--shadow-md), 0 0 0 3px var(--accent-soft);
}

.composer--dragging {
  border-color: var(--accent);
  border-style: dashed;
}

.composer__dropzone {
  position: absolute;
  inset: 0;
  z-index: 2;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border-radius: var(--radius-lg);
  background: color-mix(in srgb, var(--bg-surface) 92%, transparent);
  color: var(--accent);
  font-size: var(--text-sm);
  font-weight: 550;
  pointer-events: none;
}

.composer__attachments {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  padding: 12px 14px 4px;
}

.composer__row {
  display: flex;
  align-items: flex-end;
  gap: 6px;
  padding: 8px;
}

.composer__attach {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  flex-shrink: 0;
  border-radius: var(--radius-sm);
  color: var(--text-muted);
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.composer__attach:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.composer__input {
  flex: 1;
  min-width: 0;
  min-height: 38px;
  max-height: 220px;
  padding: 9px 4px;
  border: none;
  background: none;
  resize: none;
  line-height: 1.5;
  overflow-y: auto;
}

.composer__input::placeholder {
  color: var(--text-muted);
}

.composer__send {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  flex-shrink: 0;
  border-radius: var(--radius-sm);
  background: var(--accent-gradient);
  color: var(--accent-contrast);
  box-shadow: var(--shadow-accent);
  transition: filter var(--dur-fast) var(--ease-out), opacity var(--dur-fast) var(--ease-out),
    transform var(--dur-fast) var(--ease-out);
}

.composer__send:hover:not(:disabled) {
  filter: brightness(1.08);
}

.composer__send:active:not(:disabled) {
  transform: scale(0.94);
}

.composer__send:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  box-shadow: none;
}

.composer__send--stop {
  background: var(--danger-soft);
  color: var(--danger);
  box-shadow: none;
}

.composer__spinner {
  width: 16px;
  height: 16px;
  border: 2px solid currentColor;
  border-top-color: transparent;
  border-radius: 50%;
  animation: af-spin 0.7s linear infinite;
}

.uploading {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 9px 12px;
  border: 1px dashed var(--border-strong);
  border-radius: var(--radius-sm);
}

.uploading__spinner {
  width: 14px;
  height: 14px;
  border: 2px solid var(--accent);
  border-top-color: transparent;
  border-radius: 50%;
  animation: af-spin 0.7s linear infinite;
}

.uploading__text {
  font-size: var(--text-sm);
  color: var(--text-muted);
}

.composer__hint {
  margin-top: 8px;
  text-align: center;
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.composer__hint kbd {
  padding: 1px 5px;
  border: 1px solid var(--border);
  border-radius: 4px;
  background: var(--bg-surface-2);
  font-family: var(--font-mono);
  font-size: 10.5px;
}

@media (max-width: 640px) {
  .composer-wrap {
    padding: 8px 12px 12px;
  }

  .composer__hint {
    display: none;
  }
}
</style>
