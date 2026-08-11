import { ClerkProvider } from "@clerk/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import ReactDOM from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { App } from "./App";
import { getPublicConfig } from "./api";
import "./styles.css";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

const router = createBrowserRouter([{ path: "*", element: <App /> }]);
const root = ReactDOM.createRoot(document.getElementById("root") as HTMLElement);

async function render() {
  const config = import.meta.env.VITEST ? { clerkPublishableKey: null } : await getPublicConfig();
  const clerkPublishableKey = config.clerkPublishableKey?.startsWith("pk_") ? config.clerkPublishableKey : undefined;

  const app = (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );

  root.render(
    <React.StrictMode>
      {clerkPublishableKey ? (
        <ClerkProvider publishableKey={clerkPublishableKey}>
          {app}
        </ClerkProvider>
      ) : (
        app
      )}
    </React.StrictMode>,
  );
}

void render();
