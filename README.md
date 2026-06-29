# Free Agency Auction API

Backend for **FanPools**, a dynasty fantasy-football platform running two [MyFantasyLeague](https://www.myfantasyleague.com/) (MFL) leagues (~25 owners). This is the core service: it powers a real-time free-agent auction, enforces a salary-cap / contract rulebook, and keeps everything in sync with MFL.

## Highlights

- **Real-time auction** — eBay-style bidding with a shared, multi-user countdown over **SignalR**. Owners nominate players, bids extend the timer, and the high bid at expiry wins.
- **In-process win pipeline** — winning bids flow through a `Channel<BidDTO>` consumed by a hosted `BackgroundService` that validates the result, writes the contract to MFL, recomputes cap room, and notifies the league. This replaced an Azure Storage Queue + Function during a cost migration — same semantics, no cloud queue to run.
- **Cap computed from the source of truth** — salary-cap room is always derived from live rosters (`roster + taxi×20% + IR×50% + adjustments`), never from MFL's `bbidAvailableBalance`, which is stale outside FCFS waiver windows. This avoids a class of silent cap bugs.
- **Domain rulebook, server-enforced** — franchise tags, amnesty buyouts, holdouts, taxi squads, 5th-year options, waiver extensions, and contract-year math.

## Stack

ASP.NET Core (.NET 8) · EF Core (Npgsql / PostgreSQL) · SignalR · Auth0 (JWT) · AutoMapper · xUnit + Moq

## Architecture

| Piece | Role |
|-------|------|
| `FreeAgencyController` | auction endpoints — page load, lots, bids, nominate, win |
| `DashboardController` | cap management, trades, tags, buyouts, holdouts |
| `ConfidenceController` / `OverUnderController` | seasonal prediction games |
| `WinProcessorBackgroundService` | `Channel<BidDTO>` consumer → MFL sync |
| `MflService` | read/write integration with the MFL API |

External services: MFL, Auth0, Stream (chat), SportsData.io (NFL win totals).

## Configuration

Secrets are loaded from environment / config — `appsettings.Development.example.json` shows the expected shape. Runs as a container against a PostgreSQL database.

Part of a multi-repo system alongside a React frontend and a GroupMe bot / cap API.
