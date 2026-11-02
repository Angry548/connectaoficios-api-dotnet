import http from 'k6/http';
import { check, fail } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

export const DOTNET = 'http://127.0.0.1:8080';
export const JAVA = 'http://127.0.0.1:8081';
if (__ENV.BASE_URL && ![DOTNET, JAVA].includes(__ENV.BASE_URL)) {
  throw new Error('Performance tests accept only the authorized local API URLs.');
}
const CASE_IDS = ['CON-553','CON-554','CON-555','CON-556','CON-557','CON-558','CON-559','CON-560','CON-561','CON-562','CON-563','CON-564'];
const ENDPOINTS = ['dotnet_login','dotnet_users_list','dotnet_users_filters','java_services_search','java_promotions_filters','java_admin_reports','dotnet_user_statistics'];
const suffix = id => id.replace('CON-', '');
const caseMetrics = Object.fromEntries(CASE_IDS.map(id => [id, {
  duration: new Trend(`case_${suffix(id)}_duration`, true),
  requests: new Counter(`case_${suffix(id)}_requests`),
  successes: new Rate(`case_${suffix(id)}_success`),
}]));
const endpointMetrics = Object.fromEntries(ENDPOINTS.map(name => [name, {
  duration: new Trend(`endpoint_${name}_duration`, true),
  requests: new Counter(`endpoint_${name}_requests`),
  successes: new Rate(`endpoint_${name}_success`),
}]));
const json = { headers: { 'Content-Type': 'application/json' }, timeout: '30s' };

export function recordSample(id, name, duration, ok) {
  const cm = caseMetrics[id], em = endpointMetrics[name];
  if (!cm || !em) fail('Unknown performance case or endpoint metric.');
  cm.duration.add(duration, { case: id, endpoint: name });
  cm.requests.add(1, { case: id, endpoint: name });
  cm.successes.add(ok, { case: id, endpoint: name });
  em.duration.add(duration, { endpoint: name });
  em.requests.add(1, { endpoint: name });
  em.successes.add(ok, { endpoint: name });
}

function localCheck(response, expected, name) {
  check(response, { [`${name} HTTP ${expected}`]: r => r.status === expected });
  return response.status === expected;
}

export function setup() {
  const account = __ENV.QA_ADMIN_EMAIL;
  const password = __ENV.QA_ADMIN_PASSWORD;
  if (!account || !password) fail('Provide local QA credentials through the PowerShell runner.');
  const apiHealth = http.get(`${DOTNET}/health`, { tags: { name: 'preflight_dotnet_health' }, timeout: '5s', responseType: 'none' });
  const javaHealth = http.get(`${JAVA}/api/health`, { tags: { name: 'preflight_java_health' }, timeout: '5s', responseType: 'none' });
  if (!localCheck(apiHealth, 200, 'Local .NET health') || !localCheck(javaHealth, 200, 'Local Java health')) {
    fail('Both authorized local APIs must be healthy before load starts.');
  }
  const response = http.post(`${DOTNET}/api/users/login`, JSON.stringify({ correo: account, password }), {
    ...json, tags: { case: 'preflight', name: 'preflight_qa_login' },
  });
  if (!localCheck(response, 200, 'QA login') || !response.json('token')) fail('Local QA login failed.');
  const fixtureResponse = http.get(`${DOTNET}/api/admin/users?nombre=QA%20ZAP&correo=example.test&estado=1&page=1&pageSize=100`, {
    headers: { Authorization: `Bearer ${response.json('token')}` },
    tags: { name: 'preflight_qa_login_account_pool' }, responseType: 'text', timeout: '10s',
  });
  if (!localCheck(fixtureResponse, 200, 'Local QA account pool')) fail('Could not load QA login fixtures.');
  const accounts = (fixtureResponse.json('items') || []).filter(user => user.correo && user.nombre?.startsWith('QA ZAP ')).map(user => user.correo);
  if (accounts.length < 5) fail('At least five distinct QA ZAP accounts are required for concurrent login coverage.');
  return { token: response.json('token'), accounts };
}

function get(token, url, name) {
  return http.get(url, {
    headers: { Authorization: `Bearer ${token}` },
    tags: { name }, timeout: '30s', responseType: 'none',
  });
}

function record(id, name, response, expected = 200) {
  const ok = response && response.status === expected;
  recordSample(id, name, response ? response.timings.duration : 0, !!ok);
  if (__ENV.PERF_MODE === 'stress' && id !== 'CON-564') recordSample('CON-564', name, response ? response.timings.duration : 0, !!ok);
  check(response, { [`${id} ${name} HTTP ${expected}`]: r => r.status === expected });
  return response;
}

function perform(id, name, request, expected = 200) {
  let response;
  try { response = request(); }
  catch (_) { response = null; }
  return record(id, name, response, expected);
}

