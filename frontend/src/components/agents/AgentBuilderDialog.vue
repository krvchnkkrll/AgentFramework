<script setup lang="ts">
/**
 * Конструктор агента: название, описание, промпт в Markdown, набор скиллов и параметры генерации.
 *
 * Форма всегда отправляется целиком — частичных обновлений на бэкенде нет, пользователь
 * сохраняет то, что видит. Поэтому при открытии локальная копия формы собирается заново
 * из агента (или из пустого шаблона), а стор трогается только при сохранении.
 */
import { computed, nextTick, ref, watch } from 'vue';

import type { AgentResponse, ReasoningEffort, SaveAgentRequest } from '@/api';
import { agentToForm, emptyAgentForm } from '@/api/builtInAgent';
import AppButton from '@/components/ui/AppButton.vue';
import AppIcon from '@/components/ui/AppIcon.vue';
import { useChatsStore } from '@/stores/chats';
import { renderMarkdown } from '@/utils/markdown';

const props = defineProps<{
  open: boolean;
  /** Агент для правки. null — создаём нового. */
  agent: AgentResponse | null;
}>();

const emit = defineEmits<{ close: []; saved: [agent: AgentResponse] }>();

const chats = useChatsStore();

const form = ref<SaveAgentRequest>(emptyAgentForm());
const saving = ref(false);
const showPreview = ref(false);
const nameInput = ref<HTMLInputElement | null>(null);

const isEditing = computed(() => props.agent !== null);
const canSave = computed(() => form.value.name.trim().length > 0 && !saving.value);

const reasoningOptions: { value: ReasoningEffort; label: string; hint: string }[] = [
  { value: 'None', label: 'Выключены', hint: 'Быстрее всего — модель сразу отвечает' },
  { value: 'Low', label: 'Низкие', hint: 'Немного подумает перед ответом' },
  { value: 'Medium', label: 'Средние', hint: 'Заметно медленнее, точнее на сложном' },
  { value: 'High', label: 'Высокие', hint: 'Долго думает, для сложных задач' },
  { value: 'Default', label: 'Как решит сервер', hint: 'Параметр не отправляется' },
];

// Форма пересобирается на каждое открытие: иначе после «Отмена» в диалоге остались бы
// правки от прошлого раза.
watch(
  () => [props.open, props.agent] as const,
  async ([open, agent]) => {
    if (!open) return;

    form.value = agent ? agentToForm(agent) : emptyAgentForm();
    showPreview.value = false;

    await nextTick();
    nameInput.value?.focus();
  },
  { immediate: true },
);

const instructionsHtml = computed(() => renderMarkdown(form.value.instructions ?? ''));

function toggleSkill(name: string): void {
  const skills = form.value.skills;
  const index = skills.indexOf(name);

  if (index === -1) skills.push(name);
  else skills.splice(index, 1);
}

/** Пустые строки уезжают на бэкенд как null — там это «не задано», а не «пустая строка». */
function blankToNull(value: string | null): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

async function save(): Promise<void> {
  if (!canSave.value) return;

  saving.value = true;

  // skills копируем, а не передаём как есть: form — реактивный ref, и наружу ушёл бы
  // Proxy. По сети это незаметно (JSON.stringify его разворачивает), а вот мок-слой
  // с structuredClone на таком массиве падает.
  const payload: SaveAgentRequest = {
    ...form.value,
    name: form.value.name.trim(),
    description: blankToNull(form.value.description),
    icon: blankToNull(form.value.icon),
    instructions: blankToNull(form.value.instructions),
    skills: [...form.value.skills],
  };

  const saved = props.agent
    ? await chats.updateAgent(props.agent.id, payload)
    : await chats.createAgent(payload);

  saving.value = false;

  if (saved) {
    emit('saved', saved);
    emit('close');
  }
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') emit('close');
}
</script>

