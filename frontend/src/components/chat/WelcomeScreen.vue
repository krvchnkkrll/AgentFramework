<script setup lang="ts">
import AppIcon from '@/components/ui/AppIcon.vue';
import { useChatsStore } from '@/stores/chats';
import { useUserStore } from '@/stores/user';

const chats = useChatsStore();
const user = useUserStore();

/** Быстрые подсказки — просто подставляют текст в поле ввода. */
const suggestions = [
  {
    icon: 'sparkles',
    title: 'Объясни концепцию',
    prompt: 'Объясни простыми словами, что такое CQRS и когда он оправдан.',
  },
  {
    icon: 'file',
    title: 'Разбери документ',
    prompt: 'Прикрепляю документ — сделай краткую выжимку по ключевым пунктам.',
  },
  {
    icon: 'settings',
    title: 'Помоги с кодом',
    prompt: 'Посмотри этот код и предложи, как его упростить без потери читаемости.',
  },
  {
    icon: 'message',
    title: 'Составь план',
    prompt: 'Составь пошаговый план внедрения авторизации через Keycloak в SPA.',
  },
];

function use(prompt: string): void {
  chats.setDraftText(prompt);
}
</script>

<template>
  <div class="welcome">
    <div class="welcome__inner">
      <span class="welcome__mark" aria-hidden="true">
        <AppIcon name="sparkles" :size="28" />
      </span>

      <h1 class="welcome__title">Привет, {{ user.displayName }}</h1>
      <p class="welcome__subtitle">
        Задай вопрос или прикрепи файл — начнём новый чат.
      </p>

      <ul class="welcome__cards">
        <li v-for="item in suggestions" :key="item.title">
          <button class="card" @click="use(item.prompt)">
            <span class="card__icon"><AppIcon :name="item.icon" :size="17" /></span>
            <span class="card__text">
              <span class="card__title">{{ item.title }}</span>
              <span class="card__prompt">{{ item.prompt }}</span>
            </span>
          </button>
        </li>
      </ul>
    </div>
  </div>
</template>

<style scoped>
.welcome {
  flex: 1;
  min-height: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow-y: auto;
  padding: 32px 24px;
}

.welcome__inner {
  width: 100%;
  max-width: var(--content-max-width);
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  animation: af-rise-in var(--dur-slow) var(--ease-out) both;
}

.welcome__mark {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 56px;
  height: 56px;
  margin-bottom: 18px;
  border-radius: var(--radius-md);
  background: var(--accent-gradient);
  color: var(--accent-contrast);
  box-shadow: var(--shadow-accent);
}

.welcome__title {
  font-size: var(--text-2xl);
  font-weight: 650;
  letter-spacing: -0.025em;
  background: var(--accent-gradient);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.welcome__subtitle {
  margin-top: 8px;
  font-size: var(--text-md);
  color: var(--text-secondary);
}

.welcome__cards {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  width: 100%;
  margin-top: 30px;
  list-style: none;
}

.card {
  display: flex;
  align-items: flex-start;
  gap: 11px;
  width: 100%;
  height: 100%;
  padding: 14px;
  text-align: left;
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-md);
  transition:
    border-color var(--dur-base) var(--ease-out),
    transform var(--dur-base) var(--ease-out),
    box-shadow var(--dur-base) var(--ease-out);
}

.card:hover {
  border-color: var(--accent);
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

.card__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
  border-radius: var(--radius-xs);
  background: var(--accent-soft);
  color: var(--accent);
}

.card__text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.card__title {
  font-size: var(--text-base);
  font-weight: 570;
}

.card__prompt {
  font-size: var(--text-xs);
  line-height: 1.45;
  color: var(--text-muted);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

@media (max-width: 640px) {
  .welcome__cards {
    grid-template-columns: 1fr;
  }
}
</style>
