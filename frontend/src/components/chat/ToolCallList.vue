<script setup lang="ts">
/**
 * Показывает, чем занят агент, пока текста ответа ещё нет.
 *
 * Между «агент решил вызвать инструмент» и первым токеном ответа может пройти много времени:
 * загрузка скилла, поиск по базе знаний, чтение файла. Без этой плашки в UI просто крутятся
 * три точки, и понять, живой процесс или завис, невозможно.
 *
 * Список сворачивается: пока идёт работа — раскрыт, после ответа схлопывается в одну строку,
 * чтобы не мешать читать сам ответ.
 */
import { computed, ref, watch } from 'vue';

import type { ToolCallResponse } from '@/api';
import AppIcon from '@/components/ui/AppIcon.vue';

const props = defineProps<{ calls: ToolCallResponse[] }>();

/** Человеческие названия для инструментов, которые пользователь увидит чаще всего. */
const labels: Record<string, string> = {
  get_current_time: 'узнаёт текущее время',
  get_days_between: 'считает дни между датами',
  get_random_number: 'бросает кубик',
  get_text_statistics: 'считает статистику текста',
  new_guid: 'генерирует идентификатор',
  load_skill: 'читает скилл',
  read_skill_resource: 'читает материалы скилла',
  run_skill_script: 'запускает скрипт скилла',
  search_knowledge_base: 'ищет в базе знаний',
  todos_add: 'планирует задачи',
  todos_complete: 'закрывает задачу',
  todos_get_remaining: 'сверяется с планом',
  mode_set: 'меняет режим работы',
  file_access_read: 'читает файл',
  file_access_write: 'пишет файл',
  file_access_grep: 'ищет по файлам',
  file_access_ls: 'смотрит список файлов',
  file_memory_write: 'запоминает',
  file_memory_read: 'вспоминает',
};

const running = computed(() => props.calls.some((call) => call.status === 'running'));
const failed = computed(() => props.calls.filter((call) => call.status === 'failed').length);

const expanded = ref(true);

// Работа закончилась — сворачиваем, но только если ничего не упало: ошибку человек должен увидеть.
watch(running, (isRunning) => {
  if (!isRunning && failed.value === 0) expanded.value = false;
});

const summary = computed(() => {
  if (running.value) {
    const current = props.calls.find((call) => call.status === 'running');
    return current ? describe(current) : 'работает';
  }

  const word = plural(props.calls.length, 'инструмент', 'инструмента', 'инструментов');
  const tail = failed.value > 0 ? `, ${failed.value} с ошибкой` : '';

  return `Использовал ${props.calls.length} ${word}${tail}`;
});

function describe(call: ToolCallResponse): string {
  return labels[call.name] ?? `вызывает ${call.name}`;
}

/** Аргументы показываем как есть — это отладочная информация, а не текст для пользователя. */
function argumentsOf(call: ToolCallResponse): string | null {
  if (!call.arguments) return null;

  try {
    return JSON.stringify(JSON.parse(call.arguments));
  } catch {
    return call.arguments;
  }
}

function plural(count: number, one: string, few: string, many: string): string {
  const mod100 = count % 100;
  if (mod100 >= 11 && mod100 <= 14) return many;

  switch (count % 10) {
    case 1:
      return one;
    case 2:
    case 3:
    case 4:
      return few;
    default:
      return many;
  }
}
</script>

<template>
  <div class="tools" :class="{ 'tools--running': running }">
    <button
      class="tools__head"
      type="button"
      :aria-expanded="expanded"
      @click="expanded = !expanded"
    >
      <span class="tools__status" aria-hidden="true">
        <span v-if="running" class="spinner" />
        <AppIcon v-else-if="failed > 0" name="alert" :size="13" />
        <AppIcon v-else name="check" :size="13" />
      </span>

      <span class="tools__summary">{{ summary }}</span>

      <AppIcon
        class="tools__chevron"
        :class="{ 'tools__chevron--open': expanded }"
        name="chevron"
        :size="13"
      />
    </button>

    <ul v-if="expanded" class="tools__list">
      <li v-for="call in calls" :key="call.id" class="tools__item" :class="`is-${call.status}`">
        <span class="tools__icon" aria-hidden="true">
          <span v-if="call.status === 'running'" class="spinner" />
          <AppIcon v-else-if="call.status === 'failed'" name="alert" :size="12" />
          <AppIcon v-else name="check" :size="12" />
        </span>

        <span class="tools__body">
          <code class="tools__name">{{ call.name }}</code>
          <span v-if="argumentsOf(call)" class="tools__args">{{ argumentsOf(call) }}</span>
          <span v-if="call.error" class="tools__error">{{ call.error }}</span>
        </span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.tools {
  margin-bottom: 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-surface-2);
  overflow: hidden;
}

.tools--running {
  border-color: var(--accent-soft);
}

.tools__head {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 7px 10px;
  background: none;
  border: 0;
  text-align: left;
  color: var(--text-secondary);
  font-size: var(--text-sm);
  cursor: pointer;
}

.tools__head:hover {
  color: var(--text-primary);
}

.tools__status {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  flex-shrink: 0;
  color: var(--accent);
}

.tools__summary {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tools--running .tools__summary {
  color: var(--text-primary);
}

.tools__chevron {
  flex-shrink: 0;
  color: var(--text-muted);
  transition: transform var(--dur-fast) var(--ease-out);
}

.tools__chevron--open {
  transform: rotate(180deg);
}

.tools__list {
  margin: 0;
  padding: 0 10px 8px 10px;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.tools__item {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  font-size: var(--text-sm);
}

.tools__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 18px;
  flex-shrink: 0;
  color: var(--text-muted);
}

.tools__item.is-running .tools__icon {
  color: var(--accent);
}

.tools__item.is-failed .tools__icon {
  color: var(--danger);
}

.tools__body {
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 3px 8px;
}

.tools__name {
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: var(--text-xs);
  color: var(--text-primary);
}

.tools__args {
  min-width: 0;
  font-size: var(--text-xs);
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

.tools__error {
  flex-basis: 100%;
  font-size: var(--text-xs);
  color: var(--danger);
}

.spinner {
  width: 11px;
  height: 11px;
  border: 1.5px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: af-tool-spin 0.7s linear infinite;
}

@keyframes af-tool-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .spinner {
    animation-duration: 2s;
  }
}
</style>
