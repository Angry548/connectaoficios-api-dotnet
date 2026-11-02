import { mixedIteration, reportSummary, setup as setupLocal } from './common.js';

const loadStages = [
  { duration: '10s', target: 3 },
  { duration: '20s', target: 3 },
  { duration: '10s', target: 5 },
  { duration: '30s', target: 5 },
  { duration: '10s', target: 0 },
];

export const options = {
  scenarios: {
    load_angel: {
      executor: 'ramping-vus', startVUs: 1, exec: 'workload', gracefulRampDown: '5s',
      stages: loadStages,
    },
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
};

export function setup() {
  return setupLocal();
}

export function workload(data) { mixedIteration(data); }

export function handleSummary(data) {
  return reportSummary(data, 'Load Testing', {
    maxVUs: 5, stages: loadStages,
    duration: '80s planned; report records actual duration',
    model: 'One shared local workload rotates across all seven cases; one active VU pool.',
  }, ['CON-553','CON-554','CON-555','CON-556','CON-557','CON-558','CON-559']);
}
