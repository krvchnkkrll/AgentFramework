/**
 * Основной стор: список чатов, сообщения, черновики и стриминг ответа.
 *
 * Ходит только через интерфейс ChatsApi, поэтому переключение
 * «моки ↔ реальный бэкенд» не требует правок здесь.
 */
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

import { ApiError, chatsApi } from '@/api';
import type {
  AgentResponse,
  AttachmentResponse,
  ChatResponse,
  MessageResponse,
  SaveAgentRequest,
  SkillResponse,
} from '@/api';
import { BUILT_IN_AGENT_ID, builtInAgent } from '@/api/builtInAgent';
import { config } from '@/config';
import { onChatRenamed } from '@/realtime/chatHub';
import {
  bucketLabels,
  bucketOf,
  bucketOrder,
  titleFromContent,
  type ChatBucket,
} from '@/utils/format';

import { useToastStore } from './toasts';

/** Черновик сообщения для конкретного чата (или для ещё не созданного — ключ NEW_CHAT). */
export interface Draft {
  text: string;
  attachments: AttachmentResponse[];
}

export interface ChatGroup {
  key: ChatBucket;
  label: string;
  chats: ChatResponse[];
}

/** Ключ черновика для ещё не созданного чата. */
export const NEW_CHAT = '__new__';

const emptyDraft = (): Draft => ({ text: '', attachments: [] });

