using ESPNScrape.Jobs;
using ESPNScrape.Models;
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ESPNScrape.Tests.Jobs;

public class NFLWeeklyJobTests
{
    private readonly Mock<ILogger<NFLWeeklyJob>> _mockLogger;
    private readonly Mock<IESPNDataService> _mockEspnService;
    private readonly Mock<IESPNPlayerMappingService> _mockMappingService;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly NFLWeeklyJob _job;

    public NFLWeeklyJobTests()
    {
        _mockLogger = new Mock<ILogger<NFLWeeklyJob>>();
        _mockEspnService = new Mock<IESPNDataService>();
        _mockMappingService = new Mock<IESPNPlayerMappingService>();
        _mockSupabaseService = new Mock<ISupabaseService>();

        _job = new NFLWeeklyJob(
            _mockLogger.Object,
            _mockEspnService.Object,
            _mockMappingService.Object,
            _mockSupabaseService.Object
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

        _mockEspnService
            .Setup(s => s.GetNFLWeekGamesAsync(2025, 1))
            .ReturnsAsync(new List<Game>()); // Return empty list to stop processing early

        // Act
        await _job.Execute(mockContext.Object);

        // Assert
        _mockEspnService.Verify(s => s.GetNFLWeekGamesAsync(2025, 1), Times.Once);
    }
}
