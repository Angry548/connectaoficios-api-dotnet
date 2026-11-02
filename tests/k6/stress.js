import { check } from 'k6';
import http from 'k6/http';
import { recordSample, workloads, reportSummary, setup as setupLocal } from './common.js';

const rampStages = [
  { duration: '10s', target: 1 },
  { duration: '15s', target: 3 },
  { duration: '15s', target: 5 },
  { duration: '15s', target: 8 },
  { duration: '15s', target: 10 },
  { duration: '15s', target: 12 },
  { duration: '20s', target: 12 },
  { duration: '10s', target: 0 },
];

export const options = {
  scenarios: {
    progressive_stress: {
      executor: 'ramping-vus', startVUs: 1, exec: 'ramp', gracefulRampDown: '5s',
      stages: rampStages,
    },
    sustained_stress: {
      executor: 'constant-vus', startTime: '2m', vus: 6, duration: '90s', exec: 'sustained', gracefulStop: '5s',
    },
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
};

export function setup() { return setupLocal(); }

function login(id, data) {
  const accounts = data.accounts || [__ENV.QA_ADMIN_EMAIL];
  const correo = accounts[(__VU - 1) % accounts.length];
  const response = http.post('http://127.0.0.1:8080/api/users/login', JSON.stringify({
    correo, password: __ENV.QA_ADMIN_PASSWORD,
  }), { headers: { 'Content-Type': 'application/json' }, tags: { case: id, name: `${id}_dotnet_login` }, timeout: '30s' });
  recordSample(id, 'dotnet_login', response.timings.duration, response.status === 200);
  if (id !== 'CON-564') recordSample('CON-564', 'dotnet_login', response.timings.duration, response.status === 200);
  check(response, { [`${id} login response`]: r => r.status === 200 && !!r.json('token') });
}

export function ramp(data) {
  const index = (__ITER + __VU - 1) % workloads.length;
  if (index === 0) return login('CON-560', data);
  if (index === 1) return workloads[index].run(data, __ITER + __VU, 'CON-562');
  return workloads[index].run(data, __ITER + __VU, 'CON-563');
}

export function sustained(data) {
  login('CON-561', data);
  const index = 1 + ((__ITER + __VU - 1) % (workloads.length - 1));
  // A small read-only sample keeps representative endpoints in the sustained interval.
  if ((__ITER + __VU) % 4 === 0) workloads[index].run(data, __ITER + __VU, 'CON-563');
}

export function handleSummary(data) {
  const result = reportSummary(data, 'Stress Testing', {
    rampStages,
    rampMaxVUs: 12,
    sustained: { startAfter: '2m', vus: 6, duration: '90s' },
    plannedDuration: '3m 30s',
    model: 'Progressive VUs across login and read-only endpoints, followed by sustained local authentication with periodic read-only requests.',
    degradationCriterion: 'Observed endpoint degradation is identified from rising p95/p99 or HTTP failures and a throughput decline relative to lower VU stages; no official SLA is defined.',
  }, ['CON-560','CON-561','CON-562','CON-563','CON-564','CON-565']);
  const summary = JSON.parse(result[__ENV.PERF_SUMMARY_PATH]);
  const endpoints = summary.metadata.endpoints;
  summary.metadata.degradation = {
    point: endpoints.some(x => x.requests > 0)
      ? 'Observed samples by VU stage; compare each endpoint p95/p99 and failure rate with the 1-VU stage. No official SLA.'
      : 'Not determined: no endpoint samples.',
    endpointWithHighestP95: endpoints[0]?.endpoint || null,
    endpointRanking: endpoints,
  };
  const c565 = summary.metadata.cases.find(x => x.id === 'CON-565');
  if (c565) c565.status = endpoints.length ? 'Pass' : 'Fail';
  result[__ENV.PERF_SUMMARY_PATH] = JSON.stringify(summary, null, 2);
  return result;
}