const filters = [
  '?nombre=QA&rolId=1&estado=1&page=1&pageSize=10',
  '?nombre=QA&rolId=2&estado=1&page=1&pageSize=10',
  '?nombre=QA&rolId=1&estado=2&page=1&pageSize=10',
  '?correo=example.test&estado=1&page=1&pageSize=10',
];

export const workloads = [
  { id: 'CON-553', name: 'login', run: (d, i, id = 'CON-553') => {
    const accounts = d.accounts || [__ENV.QA_ADMIN_EMAIL];
    const correo = accounts[(__VU - 1) % accounts.length];
    return perform(id, 'dotnet_login', () => http.post(`${DOTNET}/api/users/login`, JSON.stringify({ correo, password: __ENV.QA_ADMIN_PASSWORD }), { ...json, tags: { case: id, name: 'dotnet_login' } }));
  } },
  { id: 'CON-554', name: 'users_list', run: (d, i, id = 'CON-554') => perform(id, 'dotnet_users_list', () => get(d.token, `${DOTNET}/api/admin/users?page=1&pageSize=10&nombre=QA`, 'dotnet_users_list')) },
  { id: 'CON-555', name: 'users_filters', run: (d, i, id = 'CON-555') => perform(id, 'dotnet_users_filters', () => get(d.token, `${DOTNET}/api/admin/users${filters[i % filters.length]}`, 'dotnet_users_filters')) },
  { id: 'CON-556', name: 'services_search', run: (d, i, id = 'CON-556') => perform(id, 'java_services_search', () => get(d.token, `${JAVA}/api/servicios/buscar?texto=${i % 2 ? 'QA' : 'Servicio'}&limit=10`, 'java_services_search')) },
  { id: 'CON-557', name: 'promotions_filters', run: (d, i, id = 'CON-557') => {
    const q = [
      'servicioId=2147483647', 'trabajadorId=2147483647', 'planId=2147483647', 'estado=PENDIENTE',
    ][i % 4];
    return perform(id, 'java_promotions_filters', () => get(d.token, `${JAVA}/api/promociones/paginadas?page=0&size=10&${q}`, 'java_promotions_filters'));
  } },
  { id: 'CON-558', name: 'reports_volumes', run: (d, i, id = 'CON-558') => {
    const size = [5, 10, 20][i % 3];
    return perform(id, 'java_admin_reports', () => get(d.token, `${JAVA}/api/reportes/admin?page=0&size=${size}&estado=PENDIENTE`, 'java_admin_reports'));
  } },
  { id: 'CON-559', name: 'user_statistics', run: (d, i, id = 'CON-559') => {
    const dates = i % 2 ? '?fechaDesde=2020-01-01&fechaHasta=2030-12-31' : '';
    return perform(id, 'dotnet_user_statistics', () => get(d.token, `${DOTNET}/api/admin/statistics/users${dates}`, 'dotnet_user_statistics'));
  } },
];

export function mixedIteration(data) {
  const index = (__ITER + __VU - 1) % workloads.length;
  workloads[index].run(data, __ITER + __VU);
}

export function stressIteration(data) {
  const index = (__ITER + __VU - 1) % workloads.length;
  workloads[index].run(data, __ITER + __VU);
}

export function reportSummary(data, mode, profile, cases) {
  const metrics = data.metrics || {};
  const summary = {
    metrics,
    root_group: data.root_group,
    state: { testRunDurationMs: data.state?.testRunDurationMs },
    metadata: {
      generatedAt: new Date().toISOString(),
      tool: `Grafana k6 ${__ENV.K6_VERSION || '2.3.0'}`,
      mode,
      targets: [DOTNET, JAVA],
      profile,
      cases: cases.map(id => {
        const base = `case_${suffix(id)}`;
        const requests = metrics[`${base}_requests`]?.values?.count || 0;
        return { id, status: requests > 0 ? 'Pass' : 'Fail', requests, failures: 1-(metrics[`${base}_success`]?.values?.rate ?? 1), durationMs: metrics[`${base}_duration`]?.values || null };
      }),
      endpoints: ENDPOINTS.map(endpoint => ({
        endpoint,
        requests: metrics[`endpoint_${endpoint}_requests`]?.values?.count || 0,
        durationMs: metrics[`endpoint_${endpoint}_duration`]?.values || null,
        failureRate: 1-(metrics[`endpoint_${endpoint}_success`]?.values?.rate ?? 1),
      })).filter(x => x.requests).sort((a, b) => (b.durationMs?.['p(95)'] || 0) - (a.durationMs?.['p(95)'] || 0)),
      officialSlaDefined: false,
      interpretation: 'Descriptive measurements from the local QA run; no business SLA was defined.',
      secretsExcluded: true,
    },
  };
  const path = __ENV.PERF_SUMMARY_PATH;
  if (!path) fail('PERF_SUMMARY_PATH is required.');
  return { [path]: JSON.stringify(summary, null, 2) };
}
