<script setup lang="ts">
import { computed } from 'vue';

import { initials } from '@/utils/format';

const props = withDefaults(
  defineProps<{
    name: string;
    size?: number;
    /** Аватар ассистента — красится градиентом акцента. */
    accent?: boolean;
    /** Готовый символ вместо инициалов (иконка агента). */
    glyph?: string | null;
  }>(),
  { size: 32, accent: false, glyph: null },
);

const label = computed(() => props.glyph ?? initials(props.name));

/** Стабильный оттенок из имени — чтобы у разных людей были разные аватарки. */
const hue = computed(() => {
  let hash = 0;
  for (let i = 0; i < props.name.length; i++) {
    hash = (hash * 31 + props.name.charCodeAt(i)) % 360;
  }
  return hash;
});
</script>

<template>
  <div
    class="avatar"
    :class="{ 'avatar--accent': accent }"
    :style="{
      width: `${size}px`,
      height: `${size}px`,
      fontSize: `${Math.round(size * 0.4)}px`,
      '--avatar-hue': hue,
    }"
    aria-hidden="true"
  >
    {{ label }}
  </div>
</template>

<style scoped>
.avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  border-radius: var(--radius-sm);
  font-weight: 600;
  letter-spacing: 0.02em;
  color: #fff;
  background: linear-gradient(
    135deg,
    hsl(calc(var(--avatar-hue) * 1deg) 62% 55%),
    hsl(calc((var(--avatar-hue) + 42) * 1deg) 62% 46%)
  );
  user-select: none;
}

.avatar--accent {
  background: var(--accent-gradient);
  color: var(--accent-contrast);
  box-shadow: var(--shadow-accent);
}
</style>
