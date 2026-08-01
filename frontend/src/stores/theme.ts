/**
 * Тема оформления.
 *
 * Чтобы добавить новую: допиши блок [data-theme='...'] в styles/tokens.css
 * и добавь запись в массив themes ниже. Больше нигде править не нужно.
 */
import { defineStore } from 'pinia';
import { computed, ref, watch } from 'vue';

export interface ThemeDefinition {
  id: string;
  name: string;
  /** Три цвета для превью-кружка в переключателе. */
  swatch: [string, string, string];
  dark: boolean;
}

export const themes: ThemeDefinition[] = [
  { id: 'midnight', name: 'Midnight', swatch: ['#0b0d13', '#7c8cff', '#c96bff'], dark: true },
  { id: 'daylight', name: 'Daylight', swatch: ['#f6f7fb', '#4f5dff', '#a855f7'], dark: false },
  { id: 'nebula', name: 'Nebula', swatch: ['#100a18', '#d472ff', '#ff6bc4'], dark: true },
  { id: 'forest', name: 'Forest', swatch: ['#08120f', '#35d99b', '#7fe0a0'], dark: true },
  { id: 'sand', name: 'Sand', swatch: ['#faf6f0', '#c2622a', '#d97534'], dark: false },
];

const STORAGE_KEY = 'af.theme';
const DEFAULT_THEME = 'midnight';

export const useThemeStore = defineStore('theme', () => {
  const themeId = ref(readStoredTheme());

  const current = computed(
    () => themes.find((t) => t.id === themeId.value) ?? themes[0],
  );

  watch(
    themeId,
    (id) => {
      document.documentElement.setAttribute('data-theme', id);
      try {
        localStorage.setItem(STORAGE_KEY, id);
      } catch {
        // приватный режим — переживём
      }
    },
    { immediate: true },
  );

  function setTheme(id: string): void {
    if (themes.some((t) => t.id === id)) themeId.value = id;
  }

  /** Быстрое переключение светлая ↔ тёмная по первой подходящей теме. */
  function toggleLightDark(): void {
    const wantDark = !current.value.dark;
    const next = themes.find((t) => t.dark === wantDark);
    if (next) themeId.value = next.id;
  }

  return { themeId, themes, current, setTheme, toggleLightDark };
});

function readStoredTheme(): string {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored && themes.some((t) => t.id === stored)) return stored;
  } catch {
    // localStorage недоступен
  }

  const prefersLight = window.matchMedia?.('(prefers-color-scheme: light)').matches;
  return prefersLight ? 'daylight' : DEFAULT_THEME;
}
