<script setup lang="ts">
import AppIcon from './AppIcon.vue';

withDefaults(
  defineProps<{
    variant?: 'primary' | 'ghost' | 'subtle' | 'danger';
    size?: 'sm' | 'md' | 'lg';
    /** Имя иконки слева от текста. */
    icon?: string;
    /** Квадратная кнопка только с иконкой. */
    iconOnly?: boolean;
    disabled?: boolean;
    loading?: boolean;
    title?: string;
    type?: 'button' | 'submit';
  }>(),
  { variant: 'ghost', size: 'md', type: 'button' },
);
</script>

<template>
  <button
    :class="['btn', `btn--${variant}`, `btn--${size}`, { 'btn--icon-only': iconOnly }]"
    :disabled="disabled || loading"
    :title="title"
    :aria-label="iconOnly ? title : undefined"
    :type="type"
  >
    <span v-if="loading" class="btn__spinner" />
    <AppIcon v-else-if="icon" :name="icon" :size="size === 'sm' ? 15 : size === 'lg' ? 20 : 17" />
    <slot />
  </button>
</template>

<style scoped>
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  border-radius: var(--radius-sm);
  font-weight: 500;
  white-space: nowrap;
  transition:
    background-color var(--dur-fast) var(--ease-out),
    color var(--dur-fast) var(--ease-out),
    box-shadow var(--dur-base) var(--ease-out),
    transform var(--dur-fast) var(--ease-out),
    opacity var(--dur-fast) var(--ease-out);
}

.btn:active:not(:disabled) {
  transform: scale(0.97);
}

.btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

/* Размеры */
.btn--sm {
  height: 30px;
  padding: 0 10px;
  font-size: var(--text-sm);
}
.btn--md {
  height: 36px;
  padding: 0 14px;
  font-size: var(--text-base);
}
.btn--lg {
  height: 44px;
  padding: 0 20px;
  font-size: var(--text-md);
}

.btn--icon-only.btn--sm {
  width: 30px;
  padding: 0;
}
.btn--icon-only.btn--md {
  width: 36px;
  padding: 0;
}
.btn--icon-only.btn--lg {
  width: 44px;
  padding: 0;
}

/* Варианты */
.btn--primary {
  background: var(--accent-gradient);
  color: var(--accent-contrast);
  box-shadow: var(--shadow-accent);
  font-weight: 560;
}

.btn--primary:hover:not(:disabled) {
  filter: brightness(1.08);
}

.btn--ghost {
  color: var(--text-secondary);
}

.btn--ghost:hover:not(:disabled) {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.btn--subtle {
  background: var(--bg-surface-2);
  color: var(--text-primary);
  border: 1px solid var(--border);
}

.btn--subtle:hover:not(:disabled) {
  background: var(--bg-elevated);
  border-color: var(--border-strong);
}

.btn--danger {
  color: var(--danger);
}

.btn--danger:hover:not(:disabled) {
  background: var(--danger-soft);
}

.btn__spinner {
  width: 15px;
  height: 15px;
  border: 2px solid currentColor;
  border-top-color: transparent;
  border-radius: 50%;
  animation: af-spin 0.7s linear infinite;
  flex-shrink: 0;
}
</style>
