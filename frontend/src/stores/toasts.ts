/** Всплывающие уведомления в правом нижнем углу. */
import { defineStore } from 'pinia';
import { ref } from 'vue';

export type ToastKind = 'info' | 'success' | 'error';

export interface Toast {
  id: number;
  kind: ToastKind;
  text: string;
}

let nextId = 1;

export const useToastStore = defineStore('toasts', () => {
  const items = ref<Toast[]>([]);

  function push(text: string, kind: ToastKind = 'info', timeoutMs = 4500): void {
    const toast: Toast = { id: nextId++, kind, text };
    items.value.push(toast);
    window.setTimeout(() => dismiss(toast.id), timeoutMs);
  }

  function dismiss(id: number): void {
    items.value = items.value.filter((t) => t.id !== id);
  }

  return {
    items,
    push,
    dismiss,
    info: (text: string) => push(text, 'info'),
    success: (text: string) => push(text, 'success'),
    error: (text: string) => push(text, 'error', 7000),
  };
});
