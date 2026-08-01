<script setup lang="ts">
/**
 * Выпадающая панель: закрывается по клику вне, Escape и по событию close из слота.
 * Содержимое передаётся через слот panel, триггер — через слот trigger.
 */
import { onBeforeUnmount, onMounted, ref } from 'vue';

withDefaults(
  defineProps<{
    /** С какой стороны прижимать панель к триггеру. */
    align?: 'left' | 'right';
    /** Открывать вверх (для меню в подвале сайдбара). */
    up?: boolean;
    width?: string;
  }>(),
  { align: 'right', up: false, width: '240px' },
);

const open = ref(false);
const root = ref<HTMLElement | null>(null);

function toggle(): void {
  open.value = !open.value;
}

function close(): void {
  open.value = false;
}

function onDocumentPointerDown(event: PointerEvent): void {
  if (!open.value) return;
  if (root.value && !root.value.contains(event.target as Node)) close();
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && open.value) {
    close();
    event.stopPropagation();
  }
}

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointerDown);
  document.addEventListener('keydown', onKeydown);
});

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointerDown);
  document.removeEventListener('keydown', onKeydown);
});

defineExpose({ close });
</script>

<template>
  <div ref="root" class="dropdown">
    <div class="dropdown__trigger" @click="toggle">
      <slot name="trigger" :open="open" />
    </div>

    <Transition name="dropdown">
      <div
        v-if="open"
        class="dropdown__panel surface-glass"
        :class="[`dropdown__panel--${align}`, { 'dropdown__panel--up': up }]"
        :style="{ width }"
        role="menu"
      >
        <slot name="panel" :close="close" />
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.dropdown {
  position: relative;
}

.dropdown__trigger {
  display: contents;
}

.dropdown__panel {
  position: absolute;
  top: calc(100% + 8px);
  z-index: var(--z-dropdown);
  padding: 6px;
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-lg);
  transform-origin: top right;
}

.dropdown__panel--left {
  left: 0;
  transform-origin: top left;
}

.dropdown__panel--right {
  right: 0;
}

.dropdown__panel--up {
  top: auto;
  bottom: calc(100% + 8px);
  transform-origin: bottom left;
}

.dropdown-enter-active,
.dropdown-leave-active {
  transition:
    opacity var(--dur-fast) var(--ease-out),
    transform var(--dur-fast) var(--ease-out);
}

.dropdown-enter-from,
.dropdown-leave-to {
  opacity: 0;
  transform: scale(0.96) translateY(-4px);
}

.dropdown__panel--up.dropdown-enter-from,
.dropdown__panel--up.dropdown-leave-to {
  transform: scale(0.96) translateY(4px);
}
</style>
