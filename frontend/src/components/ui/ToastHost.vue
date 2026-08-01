<script setup lang="ts">
import { useToastStore } from '@/stores/toasts';

import AppIcon from './AppIcon.vue';

const toasts = useToastStore();

const iconOf = { info: 'info', success: 'check-circle', error: 'alert' } as const;
</script>

<template>
  <Teleport to="body">
    <TransitionGroup tag="div" name="toast" class="toast-host">
      <div
        v-for="toast in toasts.items"
        :key="toast.id"
        class="toast surface-glass"
        :class="`toast--${toast.kind}`"
        role="status"
      >
        <AppIcon :name="iconOf[toast.kind]" :size="17" class="toast__icon" />
        <p class="toast__text">{{ toast.text }}</p>
        <button class="toast__close" aria-label="Закрыть" @click="toasts.dismiss(toast.id)">
          <AppIcon name="x" :size="14" />
        </button>
      </div>
    </TransitionGroup>
  </Teleport>
</template>

<style scoped>
.toast-host {
  position: fixed;
  right: 20px;
  bottom: 20px;
  z-index: var(--z-toast);
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-width: min(420px, calc(100vw - 40px));
  pointer-events: none;
}

.toast {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 12px 12px 12px 14px;
  border: 1px solid var(--border);
  border-left: 3px solid var(--text-muted);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-lg);
  pointer-events: auto;
}

.toast--info {
  border-left-color: var(--accent);
}
.toast--success {
  border-left-color: var(--success);
}
.toast--error {
  border-left-color: var(--danger);
}

.toast__icon {
  flex-shrink: 0;
  margin-top: 2px;
}

.toast--info .toast__icon {
  color: var(--accent);
}
.toast--success .toast__icon {
  color: var(--success);
}
.toast--error .toast__icon {
  color: var(--danger);
}

.toast__text {
  flex: 1;
  font-size: var(--text-sm);
  line-height: 1.5;
  color: var(--text-primary);
}

.toast__close {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: var(--radius-xs);
  color: var(--text-muted);
}

.toast__close:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.toast-enter-active,
.toast-leave-active {
  transition:
    opacity var(--dur-base) var(--ease-out),
    transform var(--dur-base) var(--ease-out);
}

.toast-enter-from {
  opacity: 0;
  transform: translateX(24px);
}

.toast-leave-to {
  opacity: 0;
  transform: translateX(24px) scale(0.96);
}

.toast-leave-active {
  position: absolute;
  right: 0;
  bottom: 0;
}
</style>
