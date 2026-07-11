import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  ArrowDownAZ,
  Database,
  Download,
  FileSearch,
  Folder,
  HardDrive,
  RefreshCw,
  Search,
  X
} from "lucide-react";
import { useMemo, useState } from "react";
import type { ReactNode } from "react";
import { createDownloadLink, search, stats, type SearchHit } from "./api";
import { useDebouncedValue } from "./useDebouncedValue";

const limit = 20;

export function App() {
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
