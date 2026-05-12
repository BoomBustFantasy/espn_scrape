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
}
