import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { App } from "../src/App";

vi.stubGlobal("fetch", vi.fn((url: string) => {
  if (url.includes("/api/stats")) {
    return Promise.resolve(new Response(JSON.stringify({
      documents: 12,
      lastCompletedSync: null,
      containers: ["invoices"],
      statusCounts: { Indexed: 12 },
      lastRun: null
    })));
  }

  return Promise.resolve(new Response(JSON.stringify({
    hits: [],
    total: 0,
    facets: { containers: {}, extensions: {} },
    processingMilliseconds: 2
  })));
}));

describe("App", () => {
  it("renders the BlobSeek search console", async () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <App />
      </QueryClientProvider>
    );

    expect(screen.getByRole("heading", { name: "BlobSeek" })).toBeInTheDocument();
    expect(screen.getByRole("searchbox", { name: "Search blobs" })).toBeInTheDocument();
    expect(await screen.findByText("No blobs match")).toBeInTheDocument();
  });
});
