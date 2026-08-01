<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { RouterView, useRoute } from 'vue-router';

import ChatSidebar from '@/components/sidebar/ChatSidebar.vue';
import ToastHost from '@/components/ui/ToastHost.vue';
import { useChatsStore } from '@/stores/chats';
import { useUserStore } from '@/stores/user';

const route = useRoute();
const chats = useChatsStore();
const user = useUserStore();

const SIDEBAR_KEY = 'af.sidebar.collapsed';
const MOBILE_QUERY = '(max-width: 899px)';

const mobileQuery = window.matchMedia(MOBILE_QUERY);

/** Узкий экран: сайдбар превращается в оверлей и по умолчанию закрыт. */
const isMobile = ref(mobileQuery.matches);
/** Пожелание пользователя для широкого экрана — переживает перезагрузку. */
const desktopCollapsed = ref(readCollapsed());
/** Открыт ли оверлей на узком экране (каждый раз начинаем с закрытого). */
const mobileOpen = ref(false);

const collapsed = computed(() =>
  isMobile.value ? !mobileOpen.value : desktopCollapsed.value,
);

const overlayOpen = computed(() => isMobile.value && mobileOpen.value);

const chatId = computed(() => {
  const value = route.params.chatId;
  return typeof value === 'string' ? value : undefined;
});

function toggleSidebar(): void {
  if (isMobile.value) {
    mobileOpen.value = !mobileOpen.value;
    return;
  }

  desktopCollapsed.value = !desktopCollapsed.value;
  try {
    localStorage.setItem(SIDEBAR_KEY, String(desktopCollapsed.value));
  } catch {
    // приватный режим
  }
}

function onBreakpointChange(event: MediaQueryListEvent): void {
  isMobile.value = event.matches;
  if (!event.matches) mobileOpen.value = false;
}

/** Ctrl/Cmd+K — фокус в поиск, Ctrl/Cmd+B — свернуть сайдбар. */
function onKeydown(event: KeyboardEvent): void {
  const meta = event.metaKey || event.ctrlKey;
  if (!meta) return;

  if (event.key.toLowerCase() === 'b') {
    event.preventDefault();
    toggleSidebar();
  }

  if (event.key.toLowerCase() === 'k') {
    event.preventDefault();
    if (isMobile.value) mobileOpen.value = true;
    else desktopCollapsed.value = false;
    requestAnimationFrame(() => {
      document.querySelector<HTMLInputElement>('.sidebar__search-input')?.focus();
    });
  }
}

onMounted(async () => {
  mobileQuery.addEventListener('change', onBreakpointChange);
  window.addEventListener('keydown', onKeydown);

  await Promise.all([user.load(), chats.init()]);
});

onBeforeUnmount(() => {
  mobileQuery.removeEventListener('change', onBreakpointChange);
  window.removeEventListener('keydown', onKeydown);
});

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(SIDEBAR_KEY) === 'true';
  } catch {
    return false;
  }
}
</script>

<template>
  <div class="shell" :class="{ 'shell--mobile': isMobile }">
    <Transition name="sidebar">
      <ChatSidebar
        v-if="!collapsed"
        :class="{ sidebar__overlay: overlayOpen }"
        @toggle="toggleSidebar"
      />
    </Transition>

    <Transition name="scrim">
      <div v-if="overlayOpen" class="scrim" @click="toggleSidebar" />
    </Transition>

    <main class="shell__main">
      <RouterView v-slot="{ Component }">
        <component
          :is="Component"
          :chat-id="chatId"
          :sidebar-collapsed="collapsed"
          @toggle-sidebar="toggleSidebar"
        />
      </RouterView>
    </main>

    <ToastHost />
  </div>
</template>

<style scoped>
.shell {
  display: flex;
  height: 100%;
  overflow: hidden;
}

.shell__main {
  display: flex;
  flex: 1;
  min-width: 0;
  height: 100%;
}

.sidebar__overlay {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  z-index: var(--z-sidebar);
  box-shadow: var(--shadow-lg);
}

.scrim {
  position: fixed;
  inset: 0;
  z-index: calc(var(--z-sidebar) - 1);
  background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(2px);
}

.sidebar-enter-active,
.sidebar-leave-active {
  transition: margin-left var(--dur-base) var(--ease-out), opacity var(--dur-base) var(--ease-out);
}

.sidebar-enter-from,
.sidebar-leave-to {
  margin-left: calc(var(--sidebar-width) * -1);
  opacity: 0;
}

.scrim-enter-active,
.scrim-leave-active {
  transition: opacity var(--dur-base) var(--ease-out);
}

.scrim-enter-from,
.scrim-leave-to {
  opacity: 0;
}
</style>
