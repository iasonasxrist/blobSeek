export type PublicConfig = {
  clerkPublishableKey: string | null;
};

export async function getPublicConfig(): Promise<PublicConfig> {
  const response = await fetch("/api/public-config", { headers: { Accept: "application/json" } });
  if (!response.ok) return { clerkPublishableKey: null };
  return response.json() as Promise<PublicConfig>;
}

export type SearchHit = {
  documentId: string;
  name: string;
  nameHtml: string;
  path: string;
  pathHtml: string;
  storageAccount: string;
  container: string;
  extension: string;
  contentType: string;
  size: number;
  lastModified: string;
  excerptHtml: string;
};

export type SearchResponse = {
  hits: SearchHit[];
  total: number;
  facets: {
    containers: Record<string, number>;
    extensions: Record<string, number>;
  };
  processingMilliseconds: number;
};

export type IndexingStats = {
  documents: number;
  lastCompletedSync: string | null;
  containers: string[];
  statusCounts: Record<string, number>;
  lastRun: {
    runId: string;
    status: string;
    blobsSeen: number;
    blobsIndexed: number;
    blobsSkipped: number;
    blobsFailed: number;
    blobsDeleted: number;
  } | null;
};

export type SearchParams = {
  q: string;
  container?: string;
  ext?: string;
  sort?: string;
  offset: number;
  limit: number;
};

function authHeaders(authToken?: string) {
  return authToken ? { Authorization: `Bearer ${authToken}` } : undefined;
}

export async function search(params: SearchParams, authToken?: string): Promise<SearchResponse> {
  const query = new URLSearchParams({
    q: params.q,
    offset: String(params.offset),
    limit: String(params.limit)
  });
  if (params.container) query.set("container", params.container);
  if (params.ext) query.set("ext", params.ext);
  if (params.sort) query.set("sort", params.sort);

  const response = await fetch(`/api/search?${query}`, { headers: authHeaders(authToken) });
  if (!response.ok) throw new Error(`Search failed with HTTP ${response.status}`);
  return response.json() as Promise<SearchResponse>;
}

export async function stats(authToken?: string): Promise<IndexingStats> {
  const response = await fetch("/api/stats", { headers: authHeaders(authToken) });
  if (!response.ok) throw new Error(`Stats failed with HTTP ${response.status}`);
  return response.json() as Promise<IndexingStats>;
}

export async function createDownloadLink(hit: SearchHit, authToken?: string): Promise<string> {
  const query = new URLSearchParams({
    storageAccount: hit.storageAccount,
    container: hit.container,
    path: hit.path
  });
  const response = await fetch(`/api/download?${query}`, { headers: authHeaders(authToken) });
  if (!response.ok) throw new Error(`Download link failed with HTTP ${response.status}`);
  const body = (await response.json()) as { url: string };
  return body.url;
}

export type CheckoutPlan = "pro" | "team" | "business";

export async function createCheckoutSession(plan: CheckoutPlan, idempotencyKey: string, authToken?: string): Promise<string> {
  const response = await fetch("/api/billing/checkout", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(authHeaders(authToken) ?? {})
    },
    body: JSON.stringify({ plan, idempotencyKey })
  });
  if (!response.ok) throw new Error(`Checkout failed with HTTP ${response.status}`);
  const body = (await response.json()) as { url: string };
  return body.url;
}
