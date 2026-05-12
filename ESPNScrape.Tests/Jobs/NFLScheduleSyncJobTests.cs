using ESPNScrape.Jobs;
using ESPNScrape.Models;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ESPNScrape.Tests.Jobs;

public class NFLScheduleSyncJobTests
{
    private readonly Mock<ILogger<NFLScheduleSyncJob>> _mockLogger;
    private readonly Mock<IESPNGameSource> _mockEspnGameSource;
    private readonly Mock<IScheduleRepository> _mockScheduleRepository;
    private readonly NFLScheduleSyncJob _job;

    public NFLScheduleSyncJobTests()
    {
        _mockLogger = new Mock<ILogger<NFLScheduleSyncJob>>();
        _mockEspnGameSource = new Mock<IESPNGameSource>();
        _mockScheduleRepository = new Mock<IScheduleRepository>();

        // Return empty game list by default so ProcessGameSchedule is never called
        _mockEspnGameSource
            .Setup(s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Game>());

        _job = new NFLScheduleSyncJob(
            _mockLogger.Object,
            _mockEspnGameSource.Object,
            _mockScheduleRepository.Object
        );
    }

    [Fact]
    public async Task Execute_WithExplicitWeekRange_FetchesGamesForEachWeek()
    {
        // Arrange — season=2025, startWeek=1, endWeek=3 → 3 fetches
        var jobData = new JobDataMap
        {
            { "season", 2025 },
            { "startWeek", 1 },
            { "endWeek", 3 }
        };

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.MergedJobDataMap).Returns(jobData);

        // Act
        await _job.Execute(mockContext.Object);

        // Assert — one call per week
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(2025, 1), Times.Once);
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(2025, 2), Times.Once);
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(2025, 3), Times.Once);
        _mockEspnGameSource.Verify(s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Execute_WithNoExplicitData_IteratesWeeks1Through18()
    {
        // Arrange — no job data → default path iterates weeks 1-18
        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.MergedJobDataMap).Returns(new JobDataMap());

        // Act
        await _job.Execute(mockContext.Object);

        // Assert — exactly 18 calls, one per regular-season week
        _mockEspnGameSource.Verify(
            s => s.GetNFLWeekGamesAsync(It.IsAny<int>(), It.IsAny<int>()),
            Times.Exactly(18));
    }
}
