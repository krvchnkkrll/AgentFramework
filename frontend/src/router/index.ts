import { createRouter, createWebHistory } from 'vue-router';

import ChatView from '@/views/ChatView.vue';

/**
 * Пропсы во ChatView прокидываются из App.vue через слот RouterView —
 * так туда попадает и состояние сайдбара, которое живёт в шелле.
 */
export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: { name: 'new-chat' } },
    { path: '/chat', name: 'new-chat', component: ChatView },
    { path: '/chat/:chatId', name: 'chat', component: ChatView },
    // Неизвестный адрес — на экран нового чата.
    { path: '/:pathMatch(.*)*', redirect: { name: 'new-chat' } },
  ],
});
