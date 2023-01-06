import { test, expect } from '@playwright/test';

// Change this to be the URL of your scaled-out app. 
let siteUrl = 'https://githubbrowserrazorapp.redsand-dec7a119.eastus.azurecontainerapps.io/';

// A list of organizations to search for
let organizations: string[] = ['microsoft', 'azure', 'github', 'netflix', 'twitter', 'facepunch', 'dotnet', 'apache', 'ibm', 'openapi', 'facebook'];

test('has title', async ({ page }) => {
  await page.goto(siteUrl);

  // Expect a title "to contain" a substring.
  await expect(page).toHaveTitle(/GitHub Browser/);
});

test('can search for repos', async ({ page }) => {
  await page.goto(siteUrl);

  // Get a random organization name
  var rnd = Math.floor(Math.random() * organizations.length);
  var searchTerm = organizations[rnd];

  // Fill an input.
  console.log("Searching for " + searchTerm);
  await page.locator('#searchTerm').fill(searchTerm);

  // Click the search button.
  await page.getByRole('button', { name: 'Search' }).click();
  await page.waitForLoadState();

  //  Make sure the search text was persisted
  var searchBox = await page.locator('#searchTerm');
  await expect(searchBox).toHaveValue(searchTerm);

  // Make sure the search is included in the list of recent searches
  var recentSearchButton = await page.getByRole('button', { name: searchTerm });
  await expect(await recentSearchButton.count()).toBeGreaterThan(0);

  // Make sure the search results are visible
  var favoriteButtons = await page.getByRole('button', { name: 'Favorite' });
  await expect(await favoriteButtons.count()).toBeGreaterThan(0);
});

test('can see recent searches', async ({ page }) => {
  await page.goto(siteUrl);
  var randomOrgs = organizations.sort(() => Math.random() - Math.random()).slice(0, 5);
  for (const org of randomOrgs) {
    // Fill an input.
    console.log("Searching for " + org);
    await page.locator('#searchTerm').fill(org);

    // Click the search button.
    await page.getByRole('button', { name: 'Search' }).click();
    await page.waitForLoadState();
  }

  // Make sure the recent searches are visible
  for (const org of randomOrgs) {
    console.log("Validating " + org + " is still here.");
    var recentSearchButton = await page.getByRole('button', { name: org });
    await expect(await recentSearchButton.count()).toBeGreaterThan(0);
  }
});