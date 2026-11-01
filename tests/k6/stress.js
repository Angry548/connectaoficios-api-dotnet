import { login, query } from './common.js';
export const options = {
  scenarios: {
    login: { executor: 'ramping-vus', startVUs: 1, stages: [{ duration: '30s', target: 5 }, { duration: '1m', target: 20 }, { duration: '1m', target: 40 }, { duration: '30s', target: 0 }], exec: 'authentication' },
    users: { executor: 'ramping-vus', startVUs: 1, stages: [{ duration: '30s', target: 5 }, { duration: '1m', target: 20 }, { duration: '1m', target: 40 }, { duration: '30s', target: 0 }], exec: 'users' }
  },
  thresholds: { http_req_failed: ['rate<0.05'], checks: ['rate>0.95'], http_req_duration: ['p(95)<5000'] }
};
export function setup() { return { token: login('admin.test@connectaoficios.local') }; }
export function authentication() { login('cliente.test@connectaoficios.local'); }
export function users(data) { query(data.token); }
