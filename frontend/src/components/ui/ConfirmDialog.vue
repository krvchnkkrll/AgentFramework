<script setup lang="ts">
import { nextTick, ref, watch } from 'vue';

import AppButton from './AppButton.vue';

const props = withDefaults(
  defineProps<{
    open: boolean;
    title: string;
    message?: string;
    confirmLabel?: string;
    cancelLabel?: string;
    danger?: boolean;
  }>(),
  { confirmLabel: 'Подтвердить', cancelLabel: 'Отмена', danger: false },
);

const emit = defineEmits<{ confirm: []; cancel: [] }>();

const confirmButton = ref<InstanceType<typeof AppButton> | null>(null);

watch(
  () => props.open,
  async (open) => {
    if (!open) return;
    await nextTick();
    (confirmButton.value?.$el as HTMLButtonElement | undefined)?.focus();
  },
);

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') emit('cancel');
}
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="open"
        class="modal"
        role="dialog"
        aria-modal="true"
        tabindex="-1"
        @keydown="onKeydown"
        @click.self="emit('cancel')"
      >
        <div class="modal__card">
          <h2 class="modal__title">{{ title }}</h2>
          <p v-if="message" class="modal__message">{{ message }}</p>

          <div class="modal__actions">
            <AppButton variant="subtle" @click="emit('cancel')">{{ cancelLabel }}</AppButton>
            <AppButton
              ref="confirmButton"
              :variant="danger ? 'danger' : 'primary'"
              @click="emit('confirm')"
            >
              {{ confirmLabel }}
            </AppButton>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal {
  position: fixed;
  inset: 0;
  z-index: var(--z-modal);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(4px);
}

.modal__card {
  width: min(420px, 100%);
  padding: 22px;
  background: var(--bg-elevated);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
}

.modal__title {
  font-size: var(--text-lg);
  font-weight: 620;
  letter-spacing: -0.01em;
}

.modal__message {
  margin-top: 8px;
  font-size: var(--text-base);
  line-height: 1.55;
  color: var(--text-secondary);
}

.modal__actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 22px;
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity var(--dur-base) var(--ease-out);
}

.modal-enter-active .modal__card,
.modal-leave-active .modal__card {
  transition: transform var(--dur-base) var(--ease-out);
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .modal__card,
.modal-leave-to .modal__card {
  transform: scale(0.95) translateY(8px);
}
</style>
