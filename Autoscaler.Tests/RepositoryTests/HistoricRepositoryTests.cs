using System.Data;
using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.ForecastRepository;
using Autoscaler.Persistence.HistoricRepository;
using Moq;

namespace Autoscaler.Tests.RepositoryTests;
[TestFixture()]
public class HistoricRepositoryTests
{
    private HistoricEntity _historicEntity;
    private Mock<IHistoricRepository> _historicRepository;
    private Mock<IDbConnection> _dbConnection = new Mock<IDbConnection>();
    private Mock<IDbConnectionFactory> _dbConnectionFactory = new Mock<IDbConnectionFactory>();
    
    
    [SetUp]
    public void Setup()
    {
        _historicEntity = new HistoricEntity();
        _historicRepository = new Mock<IHistoricRepository>();
        _dbConnectionFactory.Setup(x => x.Connection).Returns(_dbConnection.Object);
    }
    
    [Test()]
    public void GetHistoricDataByServiceIdAsyncFailure()
    {
        _historicRepository.Setup(x => x.GetHistoricDataByServiceIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _historicRepository.Object.GetHistoricDataByServiceIdAsync(It.IsAny<Guid>()));
    }
    
    [Test()]
    public void GetHistoricDataByServiceIdAsync()
    {
        _historicRepository.Setup(x => x.GetHistoricDataByServiceIdAsync(It.IsAny<Guid>())).ReturnsAsync(_historicEntity);
        var result = _historicRepository.Object.GetHistoricDataByServiceIdAsync(It.IsAny<Guid>());
        Assert.That(result.Result, Is.EqualTo(_historicEntity));
    }
    
    [Test()]
    public void UpsertHistoricDataAsyncFailure()
    {
        _historicRepository.Setup(x => x.UpsertHistoricDataAsync(It.IsAny<HistoricEntity>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _historicRepository.Object.UpsertHistoricDataAsync(It.IsAny<HistoricEntity>()));
    }
    
    [Test()]
    public void UpsertHistoricDataAsync()
    {
        _historicRepository.Setup(x => x.UpsertHistoricDataAsync(It.IsAny<HistoricEntity>())).ReturnsAsync(true);
        var result = _historicRepository.Object.UpsertHistoricDataAsync(It.IsAny<HistoricEntity>());
        Assert.That(result.Result, Is.EqualTo(true));
    }
}