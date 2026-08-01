<script setup lang="ts">
import { computed } from 'vue';

import type { AttachmentResponse } from '@/api';
import AppIcon from '@/components/ui/AppIcon.vue';
import { fileExtension, formatFileSize, isImage } from '@/utils/format';

const props = withDefaults(
  defineProps<{
    attachment: AttachmentResponse;
    /** Показывать крестик удаления (в поле ввода). */
    removable?: boolean;
  }>(),
  { removable: false },
);

const emit = defineEmits<{ remove: [] }>();

const preview = computed(() =>
  isImage(props.attachment.contentType) && props.attachment.url ? props.attachment.url : null,
);
</script>

<template>
  <div class="chip" :class="{ 'chip--image': preview }">
    <a
      v-if="attachment.url"
      class="chip__link"
      :href="attachment.url"
      :download="attachment.fileName"
      target="_blank"
      rel="noopener"
    >
      <span class="chip__thumb">
        <img v-if="preview" :src="preview" :alt="attachment.fileName" />
        <span v-else class="chip__ext">{{ fileExtension(attachment.fileName) }}</span>
      </span>

      <span class="chip__text">
        <span class="chip__name">{{ attachment.fileName }}</span>
        <span class="chip__size">{{ formatFileSize(attachment.size) }}</span>
      </span>
    </a>

    <div v-else class="chip__link chip__link--static">
      <span class="chip__thumb">
        <span class="chip__ext">{{ fileExtension(attachment.fileName) }}</span>
      </span>
      <span class="chip__text">
        <span class="chip__name">{{ attachment.fileName }}</span>
        <span class="chip__size">{{ formatFileSize(attachment.size) }}</span>
      </span>
    </div>

    <button v-if="removable" class="chip__remove" aria-label="Убрать вложение" @click="emit('remove')">
      <AppIcon name="x" :size="13" :stroke-width="2.2" />
    </button>
  </div>
</template>

<style scoped>
.chip {
  position: relative;
  display: inline-flex;
  align-items: center;
  max-width: 250px;
  background: var(--bg-surface-2);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  transition: border-color var(--dur-fast) var(--ease-out);
}

.chip:hover {
  border-color: var(--border-strong);
}

.chip__link {
  display: flex;
  align-items: center;
  gap: 9px;
  min-width: 0;
  padding: 6px 10px 6px 6px;
  color: inherit;
  text-decoration: none;
}

.chip__link:hover {
  text-decoration: none;
}

.chip__thumb {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
  overflow: hidden;
  border-radius: var(--radius-xs);
  background: var(--accent-soft);
}

.chip__thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.chip__ext {
  font-size: 9.5px;
  font-weight: 700;
  letter-spacing: 0.03em;
  color: var(--accent);
}

.chip__text {
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.chip__name {
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chip__size {
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.chip__remove {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  position: absolute;
  top: -7px;
  right: -7px;
  border-radius: var(--radius-pill);
  background: var(--bg-elevated);
  border: 1px solid var(--border-strong);
  color: var(--text-secondary);
  box-shadow: var(--shadow-sm);
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.chip__remove:hover {
  background: var(--danger);
  border-color: var(--danger);
  color: #fff;
}
</style>
