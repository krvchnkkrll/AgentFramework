/** Форматирование дат, размеров файлов и группировка чатов по времени. */

const timeFormatter = new Intl.DateTimeFormat('ru-RU', {
  hour: '2-digit',
  minute: '2-digit',
});

const dateFormatter = new Intl.DateTimeFormat('ru-RU', {
  day: 'numeric',
  month: 'long',
});

const fullFormatter = new Intl.DateTimeFormat('ru-RU', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

const DAY_MS = 86_400_000;

/** «14:03» для сегодняшних, «5 мая» для остальных. */
export function formatTimestamp(iso: string): string {
  const date = new Date(iso);
  return isToday(date) ? timeFormatter.format(date) : dateFormatter.format(date);
}

export function formatFullTimestamp(iso: string): string {
  return fullFormatter.format(new Date(iso));
}

/** «только что», «12 мин назад», «вчера», «5 мая». */
export function formatRelative(iso: string): string {
  const date = new Date(iso);
  const diffMs = Date.now() - date.getTime();

  if (diffMs < 60_000) return 'только что';

  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 60) return `${minutes} ${plural(minutes, 'мин', 'мин', 'мин')} назад`;

  const hours = Math.floor(minutes / 60);
  if (isToday(date)) return `${hours} ${plural(hours, 'час', 'часа', 'часов')} назад`;

  if (isYesterday(date)) return 'вчера';

  const dayDiff = Math.floor(diffMs / DAY_MS);
  if (dayDiff < 7) return `${dayDiff} ${plural(dayDiff, 'день', 'дня', 'дней')} назад`;

  return dateFormatter.format(date);
}

export type ChatBucket = 'pinned' | 'today' | 'yesterday' | 'week' | 'month' | 'older';

export const bucketLabels: Record<ChatBucket, string> = {
  pinned: 'Закреплённые',
  today: 'Сегодня',
  yesterday: 'Вчера',
  week: 'Последние 7 дней',
  month: 'Последние 30 дней',
  older: 'Раньше',
};

export const bucketOrder: ChatBucket[] = ['pinned', 'today', 'yesterday', 'week', 'month', 'older'];

export function bucketOf(iso: string): Exclude<ChatBucket, 'pinned'> {
  const date = new Date(iso);
  if (isToday(date)) return 'today';
  if (isYesterday(date)) return 'yesterday';

  const dayDiff = Math.floor((Date.now() - date.getTime()) / DAY_MS);
  if (dayDiff < 7) return 'week';
  if (dayDiff < 30) return 'month';
  return 'older';
}

/** Заголовок чата из первого сообщения — как в OpenWebUI. */
export function titleFromContent(content: string): string {
  const clean = content.replace(/\s+/g, ' ').trim();
  if (!clean) return 'Новый чат';
  return clean.length <= 42 ? clean : `${clean.slice(0, 42).trimEnd()}…`;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} Б`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} КБ`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} МБ`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(1)} ГБ`;
}

export function isImage(contentType: string): boolean {
  return contentType.startsWith('image/');
}

/** Расширение файла заглавными буквами — для иконки-заглушки. */
export function fileExtension(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  if (dot === -1 || dot === fileName.length - 1) return 'FILE';
  return fileName.slice(dot + 1).toUpperCase().slice(0, 4);
}

/** Инициалы для аватарки: «Иван Петров» → «ИП», «dev.user» → «DU». */
export function initials(name: string): string {
  const parts = name.split(/[\s._-]+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[1][0]).toUpperCase();
}

function isToday(date: Date): boolean {
  const now = new Date();
  return (
    date.getDate() === now.getDate() &&
    date.getMonth() === now.getMonth() &&
    date.getFullYear() === now.getFullYear()
  );
}

function isYesterday(date: Date): boolean {
  const yesterday = new Date(Date.now() - DAY_MS);
  return (
    date.getDate() === yesterday.getDate() &&
    date.getMonth() === yesterday.getMonth() &&
    date.getFullYear() === yesterday.getFullYear()
  );
}

function plural(n: number, one: string, few: string, many: string): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return one;
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return few;
  return many;
}
