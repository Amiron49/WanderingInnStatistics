# Wandering Inn Statistics
- C# goes brr to download
- Angular goes boop to display

You can view the statistics [here](https://wandering-inn-statistics.aris.moe/).

## Scraping code is not public

All scraping logic lives in the `WanderingInnStats.Scrapping` assembly, which is a git submodule pointing at a **private** repository.
That includes anything that fetches pages or knows about their structure: URLs, HTTP calls, HTML parsing (AngleSharp), CSS selectors,
link filtering and scrape diagnostics.

**Do not put scraping logic anywhere else.** Every other project in this repository is public. If you need new scraping behaviour, add it
to `WanderingInnStats.Scrapping` and consume it from the other projects through its public API (`Scrapper`, `WikiScrapper`,
`ScrapeDiagnostics`, ...).
