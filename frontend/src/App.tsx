import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  ArrowDownAZ,
  ArrowRight,
  BadgeDollarSign,
  CheckCircle2,
  Cloud,
  Database,
  Download,
  FileSearch,
  Folder,
  Gauge,
  HardDrive,
  Lock,
  RefreshCw,
  Search,
  Server,
  ShieldCheck,
  X
} from "lucide-react";
import { useMemo, useState } from "react";
import type { ReactNode } from "react";
import { createDownloadLink, search, stats, type SearchHit } from "./api";
import { useDebouncedValue } from "./useDebouncedValue";

const limit = 20;

export function App() {
  if (!window.location.pathname.startsWith("/search")) {
    return <LandingPage />;
  }

  return <SearchConsole />;
}

function LandingPage() {
  const comparisonRows = [
    ["Full-text inside blobs", "PDF, DOCX, text, and code files", "Yes, with a managed search service"],
    ["Indexing cost profile", "Runs on your Docker infrastructure", "Metered Azure Search service tiers"],
    ["Data boundary", "Self-hosted API, PostgreSQL, Lucene volume", "Managed Azure service boundary"],
    ["Incremental sync", "ETag-aware downloads only when blobs change", "Azure indexer pipeline"],
    ["Best fit", "Private blob search with predictable ownership", "Enterprise search platform needs"]
  ];

  return (
    <div className="min-h-screen bg-[#f7f7f2] text-slate-950">
      <header className="border-b border-slate-200 bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-4">
          <a className="flex items-center gap-2" href="/" aria-label="BlobSeek home">
            <span className="grid h-10 w-10 place-items-center rounded-md bg-lagoon text-white">
              <FileSearch size={21} />
            </span>
            <span>
              <span className="block text-lg font-semibold leading-none">BlobSeek</span>
              <span className="block text-xs text-slate-500">Self-hosted Azure Blob search</span>
            </span>
          </a>
          <nav className="hidden items-center gap-6 text-sm font-medium text-slate-600 md:flex" aria-label="Primary">
            <a className="hover:text-lagoon" href="#compare">Compare</a>
            <a className="hover:text-lagoon" href="#workflow">Workflow</a>
            <a className="hover:text-lagoon" href="#deploy">Deploy</a>
          </nav>
          <a className="inline-flex h-10 items-center gap-2 rounded-md bg-slate-950 px-4 text-sm font-semibold text-white hover:bg-lagoon" href="/search">
            Open app
            <ArrowRight size={16} />
          </a>
        </div>
      </header>

      <main>
        <section className="overflow-hidden border-b border-slate-200 bg-white">
          <div className="mx-auto grid max-w-7xl gap-10 px-4 py-14 lg:grid-cols-[1.02fr_0.98fr] lg:items-center lg:py-20">
            <div className="max-w-3xl">
              <div className="mb-5 inline-flex items-center gap-2 rounded-md border border-lagoon/20 bg-lagoon/10 px-3 py-2 text-sm font-semibold text-lagoon">
                <BadgeDollarSign size={16} />
                Reduce Azure AI Search spend for internal blob discovery
              </div>
              <h1 className="text-4xl font-semibold leading-tight tracking-normal text-slate-950 md:text-6xl">
                Search your blob storage like a product, not a billing surprise.
              </h1>
              <p className="mt-5 max-w-2xl text-lg leading-8 text-slate-600">
                BlobSeek is a Dockerized, self-hosted alternative for teams that need fast private search across Azure Blob Storage without adopting a standing managed search platform.
              </p>
              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <a className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-lagoon px-5 text-sm font-bold text-white shadow-sm hover:bg-teal-800" href="/search">
                  Launch search console
                  <ArrowRight size={17} />
                </a>
                <a className="inline-flex h-12 items-center justify-center rounded-md border border-slate-300 bg-white px-5 text-sm font-bold text-slate-800 shadow-sm hover:border-lagoon hover:text-lagoon" href="#compare">
                  Compare value
                </a>
              </div>
              <dl className="mt-9 grid max-w-2xl grid-cols-3 gap-3">
                <Metric value="0" label="external search servers required" />
                <Metric value="ETag" label="change-aware indexing" />
                <Metric value="Docker" label="local or production deploy" />
              </dl>
            </div>

            <div className="rounded-md border border-slate-200 bg-slate-950 p-4 shadow-2xl shadow-slate-300/50">
              <div className="rounded-md border border-white/10 bg-[#0e1726] p-4 text-white">
                <div className="mb-4 flex items-center justify-between border-b border-white/10 pb-3">
                  <div className="flex items-center gap-2">
                    <Search size={18} className="text-teal-300" />
                    <span className="font-mono text-sm text-slate-200">invoice vendor terms</span>
                  </div>
                  <span className="font-mono text-xs text-teal-300">18 ms</span>
                </div>
                <div className="space-y-3">
                  <MockResult name="finance/2026/vendor-invoice-0421.pdf" type="pdf" meta="invoices · 248 KB · indexed" />
                  <MockResult name="legal/contracts/master-service-agreement.docx" type="docx" meta="contracts · 91 KB · indexed" />
                  <MockResult name="exports/customer-ledger-jan.csv" type="csv" meta="reports · 2.4 MB · text indexed" />
                </div>
                <div className="mt-5 grid gap-3 border-t border-white/10 pt-4 sm:grid-cols-3">
                  <MiniSignal icon={<Cloud size={16} />} label="Azure Blob" />
                  <MiniSignal icon={<Database size={16} />} label="PostgreSQL" />
                  <MiniSignal icon={<Server size={16} />} label="Lucene.NET" />
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="mx-auto grid max-w-7xl gap-4 px-4 py-12 md:grid-cols-3">
          <ValueCard icon={<Gauge size={22} />} title="Fast after indexing" body="The API searches Lucene directly, so user queries never download or parse blobs at request time." />
          <ValueCard icon={<ShieldCheck size={22} />} title="Keep control" body="Run it inside your own network and keep Azure credentials server-side." />
          <ValueCard icon={<Lock size={22} />} title="Predictable ownership" body="Use the compute and Postgres stack you already operate instead of another always-on search service." />
        </section>

        <section id="compare" className="border-y border-slate-200 bg-white">
          <div className="mx-auto max-w-7xl px-4 py-14">
            <div className="mb-7 flex flex-col justify-between gap-4 md:flex-row md:items-end">
              <div>
                <p className="text-sm font-bold uppercase tracking-wide text-lagoon">Value comparison</p>
                <h2 className="mt-2 text-3xl font-semibold tracking-normal">Built for blob search, not a platform migration.</h2>
              </div>
              <p className="max-w-xl text-sm leading-6 text-slate-600">
                Azure AI Search is excellent when you need a managed enterprise search platform. BlobSeek is focused on private Azure Blob full-text search with lower operational surface area.
              </p>
            </div>
            <div className="overflow-hidden rounded-md border border-slate-200">
              <table className="w-full border-collapse bg-white text-left text-sm">
                <thead className="bg-slate-950 text-white">
                  <tr>
                    <th className="p-4 font-semibold">Capability</th>
                    <th className="p-4 font-semibold">BlobSeek</th>
                    <th className="p-4 font-semibold">Azure AI Search</th>
                  </tr>
                </thead>
                <tbody>
                  {comparisonRows.map(([label, blobSeek, azure]) => (
                    <tr className="border-t border-slate-200" key={label}>
                      <td className="p-4 font-semibold text-slate-800">{label}</td>
                      <td className="p-4 text-slate-700"><CheckLine>{blobSeek}</CheckLine></td>
                      <td className="p-4 text-slate-600">{azure}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </section>

        <section id="workflow" className="mx-auto grid max-w-7xl gap-8 px-4 py-14 lg:grid-cols-[0.9fr_1.1fr] lg:items-start">
          <div>
            <p className="text-sm font-bold uppercase tracking-wide text-copper">Indexing model</p>
            <h2 className="mt-2 text-3xl font-semibold tracking-normal">Cheap repeat crawls because unchanged blobs stay untouched.</h2>
            <p className="mt-4 text-sm leading-7 text-slate-600">
              BlobSeek stores state in PostgreSQL and treats ETag as the primary change signal. The worker streams blob listings, downloads only what changed, and isolates failures.
            </p>
          </div>
          <ol className="grid gap-3 sm:grid-cols-2">
            {["List configured containers", "Compare blob ETags", "Download changed files", "Extract text into Lucene"].map((item, index) => (
              <li className="rounded-md border border-slate-200 bg-white p-5 shadow-sm" key={item}>
                <span className="font-mono text-sm font-semibold text-lagoon">0{index + 1}</span>
                <p className="mt-3 text-base font-semibold text-slate-900">{item}</p>
              </li>
            ))}
          </ol>
        </section>

        <section id="deploy" className="border-t border-slate-200 bg-slate-950 text-white">
          <div className="mx-auto grid max-w-7xl gap-8 px-4 py-12 md:grid-cols-[1fr_auto] md:items-center">
            <div>
              <h2 className="text-3xl font-semibold tracking-normal">Run BlobSeek with one Docker command.</h2>
              <p className="mt-3 max-w-2xl text-sm leading-7 text-slate-300">
                Compose starts the API, worker, React frontend, PostgreSQL, and persistent Lucene storage. Bring your Azure Storage connection string and index.
              </p>
            </div>
            <div className="rounded-md border border-white/10 bg-white/5 p-4 font-mono text-sm text-teal-200">
              docker compose up --build
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}

function SearchConsole() {
  const [query, setQuery] = useState("");
  const [container, setContainer] = useState<string>();
  const [extension, setExtension] = useState<string>();
  const [sort, setSort] = useState("");
  const [offset, setOffset] = useState(0);
  const debouncedQuery = useDebouncedValue(query.trim(), 180);

  const searchParams = { q: debouncedQuery, container, ext: extension, sort, offset, limit };
  const searchQuery = useQuery({
    queryKey: ["search", searchParams],
    queryFn: () => search(searchParams),
    placeholderData: (previous) => previous
  });
  const statsQuery = useQuery({ queryKey: ["stats"], queryFn: stats, refetchInterval: 60_000 });

  const result = searchQuery.data;
  const activeFilters = Boolean(container || extension);
  const shown = result?.hits.length ?? 0;
  const canLoadMore = result ? offset + shown < result.total : false;

  function resetPaging(action: () => void) {
    setOffset(0);
    action();
  }

  return (
    <div className="min-h-screen bg-stone-50 text-slate-900">
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-7xl flex-col gap-3 px-4 py-3 md:flex-row md:items-center">
          <div className="flex shrink-0 items-center gap-2">
            <div className="grid h-9 w-9 place-items-center rounded-md bg-lagoon text-white">
              <FileSearch size={20} />
            </div>
            <div>
              <h1 className="text-lg font-semibold leading-tight">BlobSeek</h1>
              <p className="text-xs text-slate-500">Azure Blob full-text search</p>
            </div>
          </div>

          <label className="relative flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
            <input
              aria-label="Search blobs"
              className="h-11 w-full rounded-md border border-slate-300 bg-white pl-10 pr-24 text-sm shadow-sm outline-none transition focus:border-lagoon focus:ring-2 focus:ring-lagoon/20"
              value={query}
              onChange={(event) => resetPaging(() => setQuery(event.target.value))}
              placeholder="Search names, paths, and document text"
              type="search"
            />
            <span className="absolute right-3 top-1/2 -translate-y-1/2 font-mono text-xs text-slate-400">
              {result ? `${result.processingMilliseconds} ms` : ""}
            </span>
          </label>

          <StatusPill stats={statsQuery.data} loading={statsQuery.isLoading} />
        </div>
      </header>

      <main className="mx-auto grid max-w-7xl gap-5 px-4 py-5 lg:grid-cols-[260px_1fr]">
        <aside className="space-y-5">
          <FacetPanel
            icon={<Folder size={16} />}
            title="Container"
            values={result?.facets.containers ?? {}}
            active={container}
            onSelect={(value) => resetPaging(() => setContainer(value === container ? undefined : value))}
          />
          <FacetPanel
            icon={<HardDrive size={16} />}
            title="File type"
            values={result?.facets.extensions ?? {}}
            active={extension}
            prefix="."
            onSelect={(value) => resetPaging(() => setExtension(value === extension ? undefined : value))}
          />
          <section className="space-y-2">
            <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
              <ArrowDownAZ size={16} />
              Sort
            </div>
            <select
              className="h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm shadow-sm outline-none focus:border-lagoon focus:ring-2 focus:ring-lagoon/20"
              value={sort}
              onChange={(event) => resetPaging(() => setSort(event.target.value))}
            >
              <option value="">Relevance</option>
              <option value="last_modified:desc">Newest first</option>
              <option value="last_modified:asc">Oldest first</option>
              <option value="size:desc">Largest first</option>
              <option value="size:asc">Smallest first</option>
            </select>
          </section>
          {activeFilters ? (
            <button
              className="inline-flex h-9 w-full items-center justify-center gap-2 rounded-md border border-slate-300 bg-white text-sm font-medium text-slate-700 shadow-sm hover:border-lagoon hover:text-lagoon"
              onClick={() => resetPaging(() => {
                setContainer(undefined);
                setExtension(undefined);
              })}
              type="button"
            >
              <X size={16} />
              Clear filters
            </button>
          ) : null}
        </aside>

        <section className="min-w-0">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="font-mono text-sm text-slate-500">
                {searchQuery.isError
                  ? "Search unavailable"
                  : result
                    ? `${result.total.toLocaleString()} ${result.total === 1 ? "result" : "results"}`
                    : "Loading index"}
              </p>
              {statsQuery.data?.lastRun ? (
                <p className="mt-1 text-xs text-slate-500">
                  Last run {statsQuery.data.lastRun.status.toLowerCase()} · {statsQuery.data.lastRun.blobsSeen.toLocaleString()} seen · {statsQuery.data.lastRun.blobsFailed.toLocaleString()} failed
                </p>
              ) : null}
            </div>
            {searchQuery.isFetching ? (
              <span className="inline-flex items-center gap-2 rounded-md bg-white px-3 py-2 text-xs font-medium text-slate-500 shadow-sm">
                <RefreshCw className="animate-spin" size={14} />
                Refreshing
              </span>
            ) : null}
          </div>

          {searchQuery.isError ? <EmptyState title="Search failed" detail={(searchQuery.error as Error).message} /> : null}
          {!searchQuery.isError && result?.hits.length === 0 ? (
            <EmptyState title="No blobs match" detail="Try a shorter query or clear the active filters. The worker may also still be building the first index." />
          ) : null}

          <ol className="space-y-3">
            {result?.hits.map((hit) => <ResultRow hit={hit} key={hit.documentId} />)}
          </ol>

          {canLoadMore ? (
            <button
              className="mx-auto mt-5 flex h-10 items-center justify-center rounded-md border border-slate-300 bg-white px-5 text-sm font-semibold text-slate-700 shadow-sm hover:border-lagoon hover:text-lagoon"
              onClick={() => setOffset((current) => current + limit)}
              type="button"
            >
              Load more
            </button>
          ) : null}
        </section>
      </main>
    </div>
  );
}

function Metric({ value, label }: { value: string; label: string }) {
  return (
    <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
      <dt className="font-mono text-lg font-semibold text-slate-950">{value}</dt>
      <dd className="mt-1 text-xs leading-5 text-slate-500">{label}</dd>
    </div>
  );
}

function MockResult({ name, type, meta }: { name: string; type: string; meta: string }) {
  return (
    <div className="rounded-md border border-white/10 bg-white/[0.04] p-3">
      <div className="flex items-center justify-between gap-3">
        <span className="break-all font-mono text-sm text-slate-100">{name}</span>
        <span className="rounded bg-teal-300/10 px-2 py-1 font-mono text-xs text-teal-200">.{type}</span>
      </div>
      <p className="mt-2 text-xs text-slate-400">{meta}</p>
    </div>
  );
}

function MiniSignal({ icon, label }: { icon: ReactNode; label: string }) {
  return (
    <div className="flex items-center gap-2 rounded-md bg-white/[0.04] px-3 py-2 text-xs text-slate-300">
      <span className="text-teal-300">{icon}</span>
      {label}
    </div>
  );
}

function ValueCard({ icon, title, body }: { icon: ReactNode; title: string; body: string }) {
  return (
    <article className="rounded-md border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4 grid h-11 w-11 place-items-center rounded-md bg-copper/10 text-copper">{icon}</div>
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="mt-2 text-sm leading-6 text-slate-600">{body}</p>
    </article>
  );
}

function CheckLine({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex items-center gap-2">
      <CheckCircle2 size={16} className="shrink-0 text-lagoon" />
      <span>{children}</span>
    </span>
  );
}

function StatusPill({ stats: value, loading }: { stats?: Awaited<ReturnType<typeof stats>>; loading: boolean }) {
  return (
    <div className="flex shrink-0 items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
      <Database size={16} className="text-lagoon" />
      <span className="font-mono">
        {loading ? "loading" : `${(value?.documents ?? 0).toLocaleString()} docs`}
      </span>
      <span className="hidden text-slate-300 sm:inline">/</span>
      <span className="hidden sm:inline">
        sync {value?.lastCompletedSync ? new Date(value.lastCompletedSync).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) : "pending"}
      </span>
    </div>
  );
}

function FacetPanel({
  icon,
  title,
  values,
  active,
  prefix = "",
  onSelect
}: {
  icon: ReactNode;
  title: string;
  values: Record<string, number>;
  active?: string;
  prefix?: string;
  onSelect: (value: string) => void;
}) {
  const entries = useMemo(() => Object.entries(values).slice(0, 12), [values]);
  return (
    <section className="space-y-2">
      <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
        {icon}
        {title}
      </div>
      <ul className="space-y-1">
        {entries.map(([value, count]) => (
          <li key={value}>
            <button
              className={`flex h-8 w-full items-center justify-between gap-3 rounded-md px-2 text-left text-sm ${
                active === value
                  ? "bg-lagoon text-white"
                  : "text-slate-700 hover:bg-white hover:text-lagoon hover:shadow-sm"
              }`}
              onClick={() => onSelect(value)}
              type="button"
            >
              <span className="truncate font-medium">{prefix}{value || "none"}</span>
              <span className={active === value ? "font-mono text-xs text-white/80" : "font-mono text-xs text-slate-400"}>
                {count.toLocaleString()}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}

function ResultRow({ hit }: { hit: SearchHit }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  async function download() {
    setLoading(true);
    setError(undefined);
    try {
      window.open(await createDownloadLink(hit), "_blank", "noopener");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create link");
    } finally {
      setLoading(false);
    }
  }

  return (
    <li className="rounded-md border border-slate-200 bg-white p-4 shadow-sm transition hover:border-lagoon/50">
      <div className="flex flex-wrap items-start gap-2">
        <h2
          className="min-w-0 flex-1 break-all font-mono text-sm font-semibold text-slate-900"
          dangerouslySetInnerHTML={{ __html: hit.nameHtml }}
        />
        <span className="rounded bg-slate-100 px-2 py-1 font-mono text-xs text-slate-600">{hit.container}</span>
        {hit.extension ? <span className="rounded bg-copper/10 px-2 py-1 font-mono text-xs text-copper">.{hit.extension}</span> : null}
      </div>
      <p
        className="mt-1 break-all font-mono text-xs text-slate-500"
        dangerouslySetInnerHTML={{ __html: hit.pathHtml }}
      />
      {hit.excerptHtml ? (
        <p
          className="mt-3 border-l-2 border-slate-200 pl-3 text-sm leading-6 text-slate-700"
          dangerouslySetInnerHTML={{ __html: hit.excerptHtml }}
        />
      ) : null}
      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 font-mono text-xs text-slate-500">
        <span>{formatSize(hit.size)}</span>
        <span>{new Date(hit.lastModified).toLocaleString()}</span>
        <span>{hit.storageAccount}</span>
        <button
          className="ml-auto inline-flex items-center gap-1 rounded-md px-2 py-1 font-sans text-sm font-semibold text-lagoon hover:bg-lagoon/10 disabled:text-slate-400"
          disabled={loading}
          onClick={download}
          type="button"
        >
          {loading ? <Activity className="animate-pulse" size={15} /> : <Download size={15} />}
          Download
        </button>
        {error ? <span className="basis-full text-right font-sans text-xs text-red-600">{error}</span> : null}
      </div>
    </li>
  );
}

function EmptyState({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="mt-12 rounded-md border border-dashed border-slate-300 bg-white p-8 text-center">
      <FileSearch className="mx-auto text-slate-300" size={34} />
      <h2 className="mt-3 text-base font-semibold">{title}</h2>
      <p className="mx-auto mt-1 max-w-md text-sm text-slate-500">{detail}</p>
    </div>
  );
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  const units = ["KB", "MB", "GB", "TB"];
  let value = bytes;
  let unit = -1;
  do {
    value /= 1024;
    unit += 1;
  } while (value >= 1024 && unit < units.length - 1);
  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unit]}`;
}