<template>
  <Teleport to="body">
    <Transition name="builder">
      <div
        v-if="open"
        class="builder"
        role="dialog"
        aria-modal="true"
        aria-label="Конструктор агента"
        tabindex="-1"
        @keydown="onKeydown"
        @click.self="emit('close')"
      >
        <div class="builder__card">
          <header class="builder__head">
            <h2 class="builder__title">
              {{ isEditing ? 'Настройка агента' : 'Новый агент' }}
            </h2>
            <button class="builder__close" aria-label="Закрыть" @click="emit('close')">
              <AppIcon name="x" :size="17" />
            </button>
          </header>

          <div class="builder__body">
            <!-- ─────────── Кто это ─────────── -->
            <section class="section">
              <h3 class="section__title">Кто это</h3>

              <div class="row">
                <label class="field field--icon">
                  <span class="field__label">Значок</span>
                  <input
                    v-model="form.icon"
                    class="field__input field__input--center"
                    maxlength="8"
                    placeholder="✦"
                  />
                </label>

                <label class="field field--grow">
                  <span class="field__label">Название</span>
                  <input
                    ref="nameInput"
                    v-model="form.name"
                    class="field__input"
                    maxlength="60"
                    placeholder="Код-ревьюер"
                  />
                </label>
              </div>

              <label class="field">
                <span class="field__label">Описание</span>
                <input
                  v-model="form.description"
                  class="field__input"
                  maxlength="300"
                  placeholder="Коротко: чем занимается этот агент"
                />
              </label>
            </section>

            <!-- ─────────── Промпт ─────────── -->
            <section class="section">
              <div class="section__head">
                <h3 class="section__title">Системный промпт</h3>
                <button class="toggle" type="button" @click="showPreview = !showPreview">
                  <AppIcon :name="showPreview ? 'pencil' : 'file'" :size="13" />
                  {{ showPreview ? 'Редактировать' : 'Предпросмотр' }}
                </button>
              </div>

              <p class="section__hint">
                Markdown. Уезжает в модель при каждом запросе, поэтому здесь описывают то,
                что верно всегда: роль, тон, формат ответа, запреты. Узкие инструкции лучше
                оформлять скиллами — они подгружаются только когда нужны.
              </p>

              <div
                v-if="showPreview"
                class="markdown-body preview"
                v-html="instructionsHtml || '<p class=\'preview__empty\'>Промпт пустой — агент будет работать на встроенном.</p>'"
              />
              <textarea
                v-else
                v-model="form.instructions"
                class="field__input field__input--area"
                rows="10"
                placeholder="# Роль&#10;&#10;Ты ревьюишь код на C#..."
              />
            </section>

            <!-- ─────────── Скиллы ─────────── -->
            <section class="section">
              <h3 class="section__title">Скиллы</h3>
              <p class="section__hint">
                Агент видит только имя и описание скилла и подгружает его целиком, когда решит,
                что тот нужен. Ничего не выбрано — агент работает без скиллов.
              </p>

              <p v-if="chats.skills.length === 0" class="empty">
                Скиллов не найдено. Их кладут папками с файлом SKILL.md туда, куда смотрит
                бэкенд (по умолчанию — Web/skills).
              </p>

              <ul v-else class="skills">
                <li v-for="skill in chats.skills" :key="skill.name">
                  <button
                    class="skill"
                    type="button"
                    :class="{ 'skill--on': form.skills.includes(skill.name) }"
                    @click="toggleSkill(skill.name)"
                  >
                    <span class="skill__check">
                      <AppIcon v-if="form.skills.includes(skill.name)" name="check" :size="12" />
                    </span>
                    <span class="skill__text">
                      <code class="skill__name">{{ skill.name }}</code>
                      <span class="skill__desc">{{ skill.description }}</span>
                    </span>
                  </button>
                </li>
              </ul>
            </section>

            <!-- ─────────── Параметры ─────────── -->
            <section class="section">
              <h3 class="section__title">Параметры генерации</h3>

              <div class="params">
                <label class="param">
                  <span class="param__head">
                    <span class="param__label">Температура</span>
                    <span class="param__value">{{ form.temperature.toFixed(2) }}</span>
                  </span>
                  <input v-model.number="form.temperature" type="range" min="0" max="2" step="0.05" />
                  <span class="param__hint">Ниже — предсказуемее, выше — разнообразнее</span>
                </label>

                <label class="param">
                  <span class="param__head">
                    <span class="param__label">Top P</span>
                    <span class="param__value">{{ form.topP.toFixed(2) }}</span>
                  </span>
                  <input v-model.number="form.topP" type="range" min="0" max="1" step="0.05" />
                  <span class="param__hint">Доля вероятной массы, из которой выбираются слова</span>
                </label>

                <label class="param">
                  <span class="param__head">
                    <span class="param__label">Top K</span>
                    <span class="param__value">{{ form.topK }}</span>
                  </span>
                  <input v-model.number="form.topK" type="range" min="0" max="200" step="1" />
                  <span class="param__hint">Сколько кандидатов рассматривать на каждом шаге</span>
                </label>

                <label class="param">
                  <span class="param__head">
                    <span class="param__label">Штраф за повторы</span>
                    <span class="param__value">{{ form.frequencyPenalty.toFixed(1) }}</span>
                  </span>
                  <input
                    v-model.number="form.frequencyPenalty"
                    type="range"
                    min="-2"
                    max="2"
                    step="0.1"
                  />
                  <span class="param__hint">Выше — реже повторяет одни и те же слова</span>
                </label>

                <label class="param">
                  <span class="param__head">
                    <span class="param__label">Штраф за темы</span>
                    <span class="param__value">{{ form.presencePenalty.toFixed(1) }}</span>
                  </span>
                  <input
                    v-model.number="form.presencePenalty"
                    type="range"
                    min="-2"
                    max="2"
                    step="0.1"
                  />
                  <span class="param__hint">Выше — охотнее уходит в новые темы</span>
                </label>

                <label class="param">
                  <span class="param__head">
                    <span class="param__label">Максимум токенов ответа</span>
                  </span>
                  <input
                    v-model.number="form.maxOutputTokens"
                    class="field__input"
                    type="number"
                    min="64"
                    max="200000"
                    step="256"
                  />
                  <span class="param__hint">Потолок длины ответа, не цель</span>
                </label>
              </div>

              <label class="field">
                <span class="field__label">Рассуждения</span>
                <select v-model="form.reasoningEffortEnum" class="field__input">
                  <option v-for="option in reasoningOptions" :key="option.value" :value="option.value">
                    {{ option.label }} — {{ option.hint }}
                  </option>
                </select>
                <span class="param__hint">
                  Понимает не всякая модель. Если параметр проигнорируют, модель продолжит
                  думать столько, сколько сочтёт нужным.
                </span>
              </label>
            </section>
          </div>

          <footer class="builder__foot">
            <AppButton variant="subtle" @click="emit('close')">Отмена</AppButton>
            <AppButton variant="primary" :disabled="!canSave" :loading="saving" @click="save">
              {{ isEditing ? 'Сохранить' : 'Создать агента' }}
            </AppButton>
          </footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.builder {
  position: fixed;
  inset: 0;
  z-index: 60;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: rgb(0 0 0 / 55%);
  backdrop-filter: blur(3px);
}

