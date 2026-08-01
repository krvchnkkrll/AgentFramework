<script setup lang="ts">
import AppIcon from './AppIcon.vue';

withDefaults(
  defineProps<{
    icon?: string;
    danger?: boolean;
    active?: boolean;
  }>(),
  { danger: false, active: false },
);
</script>

<template>
  <button class="menu-item" :class="{ 'menu-item--danger': danger, 'menu-item--active': active }" role="menuitem">
    <AppIcon v-if="icon" :name="icon" :size="16" />
    <span class="menu-item__label"><slot /></span>
    <span v-if="$slots.trailing" class="menu-item__trailing"><slot name="trailing" /></span>
  </button>
</template>

<style scoped>
.menu-item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 8px 10px;
  border-radius: var(--radius-xs);
  font-size: var(--text-base);
  color: var(--text-secondary);
  text-align: left;
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.menu-item:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.menu-item--active {
  color: var(--text-primary);
  background: var(--accent-soft);
}

.menu-item--danger {
  color: var(--danger);
}

.menu-item--danger:hover {
  background: var(--danger-soft);
  color: var(--danger);
}

.menu-item__label {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.menu-item__trailing {
  display: flex;
  align-items: center;
  color: var(--text-muted);
}
</style>
