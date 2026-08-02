import type { AgentResponse, SaveAgentRequest } from './types';

/**
 * Встроенный агент. На бэкенде его нет ни в БД, ни в /api/agents — он собирается из секции
 * Assistant в appsettings и отвечает в любом чате, у которого agentId равен null.
 *
 * На клиенте нужен псевдозаписью, чтобы в списке агентов и в шапке чата было что показать.
 * Отличается флагом builtIn: его нельзя ни редактировать, ни удалить.
 */
export const BUILT_IN_AGENT_ID = 'built-in';

export const builtInAgent: AgentResponse = {
  id: BUILT_IN_AGENT_ID,
  name: 'Универсальный',
  description: 'Встроенный агент. Настраивается в конфигурации приложения, а не здесь.',
  icon: '✦',
  instructions: null,
  skills: [],
  temperature: 0.7,
  topP: 0.95,
  topK: 40,
  maxOutputTokens: 8192,
  frequencyPenalty: 0,
  presencePenalty: 0,
  reasoningEffortEnum: 'None',
  createdAt: new Date(0).toISOString(),
  updatedAt: new Date(0).toISOString(),
  builtIn: true,
};

/** Пустая форма конструктора — с ней открывается создание нового агента. */
export function emptyAgentForm(): SaveAgentRequest {
  return {
    name: '',
    description: null,
    icon: null,
    instructions: null,
    skills: [],
    temperature: 0.7,
    topP: 0.95,
    topK: 40,
    maxOutputTokens: 8192,
    frequencyPenalty: 0,
    presencePenalty: 0,
    reasoningEffortEnum: 'None',
  };
}

/** Готовит форму для редактирования существующего агента. */
export function agentToForm(agent: AgentResponse): SaveAgentRequest {
  return {
    name: agent.name,
    description: agent.description,
    icon: agent.icon,
    instructions: agent.instructions,
    skills: [...agent.skills],
    temperature: agent.temperature,
    topP: agent.topP,
    topK: agent.topK,
    maxOutputTokens: agent.maxOutputTokens,
    frequencyPenalty: agent.frequencyPenalty,
    presencePenalty: agent.presencePenalty,
    reasoningEffortEnum: agent.reasoningEffortEnum,
  };
}
