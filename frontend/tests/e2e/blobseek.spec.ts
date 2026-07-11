import { expect, test } from "@playwright/test";

test("loads the search shell", async ({ page }) => {
  await page.route("**/api/search**", async (route) => {
    await route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        hits: [],
        total: 0,
        facets: { containers: {}, extensions: {} },
        processingMilliseconds: 1
      })
    });
  });
  await page.route("**/api/stats", async (route) => {
    await route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        documents: 0,
        lastCompletedSync: null,
        containers: [],
        statusCounts: {},
        lastRun: null
      })
    });
  });

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "BlobSeek" })).toBeVisible();
  await expect(page.getByRole("searchbox", { name: "Search blobs" })).toBeVisible();
});
