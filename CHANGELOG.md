# Changelog

## [v7.1.2](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.1.2) (2025-03-17)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.1.2-alpha.0...v7.1.2)

**Closed issues:**

- RabbitMQ client for .net ignores connection string [\#1807](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1807)

**Fixed bugs:**

- `ObjectDisposedException` persists [\#1802](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1802)

**Merged pull requests:**

- Skip flaky test [\#1810](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1810) ([lukebakken](https://github.com/lukebakken))
- Address `ObjectDisposedException` [\#1809](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1809) ([lukebakken](https://github.com/lukebakken))
- Serialize sequence number as `long` [\#1806](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1806) ([lukebakken](https://github.com/lukebakken))

## [v7.1.1](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.1.1) (2025-02-26)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.1.0...v7.1.1)

**Implemented enhancements:**

- Port `IntAllocator` from `rabbitmq-java-client` [\#1786](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1786)

**Fixed bugs:**

- Intermittent deadlock when closing a channel using CloseAsync in 7.x [\#1751](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1751)
- Bug in `IntervalList` causes leak in locks. [\#1784](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1784)

**Merged pull requests:**

- Fix handling when rate limit lease can't be acquired. [\#1794](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1794) ([lukebakken](https://github.com/lukebakken))
- Demonstrate handling of `Channel.Close` [\#1791](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1791) ([lukebakken](https://github.com/lukebakken))
- Port `IntAllocator` from `rabbitmq-java-client` [\#1790](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1790) ([lukebakken](https://github.com/lukebakken))
- Fix bug in `IntervalList` [\#1785](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1785) ([lukebakken](https://github.com/lukebakken))

## [v7.1.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.1.0) (2025-02-19)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0...v7.1.0)

**Fixed bugs:**

- Intermittent deadlock when closing a channel using CloseAsync in 7.x [\#1751](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1751)
- `ObjectDisposedException` when connection is closed from the server [\#1760](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1760)
- Disposing Connection after closing it with timeout causes deadlock [\#1759](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1759)
- `ObjectDisposedException` when connection is closed from the server [\#1760](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1760)
- Disposing Connection after closing it with timeout causes deadlock [\#1759](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1759)
- Intermittent deadlock when closing a channel using CloseAsync in 7.x [\#1751](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1751)
- Connection Recovery is triggered without the old connection being closed [\#1767](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1767)
- Cancelling `ModelSendAsync` can close the connection when it shouldn't [\#1750](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1750)
- `ObjectDisposedException` when channel is closed by RabbitMQ due to a channel exception [\#1749](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1749)

**Merged pull requests:**

- Fix rare deadlock, second try [\#1782](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1782) ([lukebakken](https://github.com/lukebakken))
- Fix \#1777 [\#1781](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1781) ([lukebakken](https://github.com/lukebakken))
- Create cancellation token from `timeout` [\#1775](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1775) ([lukebakken](https://github.com/lukebakken))
- Allow setting heartbeat timeout to `TimeSpan.Zero` [\#1773](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1773) ([lukebakken](https://github.com/lukebakken))
- Track down `ObjectDisposedExceptions` [\#1772](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1772) ([lukebakken](https://github.com/lukebakken))
- Fix very rare deadlock [\#1771](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1771) ([lukebakken](https://github.com/lukebakken))
- Fix typos [\#1770](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1770) ([sungam3r](https://github.com/sungam3r))
- Remove whitespaces from csproj files [\#1768](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1768) ([sungam3r](https://github.com/sungam3r))
- Misc items [\#1766](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1766) ([lukebakken](https://github.com/lukebakken))
- Ensure broker-originated channel closure completes [\#1764](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1764) ([lukebakken](https://github.com/lukebakken))
- Update copyright year to 2025 [\#1755](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1755) ([lukebakken](https://github.com/lukebakken))
- Handle `OperationCanceledException` in RPC continuations [\#1753](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1753) ([lukebakken](https://github.com/lukebakken))
- Update NuGet packages [\#1748](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1748) ([lukebakken](https://github.com/lukebakken))
- Set TestTfmsInParallel to false for Integration tests [\#1745](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1745) ([lukebakken](https://github.com/lukebakken))
- Toxiproxy manager change [\#1744](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1744) ([lukebakken](https://github.com/lukebakken))
- Address flaky integration tests [\#1742](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1742) ([lukebakken](https://github.com/lukebakken))
- Simplify preprocessor directives [\#1741](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1741) ([danielmarbach](https://github.com/danielmarbach))
- chore: bump regular System.Text.RegularExpressions due to a known CVE in earlier versions [\#1735](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1735) ([aygalinc](https://github.com/aygalinc))
- More updates for the current OTel \(OpenTelemetry\) conventions [\#1717](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1717) ([iinuwa](https://github.com/iinuwa))

**Closed issues:**

- Throw exception during CreateConnectionAsync in case of wrong credentials [\#1777](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1777)
- Exception when disabling heartbeat with v7 [\#1756](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1756)
- Throw exception during CreateConnectionAsync in case of wrong credentials [\#1777](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1777)
- Exception when disabling heartbeat with v7 [\#1756](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1756)
- `IChannel` thread safety [\#1722](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1722)

**Implemented enhancements:**

- Single Active consumer [\#1723](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1723)
- OpenTelemetry: Update messaging.operation span attribute to latest OTel Semantic Conventions [\#1715](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1715)
- 7.0 release checklist / misc items [\#1413](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1413)

## [v7.0.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0) (2024-11-05)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.14...v7.0.0)

**Merged pull requests:**

- 7.0.0 CHANGELOG [\#1719](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1719) ([lukebakken](https://github.com/lukebakken))
- Fix build warnings in API [\#1718](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1718) ([lukebakken](https://github.com/lukebakken))
- Change OTel attribute messaging.operation to messaging.operation.type [\#1716](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1716) ([iinuwa](https://github.com/iinuwa))
- Throw when lease not acquired. This can happen then the rate limiter doesn't allow queuing or is generally wrongly configured [\#1714](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1714) ([danielmarbach](https://github.com/danielmarbach))

**Merged pull requests:**

## [v7.0.0-rc.14](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.14) (2024-10-24)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.13...v7.0.0-rc.14)

**Implemented enhancements:**

- Make handling of publisher confirmations transparent to the user [\#1682](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1682)

**Merged pull requests:**

- Remove `ChannelOptions` internal class [\#1712](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1712) ([lukebakken](https://github.com/lukebakken))
- Xml doc updates for the rate limiting [\#1711](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1711) ([danielmarbach](https://github.com/danielmarbach))
- Only add `x-dotnet-pub-seq-no` when tracking enabled [\#1710](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1710) ([lukebakken](https://github.com/lukebakken))
- Safeguarding against duplicate sequence numbers [\#1709](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1709) ([danielmarbach](https://github.com/danielmarbach))

## [v7.0.0-rc.13](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.13) (2024-10-22)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.12...v7.0.0-rc.13)

**Merged pull requests:**

- Address some more TODOs [\#1708](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1708) ([lukebakken](https://github.com/lukebakken))
- Upgrade NET6.0 to NET8.0 since NET6.0 is soon EOL [\#1707](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1707) ([danielmarbach](https://github.com/danielmarbach))
- Leverage `System.Threading.RateLimiting` [\#1706](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1706) ([lukebakken](https://github.com/lukebakken))
- Enforce maximum outstanding publisher confirms, if set [\#1703](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1703) ([lukebakken](https://github.com/lukebakken))
- Isolate publisher confirmation code [\#1702](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1702) ([lukebakken](https://github.com/lukebakken))
- Integrate `Channel` into `ChannelBase` [\#1700](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1700) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-rc.12](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.12) (2024-10-08)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.11...v7.0.0-rc.12)

**Implemented enhancements:**

- Allow DateTime for Headers in IBasicProperties [\#1691](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1691)
- Polyfill language gaps [\#1688](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1688)
- DispatchConsumerConcurrency might be misplaced on the connection factory [\#1668](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1668)
- Consider using `AsyncManualResetEvent` when handling flow state [\#1644](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1644)

**Fixed bugs:**

- Intermittent flakiness of v7.0 RC [\#1676](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1676)

**Merged pull requests:**

- Fix exception when refreshing oauth2 token [\#1690](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1690) ([BreakingBread0](https://github.com/BreakingBread0))
- Track publisher confirmations automatically [\#1687](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1687) ([lukebakken](https://github.com/lukebakken))
- Move code to appropriate directories that match namespace structure [\#1685](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1685) ([lukebakken](https://github.com/lukebakken))
- AsyncDisposable [\#1684](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1684) ([danielmarbach](https://github.com/danielmarbach))
- Event args cancellation [\#1683](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1683) ([danielmarbach](https://github.com/danielmarbach))
- Async flow control [\#1681](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1681) ([danielmarbach](https://github.com/danielmarbach))
- Make channel events async [\#1680](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1680) ([danielmarbach](https://github.com/danielmarbach))
- Make session events async [\#1679](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1679) ([danielmarbach](https://github.com/danielmarbach))
- Use unique queue and exchange names [\#1678](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1678) ([lukebakken](https://github.com/lukebakken))
- Make connection events async [\#1677](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1677) ([danielmarbach](https://github.com/danielmarbach))
- Sequence Number non-blocking [\#1675](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1675) ([danielmarbach](https://github.com/danielmarbach))
- Try to address some test flakes [\#1672](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1672) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-rc.11](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.11) (2024-09-12)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.10...v7.0.0-rc.11)

**Merged pull requests:**

- Follow-up to \#1669 - per-channel dispatch concurrency [\#1671](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1671) ([lukebakken](https://github.com/lukebakken))
- Minor cleanup in AutoRecovery classes [\#1670](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1670) ([danielmarbach](https://github.com/danielmarbach))
- Allow the dispatcher concurrency to be overriden per channel [\#1669](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1669) ([danielmarbach](https://github.com/danielmarbach))

## [v7.0.0-rc.10](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.10) (2024-09-10)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.9...v7.0.0-rc.10)

**Merged pull requests:**

- Require `IChannel` for `AsyncDefaultBasicConsumer` [\#1667](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1667) ([lukebakken](https://github.com/lukebakken))
- Add test to demonstrate `IChannel` thread-safety [\#1665](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1665) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-rc.9](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.9) (2024-09-06)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.8...v7.0.0-rc.9)

**Closed issues:**

- Re-review use of `Task` vs `ValueTask` in API [\#1645](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1645)

**Merged pull requests:**

- Clean up `IChannelExtensions` [\#1664](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1664) ([lukebakken](https://github.com/lukebakken))
- Fix `NextPublishSeqNo` when retrieved concurrently [\#1662](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1662) ([lukebakken](https://github.com/lukebakken))
- Finish up version 7 release [\#1661](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1661) ([lukebakken](https://github.com/lukebakken))
- Added ability to use Issuer to receive Token Endpoint for the OAuth2ClientBuilder [\#1656](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1656) ([Lyphion](https://github.com/Lyphion))
- Consistently use `Task` or `ValueTask` in APIs [\#1646](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1646) ([lukebakken](https://github.com/lukebakken))

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.8...HEAD)

**Closed issues:**

- Re-review use of `Task` vs `ValueTask` in API [\#1645](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1645)

**Merged pull requests:**

- Added ability to use Issuer to receive Token Endpoint for the OAuth2ClientBuilder [\#1656](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1656) ([Lyphion](https://github.com/Lyphion))
- Consistently use `Task` or `ValueTask` in APIs [\#1646](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1646) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-rc.8](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.8) (2024-08-06)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.7...v7.0.0-rc.8)

**Fixed bugs:**

- Automatic recovery fails if the channel is disposed [\#1647](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1647)
- Timer leak and potential JWT/OAuth2 expiry in TimerBasedCredentialRefresher \[OAuth2\] [\#1639](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1639)

**Merged pull requests:**

- Refactor credential refresh [\#1649](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1649) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-rc.7](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.7) (2024-07-31)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.6...v7.0.0-rc.7)

**Merged pull requests:**

- Fix object disposed exception during channel Recovery [\#1648](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1648) ([lukebakken](https://github.com/lukebakken))
- Support cancellation on the flow control block [\#1643](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1643) ([danielmarbach](https://github.com/danielmarbach))
- Align Reject with Ack/Nack [\#1642](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1642) ([danielmarbach](https://github.com/danielmarbach))
- Add cancellation token overload to channel extensions [\#1641](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1641) ([danielmarbach](https://github.com/danielmarbach))
- use async consumer only [\#1638](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1638) ([bollhals](https://github.com/bollhals))

## [v7.0.0-rc.6](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.6) (2024-07-22)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.5...v7.0.0-rc.6)

**Implemented enhancements:**

- Nullable Reference Types [\#1596](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1596)

**Fixed bugs:**

- NullReferenceException when setting null Uri into ConnectionFactory [\#1622](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1622)
- RabbitMQActivitySource.Deliver cannot be used by types that implement Consumer [\#1621](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1621)

**Merged pull requests:**

- simplify IncomingCommand.ReturnBuffers handling [\#1636](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1636) ([bollhals](https://github.com/bollhals))
- Move `Deliver` OTEL activity to consumer dispatchers [\#1633](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1633) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-rc.5](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.5) (2024-07-09)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.4...v7.0.0-rc.5)

**Implemented enhancements:**

- Testability of HandleBasicDeliver in IAsyncBasicConsumer [\#1630](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1630)

**Fixed bugs:**

- Connection recovery issue when docker container stopped / started. [\#1623](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1623)

**Closed issues:**

- Back-port \#1616 to 6.x [\#1617](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1617)

**Merged pull requests:**

- Replace ReadOnlyBasicProperties with IReadOnlyBasicProperties [\#1631](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1631) ([Tornhoof](https://github.com/Tornhoof))
- make IncomingCommand a class and simplify code around it [\#1628](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1628) ([bollhals](https://github.com/bollhals))
- Add NRT for the whole client assembly [\#1626](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1626) ([bollhals](https://github.com/bollhals))

## [v7.0.0-rc.4](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.4) (2024-07-02)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.3...v7.0.0-rc.4)

**Closed issues:**

- "Serialized to wrong size" exception when call `BasicPublish` [\#1620](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1620)

**Merged pull requests:**

- Fix issue when recovery takes longer than recovery interval [\#1624](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1624) ([lukebakken](https://github.com/lukebakken))
- Avoid boxing in IEndpointResolverExtensions.cs [\#1619](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1619) ([lechu445](https://github.com/lechu445))

## [v7.0.0-rc.3](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.3) (2024-06-27)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.2...v7.0.0-rc.3)

**Implemented enhancements:**

- Expose ConnectionFactory.DispatchConsumersAsync as a ReadOnly property on IConnection [\#1610](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1610)

**Merged pull requests:**

- Ensure that arguments passed to recorded entities are copied. [\#1616](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1616) ([lukebakken](https://github.com/lukebakken))
- Add `DispatchConsumersAsyncEnabled` property on `IConnection` \(\#1611\) [\#1615](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1615) ([lukebakken](https://github.com/lukebakken))
- change InboundFrame to a class [\#1613](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1613) ([bollhals](https://github.com/bollhals))
- Make `BasicProperties` a class [\#1609](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1609) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-rc.2](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.2) (2024-06-21)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-rc.1...v7.0.0-rc.2)

**Fixed bugs:**

- Issues with 7.0rc1 IAsyncBasicConsumer's method  [\#1601](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1601)
- \[OTel\] Activity is not produced for BasicConsumeAsync on .NET Framework [\#1533](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1533)

**Closed issues:**

- "Ship" public API [\#1570](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1570)

**Merged pull requests:**

- docs: update badge [\#1608](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1608) ([WeihanLi](https://github.com/WeihanLi))
- Make `ReadonlyBasicProperties` a class. [\#1607](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1607) ([lukebakken](https://github.com/lukebakken))
- remove CancellationTokenSource from DispatcherChannelBase [\#1606](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1606) ([bollhals](https://github.com/bollhals))
- fix some quick wins [\#1603](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1603) ([bollhals](https://github.com/bollhals))
- Remove in from async methods [\#1602](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1602) ([Tornhoof](https://github.com/Tornhoof))
- nuget CPM support [\#1599](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1599) ([WeihanLi](https://github.com/WeihanLi))
- Update CHANGELOG [\#1598](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1598) ([lukebakken](https://github.com/lukebakken))
- Use default value for `RabbitMQActivitySource.UseRoutingKeyAsOperationName` [\#1595](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1595) ([lukebakken](https://github.com/lukebakken))
- downgrade dependencies [\#1594](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1594) ([WeihanLi](https://github.com/WeihanLi))
## [v7.0.0-rc.1](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-rc.1) (2024-06-04)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-alpha.6...v7.0.0-rc.1)

**Implemented enhancements:**

- Ensure RabbitMQ.Client.OpenTelemetry is published to NuGet correctly [\#1591](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1591)
- Review TaskExtensions [\#1425](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1425)
- Support cancellation [\#1420](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1420)

**Fixed bugs:**

- Calling channel.CloseAsync from within AsyncEventingBasicConsumer handler causes deadlock [\#1567](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1567)
- MaxMessageSize actually limits frame size [\#1356](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1356)
- Calling Dispose on AutorecoveringModel that has already shutdown throws NullRefException [\#825](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/825)
- Opening a channel inside a consumer interface callback times out waiting for continuation [\#650](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/650)

**Closed issues:**

- Incorrect comments in some public member documentation [\#1109](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1109)
- CallbackException not raised for AsyncEventingBasicConsumer [\#1038](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1038)
- Unhelpful exception message when clientProvidedName is too long [\#980](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/980)
- Pool workers to handle incoming deliveries, ACKs etc. [\#906](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/906)
- AutorecoveringConnection prevents AppDomain.Unload\(\) of enclosing AppDomain [\#826](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/826)
- NullReferenceException when writing a frame on a connection being closed by the server [\#822](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/822)
- Log what TLS version\(s\) is enabled on the system [\#765](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/765)

**Merged pull requests:**

- Ship PublicAPI [\#1593](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1593) ([lukebakken](https://github.com/lukebakken))
- Ensure OpenTelemetry project is set up like OAuth2 [\#1592](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1592) ([lukebakken](https://github.com/lukebakken))
- Add test to demonstrate that \#825 is fixed [\#1590](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1590) ([lukebakken](https://github.com/lukebakken))
- Update copyright message [\#1589](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1589) ([lukebakken](https://github.com/lukebakken))
- Change test to match code provided by @neilgreatorex [\#1588](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1588) ([lukebakken](https://github.com/lukebakken))
- Add test demonstrating that \#1573 is fixed [\#1587](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1587) ([lukebakken](https://github.com/lukebakken))
- Demonstrate that \#1038 is fixed [\#1585](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1585) ([lukebakken](https://github.com/lukebakken))
- Handle AppDomain unload [\#1583](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1583) ([lukebakken](https://github.com/lukebakken))
- Use ConcurrentDictionary [\#1580](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1580) ([lukebakken](https://github.com/lukebakken))
- Fix two flaky tests [\#1579](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1579) ([lukebakken](https://github.com/lukebakken))
- Add test that creates `IChannel` within async consumer callback [\#1578](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1578) ([lukebakken](https://github.com/lukebakken))
- Remove two unnecessary `.Cast<>()` usages [\#1577](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1577) ([lukebakken](https://github.com/lukebakken))
- Truncate long client provided names [\#1576](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1576) ([lukebakken](https://github.com/lukebakken))
- Misc changes from lukebakken/amqp-string [\#1575](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1575) ([lukebakken](https://github.com/lukebakken))
- Fix ShutdownInitiator in `CloseAsync` [\#1574](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1574) ([lukebakken](https://github.com/lukebakken))
- Can't close channel from consumer dispatcher [\#1568](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1568) ([lukebakken](https://github.com/lukebakken))
- Adding proper OpenTelemetry integration via. registration helpers and better context propagation [\#1528](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1528) ([stebet](https://github.com/stebet))

## [v7.0.0-alpha.6](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-alpha.6) (2024-05-16)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-alpha.5...v7.0.0-alpha.6)

**Implemented enhancements:**

- Using the wrong consumer dispatcher type should throw an exception [\#1408](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1408)

**Fixed bugs:**

- 7.0.0-alpha.2 - Unexpected Exception: Unable to read data from the transport connection: Connection reset by peer [\#1464](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1464)
- Do not create exceptions just for logging [\#1440](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1440)
- Only throw `RabbitMQ.Client`-specific exceptions. [\#1439](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1439)

**Merged pull requests:**

- Enforce max message size with mutiple content frames [\#1566](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1566) ([lukebakken](https://github.com/lukebakken))
- Various editor suggestions [\#1563](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1563) ([lukebakken](https://github.com/lukebakken))
- Misc changes [\#1560](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1560) ([lukebakken](https://github.com/lukebakken))
- Enable `rabbitmq-client` event logging when tests are verbose [\#1559](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1559) ([lukebakken](https://github.com/lukebakken))
- More `CancellationToken` todos [\#1555](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1555) ([lukebakken](https://github.com/lukebakken))
- Fix `TestThatDeletedQueueBindingsDontReappearOnRecovery` [\#1554](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1554) ([lukebakken](https://github.com/lukebakken))
- `TestPublishRpcRightAfterReconnect` improvements [\#1553](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1553) ([lukebakken](https://github.com/lukebakken))
- Better exception message when a continuation times out [\#1552](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1552) ([lukebakken](https://github.com/lukebakken))
- Separate out connection recovery tests [\#1549](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1549) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-alpha.5](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-alpha.5) (2024-04-22)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-alpha.4...v7.0.0-alpha.5)

**Implemented enhancements:**

- add suport for wasm [\#1518](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1518)
- Optimise dependencies needed for v7 release [\#1480](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1480)
- Add net 6.0 so that conditional packages can be defined [\#1479](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1479)
- ClientArrayPool tracking is already supported by ArrayPool [\#1421](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1421)

**Closed issues:**

- Remove sync API [\#1472](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1472)

**Merged pull requests:**

- Replace `lock` with `SemaphoreSlim` [\#1539](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1539) ([lukebakken](https://github.com/lukebakken))
- Fix the flaky `TestConsumerRecoveryOnClientNamedQueueWithOneRecovery` test [\#1538](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1538) ([lukebakken](https://github.com/lukebakken))
- Add `CancellationToken` to `Async` members of `IChannel` [\#1535](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1535) ([lukebakken](https://github.com/lukebakken))
- Add build step to check for `inet_error` [\#1525](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1525) ([lukebakken](https://github.com/lukebakken))
- Add a test that closes a connection with Toxiproxy [\#1522](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1522) ([lukebakken](https://github.com/lukebakken))
- Fix small typos in ConnectionFactory [\#1521](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1521) ([markbosch](https://github.com/markbosch))
- Remove a lot of unused code [\#1517](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1517) ([lukebakken](https://github.com/lukebakken))
- Remove TODO [\#1516](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1516) ([lukebakken](https://github.com/lukebakken))
- Remove `ClientArrayPool` [\#1515](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1515) ([lukebakken](https://github.com/lukebakken))
- \#1480 Use conditional packages for v7 release [\#1481](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1481) ([thompson-tomo](https://github.com/thompson-tomo))

## [v7.0.0-alpha.4](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-alpha.4) (2024-03-05)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-alpha.3...v7.0.0-alpha.4)

**Merged pull requests:**

- Make handling of server-originated methods async [\#1508](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1508) ([lukebakken](https://github.com/lukebakken))
- Continue simplifying code [\#1507](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1507) ([lukebakken](https://github.com/lukebakken))
- Continue removing sync API [\#1506](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1506) ([lukebakken](https://github.com/lukebakken))
- Use RabbitMQ 3.13 on Windows GHA [\#1505](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1505) ([lukebakken](https://github.com/lukebakken))
- Remove more synchronous code [\#1504](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1504) ([lukebakken](https://github.com/lukebakken))

## [v7.0.0-alpha.3](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-alpha.3) (2024-02-20)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-alpha.2...v7.0.0-alpha.3)

**Implemented enhancements:**

- Consider re-trying opening connections in a specific scenario [\#1448](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1448)
- Trimming and AOT compatibility of this library [\#1410](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1410)
- Reduce allocations by using pooled memory and recycling memory streams [\#694](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/694)
- Consider adding static analysis [\#444](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/444)

**Fixed bugs:**

- HandleMainLoopException puts exception StackTrace in the EventSource Message [\#1493](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1493)

**Closed issues:**

- Async API - socket read timeout has no effect [\#1492](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1492)
- Deadlock when closing a channel in the Received Event [\#1382](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1382)
- Enable long-running tests on a cron schedule. [\#1157](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1157)
- Heap size grows when publishing a very large batch of messages [\#1106](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1106)
- IModel.IsClosed set to false after dispose [\#1086](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1086)
- WaitForConfirmsOrDie won't return if DeliveryTag on the client is diverged from the server [\#1043](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1043)
- Bug in topology-auto-recovery functionality [\#1035](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1035)
- Model.Close does not await CloseAsync [\#1011](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1011)
- Discussion: Supported frameworks [\#867](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/867)
- Evaluating how to support tracing and OpenTelemetry [\#776](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/776)

**Merged pull requests:**

- Remove more synchronous code. [\#1501](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1501) ([lukebakken](https://github.com/lukebakken))
- Update codeql.yml [\#1499](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1499) ([lukebakken](https://github.com/lukebakken))
- Address lack of Socket read timeout for async reads [\#1497](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1497) ([lukebakken](https://github.com/lukebakken))
- Port \#1494 to `main` [\#1495](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1495) ([lukebakken](https://github.com/lukebakken))
- Re-organize test projects [\#1491](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1491) ([lukebakken](https://github.com/lukebakken))
- InternalsVisibleTo enhancements [\#1488](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1488) ([WeihanLi](https://github.com/WeihanLi))
- Remove synchronous API [\#1473](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1473) ([lukebakken](https://github.com/lukebakken))
- Add more use of CancellationToken in Async methods. [\#1468](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1468) ([lukebakken](https://github.com/lukebakken))
- Add test code for issue \#1464 [\#1466](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1466) ([lukebakken](https://github.com/lukebakken))
- Add test to prove bindings are restored by topology recovery [\#1460](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1460) ([lukebakken](https://github.com/lukebakken))
- Ensure delivery tag is decremented for client-side exception [\#1453](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1453) ([lukebakken](https://github.com/lukebakken))
- Enable long running tests [\#1451](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1451) ([lukebakken](https://github.com/lukebakken))
- Add test that demonstrates the current behavior of a recovered channe… [\#1450](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1450) ([lukebakken](https://github.com/lukebakken))
- Retry more connections in test suite [\#1449](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1449) ([lukebakken](https://github.com/lukebakken))
- Use `Microsoft.CodeAnalysis.PublicApiAnalyzers` [\#1447](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1447) ([lukebakken](https://github.com/lukebakken))
- Trimming and AOT compatibility [\#1411](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1411) ([eerhardt](https://github.com/eerhardt))
- Add OpenTelemetry support via ActivitySource [\#1261](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1261) ([stebet](https://github.com/stebet))

## [v7.0.0-alpha.2](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-alpha.2) (2023-12-14)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.8.1...v7.0.0-alpha.2)

**Fixed bugs:**

- 7.0: Stack overflow "ExchangeDeclareAsync" [\#1444](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1444)

**Closed issues:**

- Port \#1438 to main [\#1441](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1441)

**Merged pull requests:**

- Port \#1434 to `main` [\#1442](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1442) ([lukebakken](https://github.com/lukebakken))
- Fix \#1429 [\#1431](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1431) ([lukebakken](https://github.com/lukebakken))
- Add cancellation to initial socket connection [\#1428](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1428) ([lukebakken](https://github.com/lukebakken))
- Ensure that the underlying timer for `Task.Delay` is canceled. [\#1426](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1426) ([lukebakken](https://github.com/lukebakken))

## [v6.8.1](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.8.1) (2023-12-11)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.8.1-rc.1...v6.8.1)

## [v6.8.1-rc.1](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.8.1-rc.1) (2023-12-11)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.8.0...v6.8.1-rc.1)

**Fixed bugs:**

- When using assembly binding redirect a connection error is shown [\#1434](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1434)
- Queue Fails to Delete with Orphaned Binding [\#1376](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1376)

## [v6.8.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.8.0) (2023-12-05)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.8.0-rc.1...v6.8.0)

**Fixed bugs:**

- Opening multiple connections from a `ConnectionFactory` with `CredentialsRefresher` makes the second connection fail [\#1429](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1429)

## [v6.8.0-rc.1](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.8.0-rc.1) (2023-12-01)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-alpha.1...v6.8.0-rc.1)

**Closed issues:**

- Should IModelExensions be named IModelExtensions \(with a "t"\)? [\#1430](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1430)
- Review async RPC continuations, await, and ConfigureAwait [\#1427](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1427)

## [v7.0.0-alpha.1](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-alpha.1) (2023-11-20)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.7.0...v7.0.0-alpha.1)

**Closed issues:**

- Xunit parallel test execution [\#1412](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1412)
- Port "Implement Full Async Channel" PR to `main` [\#1308](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1308)
- ConnectionRecovery recovers models before consumers [\#1076](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1076)
- connection topology recover can miss restore consumers [\#1047](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1047)
- Proposal: Full async channel interface \(IModel\) [\#970](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/970)
- AMQP 0-9-1 Channel \(IModel\) API with async methods [\#843](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/843)

**Merged pull requests:**

- Make TcpClientAdapter public [\#1417](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1417) ([lukebakken](https://github.com/lukebakken))
- Removed ReceiveBufferSize and SendBufferSize to improve message rates [\#1415](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1415) ([lukebakken](https://github.com/lukebakken))
- Clarify Unix Time units in AmqpTimestamp [\#1407](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1407) ([ngbrown](https://github.com/ngbrown))
- Implement asynchronous methods. [\#1347](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1347) ([lukebakken](https://github.com/lukebakken))

## [v6.7.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.7.0) (2023-11-16)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.7.0-rc.2...v6.7.0)

## [v6.7.0-rc.2](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.7.0-rc.2) (2023-11-16)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.7.0-rc.1...v6.7.0-rc.2)

## [v6.7.0-rc.1](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.7.0-rc.1) (2023-11-15)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v7.0.0-alpha.0...v6.7.0-rc.1)

**Fixed bugs:**

- 6.5.0 can cause application freeze due to TaskScheduler change [\#1357](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1357)

**Closed issues:**

- Consistent naming for methods that return Tasks [\#1296](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1296)
- Take advantage of immutable collections [\#1171](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1171)

## [v7.0.0-alpha.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v7.0.0-alpha.0) (2023-10-16)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.6.0...v7.0.0-alpha.0)

**Implemented enhancements:**

- Remove the use of Moq [\#1369](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1369)
- remove APIGen project [\#924](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/924) ([bollhals](https://github.com/bollhals))
- Ability to do concurrent dispatches both on the async as well as the sync consumer [\#866](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/866) ([danielmarbach](https://github.com/danielmarbach))
- simplify missed heartbeat recognition [\#854](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/854) ([bollhals](https://github.com/bollhals))
- eliminate allocations from InboundFrame [\#848](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/848) ([bollhals](https://github.com/bollhals))

**Fixed bugs:**

- If CreateConnection is called on a thread with a non-default TaskScheduler it will attempt to start the MainLoop on that scheduler. [\#1355](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1355)
- Fix flaky connection recovery tests. [\#1148](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1148) ([lukebakken](https://github.com/lukebakken))
- Typo in Extension class name [\#922](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/922) ([JeremyMahieu](https://github.com/JeremyMahieu))

**Merged pull requests:**

- Update GHA for NuGet publishing [\#1406](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1406) ([lukebakken](https://github.com/lukebakken))
- Update package dependencies [\#1403](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1403) ([lukebakken](https://github.com/lukebakken))
- Ensure tests that interact with RabbitMQ do not run in parallel [\#1402](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1402) ([lukebakken](https://github.com/lukebakken))
- Add certs and rabbitmq.conf to enable TLS testing on GHA [\#1398](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1398) ([lukebakken](https://github.com/lukebakken))
- Add workflow to test OAuth2 [\#1397](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1397) ([lukebakken](https://github.com/lukebakken))
- Use latest CI resources on Windows [\#1394](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1394) ([lukebakken](https://github.com/lukebakken))
- Remove use of Moq [\#1393](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1393) ([lukebakken](https://github.com/lukebakken))
- Ensure that the order of arguments is `expected`, `actual`, take two! [\#1386](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1386) ([lukebakken](https://github.com/lukebakken))
- Ensure that the order of arguments is `expected`, `actual` [\#1385](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1385) ([lukebakken](https://github.com/lukebakken))
- Update API documentation of MaxMessageSize [\#1381](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1381) ([MarcialRosales](https://github.com/MarcialRosales))
- Fix \#1378 [\#1380](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1380) ([MarcialRosales](https://github.com/MarcialRosales))
- fix typo [\#1377](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1377) ([WeihanLi](https://github.com/WeihanLi))
- Update package references [\#1372](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1372) ([lukebakken](https://github.com/lukebakken))
- Misc Windows CI updates [\#1366](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1366) ([lukebakken](https://github.com/lukebakken))
- Support OAuth2 authentication [\#1346](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1346) ([MarcialRosales](https://github.com/MarcialRosales))
- Implement QueueDeclareAsync [\#1345](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1345) ([lukebakken](https://github.com/lukebakken))
- Build using traversal project [\#1344](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1344) ([lukebakken](https://github.com/lukebakken))
- Follow-up to PR \#1332 [\#1340](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1340) ([lukebakken](https://github.com/lukebakken))
- Add a missing constant for NO\_ROUTE [\#1332](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1332) ([michaelklishin](https://github.com/michaelklishin))
- Updates from the 6.x branch [\#1328](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1328) ([lukebakken](https://github.com/lukebakken))
- Misc updates [\#1326](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1326) ([lukebakken](https://github.com/lukebakken))
- Fix consumer recovery with server-named queues [\#1325](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1325) ([lukebakken](https://github.com/lukebakken))
- Port \#1317 to main [\#1323](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1323) ([lukebakken](https://github.com/lukebakken))
- Ignore global.json [\#1319](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1319) ([michaelklishin](https://github.com/michaelklishin))
- Add custom filtering and exception handling to topology recovery [\#1312](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1312) ([rosca-sabina](https://github.com/rosca-sabina))
- Adding fully asynchronous versions of connect and publish. [\#1311](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1311) ([stebet](https://github.com/stebet))
- Part 2 of \#1308 - Port Interlocked.cs from \#982 [\#1310](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1310) ([lukebakken](https://github.com/lukebakken))
- Part 1 of \#1308 - renaming IModel and related names [\#1309](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1309) ([lukebakken](https://github.com/lukebakken))
- Fix build warning [\#1307](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1307) ([lukebakken](https://github.com/lukebakken))
- Port \#1304 - Add event for recovering consumer [\#1305](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1305) ([Zerpet](https://github.com/Zerpet))
- Update package versions [\#1295](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1295) ([lukebakken](https://github.com/lukebakken))
- Remove unused StringExtension.cs [\#1290](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1290) ([Pliner](https://github.com/Pliner))
- Address some test flakes [\#1288](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1288) ([lukebakken](https://github.com/lukebakken))
- Bump testing versions on Windows [\#1285](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1285) ([lukebakken](https://github.com/lukebakken))
- Removed an unnecessary comment [\#1276](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1276) ([3baaady07](https://github.com/3baaady07))
- fix: add unsigned short support in table deserialisation [\#1270](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1270) ([gytsen](https://github.com/gytsen))
- Minimal System.IO.Pipelines integration to prepare for full-async work [\#1264](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1264) ([stebet](https://github.com/stebet))
- Add exception logging on WriteLoop [\#1262](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1262) ([Turnerj](https://github.com/Turnerj))
- Fixing header parsing bug [\#1260](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1260) ([stebet](https://github.com/stebet))
- CI updates [\#1256](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1256) ([lukebakken](https://github.com/lukebakken))
- change ref to in and use Unsafe.AsRef [\#1247](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1247) ([bollhals](https://github.com/bollhals))
- Merge pull request \#1224 from rabbitmq/rabbitmq-dotnet-client-1223-6.x [\#1226](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1226) ([lukebakken](https://github.com/lukebakken))
- Add ability to specify maximum message size [\#1218](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1218) ([lukebakken](https://github.com/lukebakken))
- Misc updates [\#1215](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1215) ([lukebakken](https://github.com/lukebakken))
- thread-safe Random generation [\#1207](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1207) ([amiru3f](https://github.com/amiru3f))
- Use new Task.WaitAsync\(\) to cancel an await call [\#1206](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1206) ([ArminShoeibi](https://github.com/ArminShoeibi))
- Fix perpetual auto-recovery problem [\#1204](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1204) ([mot256](https://github.com/mot256))
- \[REVERTED\] Use file-scoped namesapces in RabbitMQ.Client project [\#1202](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1202) ([ArminShoeibi](https://github.com/ArminShoeibi))
- Add related headers to streams feature in Headers.cs [\#1201](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1201) ([ArminShoeibi](https://github.com/ArminShoeibi))
- Run "dotnet format" [\#1197](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1197) ([lukebakken](https://github.com/lukebakken))
- Reduce message body size to help fix flaky test [\#1193](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1193) ([lukebakken](https://github.com/lukebakken))
- Ensure target frameworks make sense for 7.0 [\#1189](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1189) ([lukebakken](https://github.com/lukebakken))
- Restore readonly [\#1188](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1188) ([lukebakken](https://github.com/lukebakken))
- Update AsyncDefaultBasicConsumer.cs [\#1186](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1186) ([lukebakken](https://github.com/lukebakken))
- `AutorecoveringModel.IsClosed` should return `true` if model is unusable [\#1179](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1179) ([onematchfox](https://github.com/onematchfox))
- Reference `RUNNING_TESTS.md` from `CONTRIBUTING.md` [\#1175](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1175) ([onematchfox](https://github.com/onematchfox))
- Add test to try and repro \#1168 [\#1173](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1173) ([lukebakken](https://github.com/lukebakken))
- Fix Concourse CI [\#1169](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1169) ([lukebakken](https://github.com/lukebakken))
- Port rabbitmq/rabbitmq-dotnet-client \#950 to main [\#1165](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1165) ([lukebakken](https://github.com/lukebakken))
- Add TimeOut to Abort call in dispose [\#1164](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1164) ([lukebakken](https://github.com/lukebakken))
- Port PR \#1145 to main, manually [\#1161](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1161) ([lukebakken](https://github.com/lukebakken))
- Do not setup dotnet on Win runner [\#1152](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1152) ([lukebakken](https://github.com/lukebakken))
- Try different docker image [\#1151](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1151) ([lukebakken](https://github.com/lukebakken))
- Add Windows GitHub build [\#1149](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1149) ([lukebakken](https://github.com/lukebakken))
- Merge pull request \#1141 from rabbitmq/lukebakken/fix-appveyor [\#1143](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1143) ([lukebakken](https://github.com/lukebakken))
- Clean-up the library's project file. [\#1124](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1124) ([teo-tsirpanis](https://github.com/teo-tsirpanis))
- simplify code around RabbitMQCtl [\#1111](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1111) ([bollhals](https://github.com/bollhals))
- introduce Connection Config class [\#1110](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1110) ([bollhals](https://github.com/bollhals))
- improve basic properties read / write [\#1100](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1100) ([bollhals](https://github.com/bollhals))
- Fix typo in comment for IConnection [\#1099](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1099) ([GitHubPang](https://github.com/GitHubPang))
- use ref instead of in for generic T + interface [\#1098](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1098) ([bollhals](https://github.com/bollhals))
- Fix minor typo [\#1097](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1097) ([GitHubPang](https://github.com/GitHubPang))
- introduce struct BasicProperties for basicPublish [\#1096](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1096) ([bollhals](https://github.com/bollhals))
- fix a typo in comments of WaitForConfirmsOrDieAsync [\#1095](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1095) ([ArminShoeibi](https://github.com/ArminShoeibi))
- Fix typo in RUNNING\_TESTS.md [\#1094](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1094) ([GitHubPang](https://github.com/GitHubPang))
- Remove unneeded comment [\#1092](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1092) ([GitHubPang](https://github.com/GitHubPang))
- Fix XML doc for AmqpTcpEndpoint [\#1091](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1091) ([GitHubPang](https://github.com/GitHubPang))
- Fix incorrect RemotePort [\#1090](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1090) ([GitHubPang](https://github.com/GitHubPang))
- Add CodeQL code security analysis workflow [\#1085](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1085) ([MirahImage](https://github.com/MirahImage))
- Fix comments in ConnectionFactory [\#1084](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1084) ([GitHubPang](https://github.com/GitHubPang))
- Don't record consumers if topology recovery is off [\#1082](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1082) ([Pliner](https://github.com/Pliner))
- Typos [\#1080](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1080) ([sungam3r](https://github.com/sungam3r))
- Recover models together with consumers [\#1077](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1077) ([bollhals](https://github.com/bollhals))
- add DeliveryModes convenience enum [\#1063](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1063) ([Jalalx](https://github.com/Jalalx))
- Fix misspelling and add missing words in comments [\#1053](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1053) ([GitHubPang](https://github.com/GitHubPang))
- Feature/recover topology if connection problem [\#1051](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1051) ([pierresetteskog](https://github.com/pierresetteskog))
- Resolve \#1039 Confirms Failure should use ShutdownInitiator.Library [\#1040](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1040) ([StevenBonePgh](https://github.com/StevenBonePgh))
- Fix typo in ConnectionFactory.cs [\#1034](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1034) ([GitHubPang](https://github.com/GitHubPang))
- Removing bounds checks when serializing commands/frames. [\#1030](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1030) ([stebet](https://github.com/stebet))
- split connection, enable nullable and remove SoftProtocolException [\#1029](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1029) ([bollhals](https://github.com/bollhals))
- Remove batch publishing as its optimizations are now performed for "regular" publishes/outgoing frames [\#1028](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1028) ([bollhals](https://github.com/bollhals))
- Performance counters \(metrics\) [\#1027](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1027) ([bollhals](https://github.com/bollhals))
- Workaround: Fixing analyzer build errors on VS2019 v16.9 [\#1026](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1026) ([stebet](https://github.com/stebet))
- Replacing nUnit with xUnit and parallelizing tests where possible. [\#1025](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1025) ([stebet](https://github.com/stebet))
- remove unused classes [\#1022](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1022) ([bollhals](https://github.com/bollhals))
- fix always throwing exception on session close [\#1020](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1020) ([bollhals](https://github.com/bollhals))
- remove socket.poll [\#1018](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1018) ([bollhals](https://github.com/bollhals))
- Code cleanups and performance optimizations. [\#1017](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1017) ([stebet](https://github.com/stebet))
- Framing benchmarks and optimizations [\#1016](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1016) ([stebet](https://github.com/stebet))
- Fix Semaphore Disposed Exception in AsyncConsumerWorkService [\#1015](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1015) ([ashneilson](https://github.com/ashneilson))
- \#1012 Fixed exception when consumer tag cannot be found in the consumers dictionary [\#1013](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1013) ([LamarLugli](https://github.com/LamarLugli))
- Drop deprecated members of framing [\#1010](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1010) ([bollhals](https://github.com/bollhals))
- Various cleanups of Connection / Model [\#1009](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1009) ([bollhals](https://github.com/bollhals))
- Reduce close / abort methods in public API [\#1008](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1008) ([bollhals](https://github.com/bollhals))
- Simplify recordings in AutorecoveringConnection [\#1007](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1007) ([bollhals](https://github.com/bollhals))
- Update EventingBasicConsumer.cs [\#1006](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1006) ([riddletd](https://github.com/riddletd))
- Simplify connection recovery [\#1004](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1004) ([bollhals](https://github.com/bollhals))
- add two test applications [\#1003](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1003) ([bollhals](https://github.com/bollhals))
- simplify CloseReason handling & improve AutorecoveringConnection eventing [\#1002](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1002) ([bollhals](https://github.com/bollhals))
- Remove Experimental from EventingBasicConsumer doc [\#1001](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1001) ([kjnilsson](https://github.com/kjnilsson))
- 8.0: implement WaitForConfirmsAsync [\#999](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/999) ([bollhals](https://github.com/bollhals))
- Consumer dispatcher improvements [\#997](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/997) ([bollhals](https://github.com/bollhals))
- reduce allocations [\#996](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/996) ([bollhals](https://github.com/bollhals))
- minor cleanup of \(unused / rarely used\) classes [\#992](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/992) ([bollhals](https://github.com/bollhals))
- 8.0: implement BasicPublishMemory [\#990](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/990) ([bollhals](https://github.com/bollhals))
- some more wire formatting improvements [\#989](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/989) ([bollhals](https://github.com/bollhals))
- 8.0: improve eventing [\#986](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/986) ([bollhals](https://github.com/bollhals))
- Speeding up shortstr/longstr \(de\)serialization. [\#985](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/985) ([stebet](https://github.com/stebet))
- Speeding up decimal \(de\)serialization. [\#984](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/984) ([stebet](https://github.com/stebet))
- Cleaning up and adding more benchmarks. [\#983](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/983) ([stebet](https://github.com/stebet))
- 7.0: update target framework from net461 to netcoreapp3.1 [\#971](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/971) ([bollhals](https://github.com/bollhals))
- Structify the Framing.Impl methods [\#962](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/962) ([bollhals](https://github.com/bollhals))
- Update IModel Documentation [\#958](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/958) ([JKamsker](https://github.com/JKamsker))
- Minor improvements to GitHub Actions CI build. [\#954](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/954) ([stebet](https://github.com/stebet))
- Replace Travis with GitHub Actions [\#952](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/952) ([asbjornu](https://github.com/asbjornu))
- Make sure OnCallbackException is executed for AsyncConsumers [\#946](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/946) ([BarShavit](https://github.com/BarShavit))
- Breaking public API change: fold IAutorecoveringConnection \[back\] into IConnection [\#943](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/943) ([michaelklishin](https://github.com/michaelklishin))
- Switch FxCop to PrivateAssets="All" [\#941](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/941) ([danielmarbach](https://github.com/danielmarbach))
- Use span directly instead of reader / writer for methods & properties [\#936](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/936) ([bollhals](https://github.com/bollhals))
- Expose events ConsumerTagChangeAfterRecovery and QueueNameChangeAfterRecovery [\#935](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/935) ([paulomf](https://github.com/paulomf))
- Remove IAsyncConnectionFactory [\#933](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/933) ([danielmarbach](https://github.com/danielmarbach))
- Add FxCop with only ConfigureAwait rule enabled for now [\#932](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/932) ([danielmarbach](https://github.com/danielmarbach))
- Code cleanups [\#931](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/931) ([stebet](https://github.com/stebet))
- Update dependencies to latest versions [\#929](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/929) ([lukebakken](https://github.com/lukebakken))
- change from Memory to ReadOnlyMemory [\#928](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/928) ([bollhals](https://github.com/bollhals))
- change AmqpVersion to struct [\#927](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/927) ([bollhals](https://github.com/bollhals))
- Command Id instead of class & method id [\#925](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/925) ([bollhals](https://github.com/bollhals))
- Running Tests docs: quotes for setting env vars in windows [\#921](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/921) ([JeremyMahieu](https://github.com/JeremyMahieu))
- reduce the amount of times we rent / return from arraypool [\#919](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/919) ([bollhals](https://github.com/bollhals))
- Switch to Mozilla Public License 2.0 \(MPL 2.0\) [\#916](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/916) ([dcorbacho](https://github.com/dcorbacho))
- simplify ack/nack handling [\#912](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/912) ([bollhals](https://github.com/bollhals))
- cache the frameHeaderBuffer & rentedArray [\#911](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/911) ([bollhals](https://github.com/bollhals))
- Reduce work allocations [\#910](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/910) ([bollhals](https://github.com/bollhals))
- More descriptive exception in WriteShortstr [\#908](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/908) ([tmasternak](https://github.com/tmasternak))
- Rename ProcessingConcurrency to ConsumerDispatchConcurrency [\#905](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/905) ([danielmarbach](https://github.com/danielmarbach))
- Getting rid of Command allocations [\#902](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/902) ([bollhals](https://github.com/bollhals))
- Pull ProcessingConcurrency into connection factory interface [\#899](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/899) ([danielmarbach](https://github.com/danielmarbach))
- Missing ConfigureAwait in TcpClientAdapter [\#897](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/897) ([danielmarbach](https://github.com/danielmarbach))
- Edit docs [\#896](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/896) ([JeremyMahieu](https://github.com/JeremyMahieu))
- 7.x: remove deprecated message publishing method overloads [\#895](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/895) ([danielmarbach](https://github.com/danielmarbach))
- Do DNS resolution before connection attempt [\#893](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/893) ([JeremyMahieu](https://github.com/JeremyMahieu))
- precompute sizes and and simplify BasicProperties presence [\#890](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/890) ([bollhals](https://github.com/bollhals))
- add size hint variable for PublishBatch creation [\#888](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/888) ([bollhals](https://github.com/bollhals))
- use cached empty BasicProperties when null [\#887](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/887) ([bollhals](https://github.com/bollhals))
- Refactor: extracted RabbitMQ node management functions from IntegrationFixture [\#884](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/884) ([pergh](https://github.com/pergh))
- Switch WorkPool of ConsumerWorkService to channels [\#882](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/882) ([danielmarbach](https://github.com/danielmarbach))
- Move BinaryTableValue to public section [\#880](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/880) ([BenBorn](https://github.com/BenBorn))
- fix issue 868 [\#878](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/878) ([bollhals](https://github.com/bollhals))
- Fix AppVeyor build [\#871](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/871) ([lukebakken](https://github.com/lukebakken))
- Prep for 6.1.1 [\#870](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/870) ([lukebakken](https://github.com/lukebakken))
- Implement BasicPublishBatch with ReadOnlyMemory [\#865](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/865) ([danielmarbach](https://github.com/danielmarbach))
- Report a Uri scheme of "amqps" in AmqpTcpEndpoint.ToString\(\) iff SslOption is enabled [\#864](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/864) ([ghost](https://github.com/ghost))
- Re-merge pull request \#855 from bollhals/remove.task.yield [\#863](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/863) ([lukebakken](https://github.com/lukebakken))
- Fix RabbitMQ version parsing [\#862](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/862) ([lukebakken](https://github.com/lukebakken))
- Unify on IModel [\#858](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/858) ([danielmarbach](https://github.com/danielmarbach))
- remove task yield [\#855](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/855) ([bollhals](https://github.com/bollhals))
- Delete AutoClose from SessionManager [\#852](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/852) ([Pliner](https://github.com/Pliner))
- Adding BinaryPrimitives support for NETSTANDARD targets [\#851](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/851) ([stebet](https://github.com/stebet))
- Fixing rethrown exceptions to properly preserve stackframes. [\#850](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/850) ([stebet](https://github.com/stebet))
- throw ArgumentOutOfRangeException when table key is too long \(\> 255\) [\#849](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/849) ([bollhals](https://github.com/bollhals))
- Deletion of unreachable code [\#847](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/847) ([Pliner](https://github.com/Pliner))
- Deletion of unused UseBackgroundThreadsForIO [\#846](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/846) ([Pliner](https://github.com/Pliner))
- simplify WriteShortstr [\#845](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/845) ([bollhals](https://github.com/bollhals))
- Frame optimizations [\#844](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/844) ([stebet](https://github.com/stebet))
- Minor improvements and optimizations [\#842](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/842) ([bollhals](https://github.com/bollhals))

## [v6.6.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.6.0) (2023-09-25)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.6.0-beta.0...v6.6.0)

## [v6.6.0-beta.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.6.0-beta.0) (2023-09-25)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.5.0...v6.6.0-beta.0)

**Implemented enhancements:**

- Allow update of RoutingKey during Nack [\#1333](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1333)
- Support credential refresh for OAuth 2/JWT authentication scenarios [\#956](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/956)

**Fixed bugs:**

- Configured `MaxMessageSize` is not honoured  [\#1378](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1378)
- ConnectionFactory Authmechanisms has incorrect sharing between instances [\#1370](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1370)
- Unknown reply code 312 [\#1331](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1331)

**Closed issues:**

- Invalid parameter name in IManagementClient.DeleteExchangeBindingAsync [\#1375](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1375)
- Closing connection in EventingBasicConsumer.Received event handler freezes execution of event handler and connection is never closed [\#1292](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1292)

## [v6.5.0](https://github.com/rabbitmq/rabbitmq-dotnet-client/tree/v6.5.0) (2023-03-25)

[Full Changelog](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.4.0...v6.5.0)

**Implemented enhancements:**

- Investigate the use of TaskCreationOptions.LongRunning [\#1318](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1318)
- Exception during recovery causes recovery failure [\#658](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/658)

**Closed issues:**

- Consumer tags aren't cleaned on channel close causing memory leak. [\#1302](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1302)
- Impossible to use a ulong as a header value [\#1298](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1298)
- Recovery does not appear to save consumer arguments [\#1293](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1293)
- Timeout when opening a new channel after channel exception [\#1246](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1246)
- Autorecovery for server-named queues loops indefinitely when consumer listen this queue [\#1238](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1238)
- IModel.WaitForConfirmsOrDie\* methods don't document that they close [\#1234](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1234)
- Port \#1223 to main [\#1225](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1225)
- Better integrate max message size [\#1223](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1223)
- 7.0 Release Checklist [\#1191](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/1191)
- Missing IRecoveryable implementation [\#998](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/998)
- BasicGetResult body memory safety in 6.x+ [\#994](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/994)

## Changes Between 6.3.1 and 6.4.0

This release adds the ability to specify a maximum message size when receiving data. The default
values are:

* RabbitMQ .NET client 7.0.0 and beyond: 128MiB
* RabbitMQ .NET client 6.4.0 up to 7.0.0: no limit by default

Receiving a frame that specifies a content larger than the limit will throw an execption. This is to
help prevent situations as described in [this discussion](https://github.com/rabbitmq/rabbitmq-dotnet-client/discussions/1213).

To set a limit, use the set `MaxMessageSize` on your `ConnectionFactory` before opening connections:

```
// This sets the limit to 512MiB
var cf = new ConnectionFactory();
cf.MaxMessageSize = 536870912;
var conn = cf.CreateConnection()`
```

GitHub milestone: [`6.4.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/58?closed=1)
Diff: [link](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.3.1...v6.4.0)

## Changes Between 6.3.0 and 6.3.1

GitHub milestone: [`6.3.1`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/57?closed=1)
Diff: [link](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.3.0...v6.3.1)

## Changes Between 6.2.4 and 6.3.0

GitHub milestone: [`6.3.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/52?closed=1)
Diff: [link](https://github.com/rabbitmq/rabbitmq-dotnet-client/compare/v6.2.4...v6.3.0)

## Changes Between 6.2.3 and 6.2.4

GitHub milestone: [`6.2.4`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/55?closed=1)

This release contains some important bug fixes:

* [Fix connection leaks on auto recovery](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1145)
* [Fix buffer overflow when writing long strings](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1162)
* [Fix regression resulting in `ObjectDisposedException`](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1154)
* [Fix regression that could affect consuming after auto recovery](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1148)

## Changes Between 6.2.1 and 6.2.3

GitHub milestone: [`6.2.3`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/54?closed=1)
GitHub milestone: [`6.2.2`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/51?closed=1)

## Changes Between 6.2.0 and 6.2.1

6.2.0 was published incorrectly, resulting in version 6.2.1

GitHub milestone: [`6.2.1`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/50?closed=1).

## Changes Between 6.1.0 and 6.2.0

A full list of changes can be found in the GitHub milestone: [`6.2.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/49?closed=1).

## Changes Between 6.0.0 and 6.1.0

This release continues with improvements to memory use and object allocations.

A full list of changes can be found in the GitHub milestone: [`6.1.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/48?closed=1).

## Changes Between 5.2.0 and 6.0.0

This major release of this client introduces substantial improvements
in terms of memory footprint and throughput. They come at the cost
of minor but important **breaking API changes** covered below.

The client now requires .NET Framework 4.6.1 or .NET Standard 2.0.
Earlier versions are no longer supported by the `6.x` series.

Key improvements in this release have been the result of hard work by
our stellar community members (in no particular order): @stebet, @bording,
@Anarh2404, @danielmarbach, and others.

A full list of changes can be found in the GitHub milestone: [`6.0.0`](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/41?closed=1).

### The Switch to System.Memory (and [Significantly Lower Memory Footprint](https://stebet.net/real-world-example-of-reducing-allocations-using-span-t-and-memory-t/) that Comes with It)

The client now uses the [`System.Memory` library](https://www.nuget.org/packages/System.Memory/) for message and command payloads. This significantly
reduces object allocation and GC pressure for heavy workloads but also
**potentially requires application changes**: consumer delivery payloads are now of instance [`System.ReadOnlyMemory<byte>`](https://docs.microsoft.com/en-us/dotnet/api/system.readonlymemory-1?view=netcore-3.1)
instead of `byte[]`.

While there's an implicit conversion for these types,
instances of `System.ReadOnlyMemory<byte>` **must be copied or consumed/deserialised before delivery handler completes**.
Holding on to delivered payloads and referencing them at a later point **is no longer safe**.

The same applies to publishers and the `IModel.BasicPublish` method: prefer using `System.ReadOnlyMemory<byte>`
over `byte[]` and dont' assume that this memory can be retained and used outside of the scope of the publishing
function.

GitHub issue: [#732](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/732)

### Timeouts Use `System.TimeSpan`

All timeout arguments now use `System.TimeSpan` values.

GitHub issue: [#688](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/688)

### Reduced Public API Surface

No major changes here but this is potentially breaking. Only public classes that were never meant
to be publicly used have been turned internal to the client.

GitHub issue: [#714](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/714)

### Requires .NET Framework 4.6.1 or .NET Standard 2.0

The client now requires .NET Framework 4.6.1 or .NET Standard 2.0. Earlier versions are no longer
supported.

GitHub issue: [#686](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/686)

### `Microsoft.Diagnostics.Tracing.EventSource` Dependency Dropped

`Microsoft.Diagnostics.Tracing.EventSource` dependency has been removed. It was an annoying
dependency to have for some environments.

### Source Linking

The library now supports source linking.

GitHub issue: [#697](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/697)

### NuGet Source Packages

Source packages are now also distributed via NuGet.

### CRL Checks for Server x.509 (TLS) Certificates

Added a TLS option to enforce CRL checks for server certificates.

GitHub issue: [#500](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/500)



## Changes Between 5.1.2 and 5.2.0

Please see the milestone for all changes:

[GitHub `5.2.0` Milestone](https://github.com/rabbitmq/rabbitmq-dotnet-client/milestone/42?closed=1)

Selected highlights:

### Add support for `netstandard2.0`

GitHub issues: [#428](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/428), [#435](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/435)

### Re-introduce lock for all socket writes

GitHub PR: [rabbitmq-dotnet-client#702](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/702)



## Changes Between 5.1.1 and 5.1.2

### Bump System.Net.Security to 4.3.2

## Changes Between 5.1.0 and 5.1.1

### Deprecate messaging patterns

GitHub PR: [rabbitmq-dotnet-client#654](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/654)

### Fix stack overflow

GitHub PR: [rabbitmq-dotnet-client#578](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/578)

### Byte conversion support

GitHub PR: [rabbitmq-dotnet-client#579](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/579)

### Heartbeat write deadlock fix

GitHub PR: [rabbitmq-dotnet-client#636](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/636)


## Changes Between 5.0.x and 5.1.0

### Batch Publishing

GitHub PR: [rabbitmq-dotnet-client#368](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/368)

### Introduced a Static Property to AmqpTcpEndpoint to specify the default TLS Protocol Version(s)

GitHub PR: [rabbitmq-dotnet-client#389](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/389)

### All Exceptions are Collected When Selecting an Endpoint

GitHub PR: [rabbitmq-dotnet-client#377](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/377)

### Reduced Lock Contention for Frame Writes

GitHub PR: [rabbitmq-dotnet-client#354](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/354)


## Changes Between 4.1.x and 5.0.1

### Async consumers

GitHub PR: [rabbitmq-dotnet-client#307](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/307)

### Enable connection recovery by default

GitHub issue: [rabbitmq-dotnet-client#271](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/271)

### Replace Console.WriteLine logging with EventSource

GitHub issue: [rabbitmq-dotnet-client#94](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/94)

### Add events for connection recovery errors and connection success

GitHub issue: [rabbitmq-dotnet-client#156](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/156)

### noAck renamed to autoAck

GitHub issue: [rabbitmq-dotnet-client#255](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/255)

### Make uri and Uri setters in ConnectionFactory obsolete

GitHub issue: [rabbitmq-dotnet-client#264](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/264)

### Ensure connection recovery does not keep going after the connection has been closed

GitHub issue: [rabbitmq-dotnet-client#294](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/294)

### Synchronize access to the manuallyClosed field in AutorecoveringConnection.

GitHub issue: [rabbitmq-dotnet-client#291](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/291)

### Use concurrent collections inside AutorecoveringConnection

GitHub issue: [rabbitmq-dotnet-client#288](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/288)

### Add property to allow an endpoint to specify the address family

GitHub issue: [rabbitmq-dotnet-client#226](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/226)

### Fix potential EventingBasicConsumer race condition

### Re-introduce a Uri property on IConnectionFactory

GitHub issue: [rabbitmq-dotnet-client#330](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/330)

### Add CreateConnection overload to IConnectionFactory

GitHub PR: [rabbitmq-dotnet-client#325](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/325)

## Changes Between 4.1.0 and 4.1.1

### Fixed XML Documentation Generation

GitHub issue: [rabbitmq-dotnet-client#269](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/269)

Contributed by Brandon Ording.

### Fixed WinRT Project Build

GitHub issue: [rabbitmq-dotnet-client#270](https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/270)

Contributed by Brandon Ording.

### `TcpClientAdapter` Nullifies Socket Field on Close

GitHub issue: [rabbitmq-dotnet-client#263](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/263)

### `HeartbeatReadTimerCallback` Handles Potential `NullReferenceException`

GitHub issue: [rabbitmq-dotnet-client#257](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/257)


## Changes Between 4.0.2 and 4.1.0

`4.1.0` was released on **September 14th, 2016**.

### No lock contention at consumer work service

Switched to a "thread-per-model" approach in the `ConsumerWorkService`.

The TaskScheduler property on `ConnectionFactory` has been obsoleted and can no
longer be used to control concurrency.

Utility class changes:

- `BatchingWorkPool` has been removed
- `ConsumerWorkService` no longer has a constructor that takes a `TaskScheduler`
- `ConsumerWorkService.MAX_THUNK_EXECUTION_BATCH_SIZE` has been removed
- `ConsumerWorkService` no longer has the `ExecuteThunk` or `RegisterKey` methods

Contributed by Brandon Ording and Szymon Kulec.

GH issue: [rabbitmq-dotnet-client#251](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/251)


## Changes Between 4.0.1 and 4.0.2 (September 1st, 2016)

### TcpClientAdapter Didn't Respect IP Address Family

GH issue: [rabbitmq-dotnet-client#244](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/244)

## Changes Between 4.0.0 and 4.0.1 (August 25th, 2016)

### `ConnectionFactory#CreateConnection` Deadlock

`ConnectionFactory#CreateConnection` could deadlock in some circumstances.

GH issue: [rabbitmq-dotnet-client#239](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/239).

### Occasional `NullReferenceException` in Endpoint Resolution

GH issue: [rabbitmq-dotnet-client#238](https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/238)


\* *This Changelog was automatically generated by [github_changelog_generator](https://github.com/github-changelog-generator/github-changelog-generator)*
