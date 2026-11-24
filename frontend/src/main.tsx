import { StrictMode, useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { createRoot } from 'react-dom/client';
import { request } from './api';
import type { Session, Ticket } from './api';
import './style.css';

function App() {
  const [session, setSession] = useState<Session | null>(null);
  return session ? <Workspace session={session} logout={() => setSession(null)} /> : <Auth login={setSession} />;
}

function Auth({ login }: { login: (session: Session) => void }) {
  const [register, setRegister] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true); setMessage('');
    try {
      if (register) {
        await request('/auth/register', 'POST', { email, password });
        setRegister(false); setMessage('Account created. Sign in to continue.');
      } else login(await request<Session>('/auth/login', 'POST', { email, password }));
    } catch (error) { setMessage(String(error instanceof Error ? error.message : error)); }
    finally { setBusy(false); }
  }
  return <main className="auth"><form className="panel" onSubmit={submit}>
    <p className="brand">SupportDesk</p><h1>{register ? 'Create an account' : 'Welcome back'}</h1>
    <label>Email<input type="email" required value={email} onChange={e => setEmail(e.target.value)} /></label>
    <label>Password<input type="password" required maxLength={128} autoComplete={register ? 'new-password' : 'current-password'} value={password} onChange={e => setPassword(e.target.value)} /></label>
    <button disabled={busy}>{busy ? 'Please wait' : register ? 'Register' : 'Sign in'}</button>
    <p role="status">{message}</p>
    <button type="button" className="secondary" onClick={() => { setRegister(!register); setMessage(''); }}>{register ? 'Back to sign in' : 'Create an account'}</button>
  </form></main>;
}

function Workspace({ session, logout }: { session: Session; logout: () => void }) {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('All');
  const [sort, setSort] = useState('newest');
  const counts = {
    Open: tickets.filter(ticket => ticket.status === 'Open').length,
    InProgress: tickets.filter(ticket => ticket.status === 'InProgress').length,
    Resolved: tickets.filter(ticket => ticket.status === 'Resolved').length
  };
  const query = search.trim().toLowerCase();
  const visibleTickets = tickets.filter(ticket =>
    (statusFilter === 'All' || ticket.status === statusFilter) &&
    (ticket.title.toLowerCase().includes(query) || ticket.description.toLowerCase().includes(query))
  ).sort((a, b) => sort === 'oldest'
    ? Date.parse(a.createdAt) - Date.parse(b.createdAt)
    : Date.parse(b.createdAt) - Date.parse(a.createdAt));
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [editing, setEditing] = useState<Ticket | null>(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const privileged = session.roles.some(role => role === 'Admin' || role === 'Agent');
  async function refresh() {
    setLoading(true);
    try { setTickets(await request<Ticket[]>('/tickets', 'GET', undefined, session.token)); }
    catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setLoading(false); }
  }
  useEffect(() => { void refresh(); }, [session.token]);
  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setMessage('');
    try {
      await request(editing ? '/tickets/' + editing.id : '/tickets', editing ? 'PUT' : 'POST',
        { title, description, ...(editing ? { status: editing.status } : {}) }, session.token);
      setEditing(null); setTitle(''); setDescription(''); await refresh();
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  }
  async function remove(id: string) {
    if (!window.confirm('Delete this ticket?')) return;
    setBusy(true); setMessage('');
    try { await request('/tickets/' + id, 'DELETE', undefined, session.token); await refresh(); }
    catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  }
  return <><header><strong>SupportDesk</strong><span>{session.roles.join(', ')}</span><button className="secondary" onClick={logout}>Sign out</button></header>
    <main className="workspace"><h1>Support tickets</h1><p>Report issues and track their progress.</p>
    <p role="status">{message}</p>
    <section className="overview" aria-label="Ticket overview" aria-busy={loading}>
      {[
        { label: 'Total tickets', count: tickets.length, filter: 'All' },
        { label: 'Open', count: counts.Open, filter: 'Open' },
        { label: 'In progress', count: counts.InProgress, filter: 'InProgress' },
        { label: 'Resolved', count: counts.Resolved, filter: 'Resolved' }
      ].map(item => <button type="button" className="metric" key={item.filter}
        aria-pressed={statusFilter === item.filter} onClick={() => setStatusFilter(item.filter)}>
        <span>{item.label}</span><strong>{loading ? '—' : item.count}</strong>
      </button>)}
    </section>
    <section className="panel breakdown"><h2>Ticket resolution</h2>
      <p>{loading ? 'Loading statistics...' : tickets.length ? Math.round(counts.Resolved / tickets.length * 100) + '% of accessible tickets resolved' : 'No ticket data yet'}</p>
      <div className="progress-track" role="progressbar" aria-label="Resolved tickets" aria-valuemin={0} aria-valuemax={100} aria-valuenow={tickets.length ? Math.round(counts.Resolved / tickets.length * 100) : 0}>
        <div style={{ width: (tickets.length ? counts.Resolved / tickets.length * 100 : 0) + '%' }} />
      </div>
    </section>
    <form className="panel" onSubmit={save}><h2>{editing ? 'Edit ticket' : 'Report an issue'}</h2>
      <label>Title<input required maxLength={120} value={title} onChange={e => setTitle(e.target.value)} /></label>
      <label>Description<textarea required maxLength={4000} value={description} onChange={e => setDescription(e.target.value)} /></label>
      {editing && privileged && <label>Status<select value={editing.status} onChange={e => setEditing({ ...editing, status: e.target.value })}>{['Open','InProgress','Resolved'].map(status => <option key={status}>{status}</option>)}</select></label>}
      <button disabled={busy}>{editing ? 'Save changes' : 'Create ticket'}</button>
      {editing && <button type="button" className="secondary" onClick={() => { setEditing(null); setTitle(''); setDescription(''); }}>Cancel</button>}
    </form>
    <section className="tickets"><h2>Tickets</h2>
      <div className="filters">
        <label>Search tickets<input type="search" placeholder="Search title or description" value={search} onChange={e => setSearch(e.target.value)} /></label>
        <label>Status<select value={statusFilter} onChange={e => setStatusFilter(e.target.value)}>
          <option value="All">All statuses</option><option value="Open">Open</option><option value="InProgress">In progress</option><option value="Resolved">Resolved</option>
        </select></label>
        <label>Sort by<select value={sort} onChange={e => setSort(e.target.value)}><option value="newest">Newest first</option><option value="oldest">Oldest first</option></select></label>
      </div>
      {!loading && <p className="result-count" aria-live="polite">Showing {visibleTickets.length} of {tickets.length} tickets</p>}
      {loading ? <p>Loading tickets...</p> : visibleTickets.length === 0 ? <p>{tickets.length ? 'No tickets match your filters.' : 'No tickets found.'}</p> : visibleTickets.map(ticket => <article className="panel" key={ticket.id}>
      <div className="ticket-heading"><h3>{ticket.title}</h3><span className={"status status-" + ticket.status}>{ticket.status === "InProgress" ? "In progress" : ticket.status}</span></div>
      <p className="description">{ticket.description}</p><small>{new Date(ticket.createdAt).toLocaleString()}</small>
      <div className="actions"><button disabled={busy} className="secondary" onClick={() => { setEditing(ticket); setTitle(ticket.title); setDescription(ticket.description); }}>Edit</button>
      {session.roles.includes('Admin') && <button disabled={busy} className="secondary" onClick={() => void remove(ticket.id)}>Delete</button>}</div>
    </article>)}</section></main></>;
}
createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
