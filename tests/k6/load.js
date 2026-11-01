import { login, query } from './common.js';
export const options = {
  scenarios: {
    login: { executor: 'constant-vus', vus: 3, duration: '2m', exec: 'authentication' },
    users: { executor: 'constant-vus', vus: 5, duration: '2m', exec: 'users' }
  },
  thresholds: { http_req_failed: ['rate<0.01'], checks: ['rate>0.99'], http_req_duration: ['p(95)<2000'] }
};
export function setup() { return { token: login('admin.test@connectaoficios.local') }; }
export function authentication() { login('cliente.test@connectaoficios.local'); }
export function users(data) { query(data.token); }
