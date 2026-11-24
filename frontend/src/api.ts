export type Session = { token: string; roles: string[] };
export type Ticket = { id: string; title: string; description: string; status: string; createdAt: string; ownerId: string };
export async function request<T>(path: string, method = 'GET', body?: unknown, token?: string): Promise<T> {
  const response = await fetch('/api' + path, {
    method,
    headers: { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: 'Bearer ' + token } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (!response.ok) {
    const data = await response.json().catch(() => null);
    throw new Error(response.status === 401 ? 'Invalid credentials or expired session. Please sign in again.' : data?.error || 'Request failed (' + response.status + ')');
  }
  return response.status === 204 ? undefined as T : response.json();
}
