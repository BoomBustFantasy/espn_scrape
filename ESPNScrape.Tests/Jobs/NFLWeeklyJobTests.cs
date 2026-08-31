using ESPNScrape.Jobs;
using ESPNScrape.Models;
using ESPNScrape.Models.Supa;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ESPNScrape.Tests.Jobs;

public class NFLWeeklyJobTests
{
    private readonly Mock<ILogger<NFLWeeklyJob>> _mockLogger;
    private readonly Mock<IESPNGameSource> _mockEspnGameSource;
    private readonly Mock<IPlayerRepository> _mockPlayerRepository;
    private readonly Mock<IPlayerStatRepository> _mockPlayerStatRepository;
    private readonly NFLWeeklyJob _job;

    public NFLWeeklyJobTests()
    {
        _mockLogger = new Mock<ILogger<NFLWeeklyJob>>();
        _mockEspnGameSource = new Mock<IESPNGameSource>();
        _mockPlayerRepository = new Mock<IPlayerRepository>();
        _mockPlayerStatRepository = new Mock<IPlayerStatRepository>();

        _mockPlayerRepository
            .Setup(s => s.GetPlayersAsync(null))
            .ReturnsAsync(new List<Models.Supa.Player>());

        _job = new NFLWeeklyJob(
            _mockLogger.Object,
            _mockEspnGameSource.Object,
            _mockPlayerRepository.Object,
            _mockPlayerStatRepository.Object
        );
    }

    [Fact]
    public async Task Execute_WithExplicitJobData_ShouldFetchGames()
    {
        // Arrange
        var jobData = new JobDataMap
        {
            { "season", 2025 },
            { "startWeek", 1 },
            { "endWeek", 1 }
        };

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.MergedJobDataMap).Returns(jobData);

        _mockEspnGameSource
            .Setup(s => s.GetNFLWeekGamesAsync(2025, 1))
            .ReturnsAsync(new List<Game>()); // Return empty list to stop processing early

        // Act
        await _job.Execute(mockContext.Object);

        // Assert
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(2025, 1), Times.Once);
    }

    [Fact]
    public async Task Execute_WithNoJobData_ScansWeekWindowReportedByEspn()
    {
        // Arrange - no job data, so the job must ask ESPN where the season is
        _mockEspnGameSource
            .Setup(s => s.GetCurrentSeasonPhaseAsync())
            .ReturnsAsync(new SeasonPhase(2026, SeasonPhase.RegularSeason, 5));
        _mockEspnGameSource
            .Setup(s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.MergedJobDataMap).Returns(new JobDataMap());

        // Act
        await _job.Execute(mockContext.Object);

        // Assert - current week plus the one before it, for the season ESPN reported
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(2026, 4), Times.Once);
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(2026, 5), Times.Once);
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_WhenEspnReportsPreseason_ScrapesNothing()
    {
        // Arrange - preseason week numbers must not be read as regular season weeks
        _mockEspnGameSource
            .Setup(s => s.GetCurrentSeasonPhaseAsync())
            .ReturnsAsync(new SeasonPhase(2026, SeasonPhase.Preseason, 4));

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.MergedJobDataMap).Returns(new JobDataMap());

        // Act
        await _job.Execute(mockContext.Object);

        // Assert - no games fetched, and no pointless full player load either
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockPlayerRepository.Verify(s => s.GetPlayersAsync(null), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenEspnReportsPostseason_RechecksWeek18Only()
    {
        // Arrange - week 18 stats can still be corrected once the playoffs begin
        _mockEspnGameSource
            .Setup(s => s.GetCurrentSeasonPhaseAsync())
            .ReturnsAsync(new SeasonPhase(2026, SeasonPhase.Postseason, 1));
        _mockEspnGameSource
            .Setup(s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.MergedJobDataMap).Returns(new JobDataMap());

        // Act
        await _job.Execute(mockContext.Object);

        // Assert
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(2026, 18), Times.Once);
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }
}
