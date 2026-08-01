/**
 * Markdown → безопасный HTML.
 *
 * Ответ модели — это по сути недоверенный ввод, поэтому результат обязательно
 * прогоняется через DOMPurify. Никогда не рендерь content напрямую через v-html.
 */
import DOMPurify from 'dompurify';
import { marked } from 'marked';

marked.setOptions({
  gfm: true,
  breaks: true,
});

// Внешние ссылки открываем в новой вкладке и обрубаем доступ к window.opener.
DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A' && node.hasAttribute('href')) {
    node.setAttribute('target', '_blank');
    node.setAttribute('rel', 'noopener noreferrer nofollow');
  }
});

export function renderMarkdown(source: string): string {
  const html = marked.parse(source, { async: false });

  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'p', 'br', 'hr', 'strong', 'em', 'del', 'code', 'pre', 'blockquote',
      'ul', 'ol', 'li', 'a', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
      'table', 'thead', 'tbody', 'tr', 'th', 'td', 'span',
    ],
    ALLOWED_ATTR: ['href', 'title', 'class', 'target', 'rel'],
    ALLOWED_URI_REGEXP: /^(?:https?|mailto):/i,
  });
}

/** Достаёт исходный текст всех блоков кода — для кнопки «копировать». */
export function extractCodeBlocks(source: string): string[] {
  return [...source.matchAll(/```[^\n]*\n([\s\S]*?)```/g)].map((match) => match[1]);
}
