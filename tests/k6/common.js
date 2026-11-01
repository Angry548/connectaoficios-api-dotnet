import http from 'k6/http';
import { check, sleep } from 'k6';
export const base = __ENV.BASE_URL || 'http://127.0.0.1:8080';
if (!/^http:\/\/(localhost|127\.0\.0\.1|connectaoficios-api)(:\d+)?$/.test(base)) throw new Error('Solo API Docker local');
export const json = { headers: { 'Content-Type': 'application/json' } };
export function login(email) {
  const response = http.post(`${base}/api/users/login`, JSON.stringify({ correo: email, password: __ENV.TEST_PASSWORD || 'LocalOnly-Connecta-2026!' }), json);
  check(response, { 'login 200': r => r.status === 200 });
  if (response.status !== 200) throw new Error('Login local fallido');
  return response.json('token');
}
export function query(token) {
  for (const suffix of ['?page=1&pageSize=10', '?page=1&pageSize=10&nombre=Cliente&rolId=1&estado=1', '/search?texto=local&limit=10']) {
    const r = http.get(`${base}/api/admin/users${suffix}`, { headers: { Authorization: `Bearer ${token}` } });
    check(r, { 'consulta 200': x => x.status === 200 });
  }
  sleep(1);
}