.builder__card {
  display: flex;
  flex-direction: column;
  width: min(760px, 100%);
  max-height: 100%;
  background: var(--bg-elevated);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  overflow: hidden;
}

.builder__head {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px 18px;
  border-bottom: 1px solid var(--border);
}

.builder__title {
  flex: 1;
  margin: 0;
  font-size: var(--text-lg);
  font-weight: 600;
}

.builder__close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  border: 0;
  border-radius: var(--radius-sm);
  background: none;
  color: var(--text-secondary);
  cursor: pointer;
}

.builder__close:hover {
  background: var(--bg-surface-2);
  color: var(--text-primary);
}

.builder__body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 4px 18px 18px;
}

.builder__foot {
  display: flex;
  justify-content: flex-end;
  gap: 9px;
  padding: 14px 18px;
  border-top: 1px solid var(--border);
  background: var(--bg-surface);
}

.section {
  padding: 16px 0;
  border-bottom: 1px solid var(--border);
}

.section:last-child {
  border-bottom: 0;
}

.section__head {
  display: flex;
  align-items: center;
  gap: 10px;
}

.section__title {
  flex: 1;
  margin: 0 0 4px;
  font-size: var(--text-sm);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--text-secondary);
}

.section__hint {
  margin: 0 0 10px;
  font-size: var(--text-sm);
  color: var(--text-muted);
}

.toggle {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 4px 8px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: none;
  color: var(--text-secondary);
  font-size: var(--text-xs);
  cursor: pointer;
}

.toggle:hover {
  color: var(--text-primary);
  border-color: var(--border-strong);
}

.row {
  display: flex;
  gap: 10px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 5px;
  margin-bottom: 10px;
}

.field--grow {
  flex: 1;
  min-width: 0;
}

.field--icon {
  width: 74px;
  flex-shrink: 0;
}

.field__label {
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.field__input {
  width: 100%;
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-surface);
  color: var(--text-primary);
  font: inherit;
  font-size: var(--text-sm);
}

.field__input:focus {
  outline: none;
  border-color: var(--accent);
}

.field__input--center {
  text-align: center;
  font-size: var(--text-lg);
}

.field__input--area {
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: var(--text-xs);
  line-height: 1.55;
  resize: vertical;
}

.preview {
  min-height: 180px;
  padding: 10px 12px;
  border: 1px dashed var(--border-strong);
  border-radius: var(--radius-sm);
  background: var(--bg-surface);
}

.empty {
  margin: 0;
  padding: 10px 12px;
  border: 1px dashed var(--border-strong);
  border-radius: var(--radius-sm);
  font-size: var(--text-sm);
  color: var(--text-muted);
}

.skills {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.skill {
  display: flex;
  gap: 9px;
  width: 100%;
  padding: 8px 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--bg-surface);
  text-align: left;
  cursor: pointer;
}

.skill:hover {
  border-color: var(--border-strong);
}

.skill--on {
  border-color: var(--accent);
  background: var(--accent-soft);
}

.skill__check {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  margin-top: 1px;
  flex-shrink: 0;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-xs);
  color: var(--accent);
}

.skill--on .skill__check {
  border-color: var(--accent);
}

.skill__text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.skill__name {
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: var(--text-xs);
  color: var(--text-primary);
}

.skill__desc {
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.params {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 14px;
  margin-bottom: 12px;
}

.param {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.param__head {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.param__label {
  flex: 1;
  font-size: var(--text-sm);
  color: var(--text-primary);
}

.param__value {
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: var(--text-xs);
  color: var(--accent);
}

.param__hint {
  font-size: var(--text-xs);
  color: var(--text-muted);
}

.param input[type='range'] {
  width: 100%;
  accent-color: var(--accent);
}

.builder-enter-active,
.builder-leave-active {
  transition: opacity var(--dur-fast) var(--ease-out);
}

.builder-enter-from,
.builder-leave-to {
  opacity: 0;
}

@media (max-width: 620px) {
  .builder {
    padding: 0;
  }

  .builder__card {
    max-height: 100%;
    height: 100%;
    border-radius: 0;
    border: 0;
  }
}
</style>
