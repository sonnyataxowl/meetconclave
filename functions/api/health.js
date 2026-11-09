import { json, methodNotAllowed } from '../_lib/http.js';

export function onRequestGet() {
  return json({ status: 'healthy', service: 'conclave', time: new Date().toISOString() });
}

export function onRequest() {
  return methodNotAllowed(['GET']);
}
