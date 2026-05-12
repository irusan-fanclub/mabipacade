# Test fixtures

These pcap files are loaded by E2E tests but NOT committed to git
(see top-level `.gitignore`). To run the E2E suite locally:

1. Copy one pcap from `D:/Projects/mabi_stage4_boss_notifier/publish/logs/`
   to `tests/Mabipacade.Core.Tests/fixtures/known_good.pcap`
2. Remove `[Fact(Skip = "Requires fixture")]` attribute on E2E tests
3. Run `dotnet test`

The CI build runs without fixtures (tests are skipped); local dev catches
regressions when running with fixtures present.
