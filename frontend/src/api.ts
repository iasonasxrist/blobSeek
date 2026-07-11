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

export async function search(params: SearchParams): Promise<SearchResponse> {
  const query = new URLSearchParams({
    q: params.q,
    offset: String(params.offset),
    limit: String(params.limit)
  });
  if (params.container) query.set("container", params.container);
  if (params.ext) query.set("ext", params.ext);
  if (params.sort) query.set("sort", params.sort);

  const response = await fetch(`/api/search?${query}`);
  if (!response.ok) throw new Error(`Search failed with HTTP ${response.status}`);
  return response.json() as Promise<SearchResponse>;
}

export async function stats(): Promise<IndexingStats> {
  const response = await fetch("/api/stats");
  if (!response.ok) throw new Error(`Stats failed with HTTP ${response.status}`);
  return response.json() as Promise<IndexingStats>;
}

export async function createDownloadLink(hit: SearchHit): Promise<string> {
  const query = new URLSearchParams({
    storageAccount: hit.storageAccount,
    container: hit.container,
    path: hit.path
  });
  const response = await fetch(`/api/download?${query}`);
  if (!response.ok) throw new Error(`Download link failed with HTTP ${response.status}`);
  const body = (await response.json()) as { url: string };
  return body.url;
}