export const useChatsStore = defineStore('chats', () => {
  const toasts = useToastStore();

  const chats = ref<ChatResponse[]>([]);
  /** Только пользовательские агенты. Встроенный подмешивается в allAgents. */
  const agents = ref<AgentResponse[]>([]);
  const skills = ref<SkillResponse[]>([]);
  const agentsLoading = ref(false);
  const messagesByChat = ref<Record<string, MessageResponse[]>>({});
  const drafts = ref<Record<string, Draft>>({});

  const activeChatId = ref<string | null>(null);
  const searchQuery = ref('');

  const chatsLoading = ref(false);
  const messagesLoading = ref(false);
  const sending = ref(false);
  const uploadingCount = ref(0);
  const loadError = ref<string | null>(null);

  /** id сообщения, которое сейчас «печатается». */
  const streamingMessageId = ref<string | null>(null);
  let streamController: AbortController | null = null;
  /** Чат, ответ в котором сейчас стримится — нужен, чтобы остановить генерацию на сервере. */
  let streamingChatId: string | null = null;

  // ─────────────────────────────── computed ───────────────────────────────

  const activeChat = computed(() =>
    activeChatId.value ? (chats.value.find((c) => c.id === activeChatId.value) ?? null) : null,
  );

  const activeMessages = computed<MessageResponse[]>(() =>
    activeChatId.value ? (messagesByChat.value[activeChatId.value] ?? []) : [],
  );

  const activeDraft = computed<Draft>(() => {
    const key = activeChatId.value ?? NEW_CHAT;
    return drafts.value[key] ?? emptyDraft();
  });

  const filteredChats = computed(() => {
    const query = searchQuery.value.trim().toLowerCase();
    if (!query) return chats.value;

    return chats.value.filter(
      (chat) =>
        chat.title.toLowerCase().includes(query) ||
        (chat.lastMessagePreview ?? '').toLowerCase().includes(query),
    );
  });

  /** Чаты, разложенные по «Закреплённые / Сегодня / Вчера / …». */
  const groupedChats = computed<ChatGroup[]>(() => {
    const buckets = new Map<ChatBucket, ChatResponse[]>();

    for (const chat of [...filteredChats.value].sort(byUpdatedDesc)) {
      const key: ChatBucket = chat.pinned ? 'pinned' : bucketOf(chat.updatedAt);
      const list = buckets.get(key);
      if (list) list.push(chat);
      else buckets.set(key, [chat]);
    }

    return bucketOrder
      .filter((key) => buckets.has(key))
      .map((key) => ({ key, label: bucketLabels[key], chats: buckets.get(key)! }));
  });

  /** Встроенный агент первым, дальше пользовательские — в таком виде список идёт в UI. */
  const allAgents = computed<AgentResponse[]>(() => [builtInAgent, ...agents.value]);

  const canSend = computed(() => {
    const draft = activeDraft.value;
    const hasContent = draft.text.trim().length > 0 || draft.attachments.length > 0;
    return hasContent && !sending.value && uploadingCount.value === 0 && !streamingMessageId.value;
  });

  /**
   * Агент чата. null у чата означает встроенного агента, а не «никакого» —
   * поэтому здесь возвращается именно он, а не null.
   */
  function agentById(id: string | null | undefined): AgentResponse {
    if (!id || id === BUILT_IN_AGENT_ID) return builtInAgent;
    return agents.value.find((a) => a.id === id) ?? builtInAgent;
  }

  // ─────────────────────────────── загрузка ───────────────────────────────

  async function init(): Promise<void> {
    await Promise.all([loadChats(), loadAgents(), loadSkills()]);

    // Мок-ассистент на бэкенде может переименовать чат по первому сообщению —
    // это приходит пушем по SignalR, а не как ответ на какой-то наш вызов.
    if (!config.useMocks) {
      onChatRenamed((chatId, title) => {
        const chat = chats.value.find((c) => c.id === chatId);
        if (chat) chat.title = title;
      });
    }
  }

  async function loadChats(): Promise<void> {
    chatsLoading.value = true;
    loadError.value = null;

    try {
      chats.value = (await chatsApi.listChats()).sort(byUpdatedDesc);
    } catch (e) {
      chats.value = [];
      loadError.value = describe(e, 'Не удалось загрузить список чатов.');
    } finally {
      chatsLoading.value = false;
    }
  }

  async function loadAgents(): Promise<void> {
    agentsLoading.value = true;
    try {
      agents.value = await chatsApi.listAgents();
    } catch {
      agents.value = [];
    } finally {
      agentsLoading.value = false;
    }
  }

  /** Скиллы меняются только при деплое, поэтому грузим один раз и держим. */
  async function loadSkills(): Promise<void> {
    if (skills.value.length > 0) return;

    try {
      skills.value = await chatsApi.listSkills();
    } catch {
      skills.value = [];
    }
  }

  async function createAgent(form: SaveAgentRequest): Promise<AgentResponse | null> {
    try {
      const agent = await chatsApi.createAgent(form);
      agents.value.push(agent);
      toasts.success(`Агент «${agent.name}» создан.`);
      return agent;
    } catch (e) {
      toasts.error(describe(e, 'Не удалось создать агента.'));
      return null;
    }
  }

  async function updateAgent(agentId: string, form: SaveAgentRequest): Promise<AgentResponse | null> {
    try {
      const agent = await chatsApi.updateAgent(agentId, form);

      const index = agents.value.findIndex((a) => a.id === agentId);
      if (index !== -1) agents.value[index] = agent;

      toasts.success(`Агент «${agent.name}» сохранён.`);
      return agent;
    } catch (e) {
      toasts.error(describe(e, 'Не удалось сохранить агента.'));
      return null;
    }
  }

  async function deleteAgent(agentId: string): Promise<void> {
    const index = agents.value.findIndex((a) => a.id === agentId);
    if (index === -1) return;

    const [removed] = agents.value.splice(index, 1);

    try {
      await chatsApi.deleteAgent(agentId);

      // Чаты удалённого агента возвращаются к встроенному — так же, как на бэкенде.
      for (const chat of chats.value) {
        if (chat.agentId === agentId) chat.agentId = null;
      }

      toasts.success(`Агент «${removed.name}» удалён.`);
    } catch (e) {
      agents.value.splice(index, 0, removed);
      toasts.error(describe(e, 'Не удалось удалить агента.'));
    }
  }

  /** Открыть чат. null — экран нового чата. */
  async function selectChat(chatId: string | null): Promise<void> {
    // Тот же чат уже открыт. Важно выйти раньше stopStreaming(): после отправки
    // первого сообщения роутер переезжает на /chat/{id}, и без этой проверки
    // переход обрывал только что начавшийся ответ.
    if (activeChatId.value === chatId) return;

    // Именно detachStream, а не stopStreaming: уход в другой чат не должен обрывать
    // генерацию на сервере — вернёмся и увидим готовый ответ.
    detachStream();
    activeChatId.value = chatId;

    if (!chatId || messagesByChat.value[chatId]) return;

    messagesLoading.value = true;
    try {
      messagesByChat.value[chatId] = await chatsApi.listMessages(chatId);
    } catch (e) {
      messagesByChat.value[chatId] = [];
      toasts.error(describe(e, 'Не удалось загрузить сообщения.'));
    } finally {
      messagesLoading.value = false;
    }
  }

  // ─────────────────────────── управление чатами ──────────────────────────

  /** agentId не задан — чат достаётся встроенному агенту, это поведение нового чата по умолчанию. */
  async function createChat(title?: string, agentId: string | null = null): Promise<ChatResponse | null> {
    try {
      const chat = await chatsApi.createChat({ title, agentId });
      chats.value.unshift(chat);
      messagesByChat.value[chat.id] = [];
      return chat;
    } catch (e) {
      toasts.error(describe(e, 'Не удалось создать чат.'));
      return null;
    }
  }

  async function renameChat(chatId: string, title: string): Promise<void> {
    const chat = chats.value.find((c) => c.id === chatId);
    if (!chat) return;

    const previous = chat.title;
    chat.title = title; // оптимистично

    try {
      const updated = await chatsApi.updateChat(chatId, { title });
      Object.assign(chat, updated);
    } catch (e) {
      chat.title = previous;
      toasts.error(describe(e, 'Не удалось переименовать чат.'));
    }
  }

  /**
   * Меняет агента чата. Встроенный агент уезжает на бэкенд как null — идентификатора
   * в БД у него нет.
   */
  async function setAgent(chatId: string, agentId: string | null): Promise<void> {
    const next = agentId === BUILT_IN_AGENT_ID ? null : agentId;

    const chat = chats.value.find((c) => c.id === chatId);
    if (!chat || chat.agentId === next) return;

    const previous = chat.agentId;
    chat.agentId = next; // оптимистично

    try {
      await chatsApi.setChatAgent(chatId, next);
    } catch (e) {
      chat.agentId = previous;
      toasts.error(describe(e, 'Не удалось сменить агента.'));
    }
  }

  async function togglePin(chatId: string): Promise<void> {
    const chat = chats.value.find((c) => c.id === chatId);
    if (!chat) return;

    const next = !chat.pinned;
    chat.pinned = next;

    try {
      await chatsApi.updateChat(chatId, { pinned: next });
    } catch (e) {
      chat.pinned = !next;
      toasts.error(describe(e, 'Не удалось изменить закрепление.'));
    }
  }

  async function deleteChat(chatId: string): Promise<void> {
    const index = chats.value.findIndex((c) => c.id === chatId);
    if (index === -1) return;

    const [removed] = chats.value.splice(index, 1);
    const removedMessages = messagesByChat.value[chatId];
    delete messagesByChat.value[chatId];

    try {
      await chatsApi.deleteChat(chatId);
      toasts.success(`Чат «${removed.title}» удалён.`);
    } catch (e) {
      chats.value.splice(index, 0, removed);
      if (removedMessages) messagesByChat.value[chatId] = removedMessages;
      toasts.error(describe(e, 'Не удалось удалить чат.'));
    }
  }

  // ───────────────────────────── черновики ────────────────────────────────

  function draftFor(key: string): Draft {
    return (drafts.value[key] ??= emptyDraft());
  }

  function setDraftText(text: string): void {
    draftFor(activeChatId.value ?? NEW_CHAT).text = text;
  }

  async function attachFiles(files: File[]): Promise<void> {
    const key = activeChatId.value ?? NEW_CHAT;

    await Promise.all(
      files.map(async (file) => {
        uploadingCount.value++;
        try {
          const attachment = await chatsApi.uploadAttachment(file);
          draftFor(key).attachments.push(attachment);
        } catch (e) {
          toasts.error(describe(e, `Не удалось загрузить «${file.name}».`));
        } finally {
          uploadingCount.value--;
        }
      }),
    );
  }

  async function removeAttachment(attachmentId: string): Promise<void> {
    const draft = draftFor(activeChatId.value ?? NEW_CHAT);
    draft.attachments = draft.attachments.filter((a) => a.id !== attachmentId);

    try {
      await chatsApi.deleteAttachment(attachmentId);
    } catch {
      // файл уже убран из черновика — молча игнорируем
    }
  }

  // ────────────────────────── отправка и стриминг ─────────────────────────

  /**
   * Отправляет черновик. Если чат ещё не создан — сначала создаёт его.
   * Возвращает id чата, в который ушло сообщение (нужен роутеру).
   */
  async function sendDraft(): Promise<string | null> {
    if (!canSend.value) return null;

    const sourceKey = activeChatId.value ?? NEW_CHAT;
    const draft = draftFor(sourceKey);
    const content = draft.text.trim();
    const attachmentIds = draft.attachments.map((a) => a.id);

    sending.value = true;

    try {
      let chatId = activeChatId.value;

      if (!chatId) {
        // Первое сообщение задаёт название чата.
        const chat = await createChat(titleFromContent(content));
        if (!chat) return null;
        chatId = chat.id;
        activeChatId.value = chatId;
      }

      // Черновик очищаем сразу — так поле ввода отзывчивее.
      drafts.value[sourceKey] = emptyDraft();

      const { userMessage, assistantMessage } = await chatsApi.sendMessage(chatId, {
        content,
        attachmentIds,
      });

      const list = (messagesByChat.value[chatId] ??= []);
      list.push(userMessage, assistantMessage);
      touchChat(chatId, userMessage.content);

      void streamReply(chatId, assistantMessage.id);

      return chatId;
    } catch (e) {
      // Возвращаем черновик, чтобы пользователь не потерял текст.
      drafts.value[sourceKey] = { text: content, attachments: draft.attachments };
      toasts.error(describe(e, 'Не удалось отправить сообщение.'));
      return null;
    } finally {
      sending.value = false;
    }
  }

  async function streamReply(chatId: string, messageId: string): Promise<void> {
    streamingMessageId.value = messageId;
    streamingChatId = chatId;
    streamController = new AbortController();

    const list = messagesByChat.value[chatId];
    const message = list?.find((m) => m.id === messageId);
    if (!message) return;

    try {
      const final = await chatsApi.streamAssistantMessage(
        chatId,
        messageId,
        {
          onDelta: (delta) => {
            message.content += delta;
          },

          onToolCallStarted: (toolCall) => {
            (message.toolCalls ??= []).push({ ...toolCall, status: 'running' });
          },

          onToolCallCompleted: (toolCallId, error) => {
            const call = message.toolCalls?.find((c) => c.id === toolCallId);
            if (!call) return;

            call.status = error ? 'failed' : 'done';
            call.error = error;
          },
        },
        streamController.signal,
      );

      message.content = final.content;
      message.status = 'complete';
      // Финальное сообщение приходит из БД, где вызовов инструментов нет, — сохраняем те,
      // что накопили по ходу стрима, иначе они пропадут ровно в момент завершения ответа.
      finishPendingToolCalls(message);
      touchChat(chatId, final.content);
    } catch (e) {
      finishPendingToolCalls(message);

      if (e instanceof DOMException && e.name === 'AbortError') {
        message.status = 'complete';
      } else {
        message.status = 'failed';
        message.error = describe(e, 'Ответ не получен.');
        toasts.error(message.error);
      }
    } finally {
      if (streamingMessageId.value === messageId) {
        streamingMessageId.value = null;
        streamController = null;
        streamingChatId = null;
      }
    }
  }

  /** Перестать слушать стрим на клиенте. Сервер при этом спокойно догенерирует ответ. */
  function detachStream(): void {
    streamController?.abort();
    streamController = null;
    streamingMessageId.value = null;
    streamingChatId = null;
  }

  /**
   * Прервать генерацию по кнопке «стоп»: отцепляемся сами и просим сервер тоже остановиться.
   * Без запроса на сервер модель продолжила бы считать, а чат оставался бы занятым
   * и не принимал новые сообщения.
   */
  async function stopStreaming(): Promise<void> {
    const chatId = streamingChatId;

    detachStream();

    if (!chatId) return;

    try {
      await chatsApi.stopGeneration(chatId);
    } catch (e) {
      toasts.error(describe(e, 'Не удалось остановить генерацию.'));
    }
  }

  function touchChat(chatId: string, lastContent: string): void {
    const chat = chats.value.find((c) => c.id === chatId);
    if (!chat) return;

    chat.updatedAt = new Date().toISOString();
    chat.lastMessagePreview = lastContent.replace(/\s+/g, ' ').trim().slice(0, 90);
  }

  return {
    // state
    chats,
    agents,
    skills,
    agentsLoading,
    messagesByChat,
    drafts,
    activeChatId,
    searchQuery,
    chatsLoading,
    messagesLoading,
    sending,
    uploadingCount,
    loadError,
    streamingMessageId,

    // computed
    activeChat,
    allAgents,
    activeMessages,
    activeDraft,
    groupedChats,
    filteredChats,
    canSend,

    // actions
    init,
    loadChats,
    loadAgents,
    loadSkills,
    createAgent,
    updateAgent,
    deleteAgent,
    selectChat,
    createChat,
    renameChat,
    setAgent,
    togglePin,
    deleteChat,
    setDraftText,
    attachFiles,
    removeAttachment,
    sendDraft,
    stopStreaming,
    agentById,
  };
});

/**
 * Снимает статус «выполняется» с вызовов, для которых так и не пришло завершение:
 * генерацию оборвали или соединение отвалилось. Иначе спиннер крутился бы вечно.
 */
function finishPendingToolCalls(message: MessageResponse): void {
  for (const call of message.toolCalls ?? []) {
    if (call.status === 'running') call.status = 'done';
  }
}

function byUpdatedDesc(a: ChatResponse, b: ChatResponse): number {
  return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
}

function describe(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.isNotImplemented
      ? `${fallback} Эндпоинт ещё не реализован на бэкенде.`
      : `${fallback} ${error.message}`;
  }
  if (error instanceof Error) return `${fallback} ${error.message}`;
  return fallback;
}
