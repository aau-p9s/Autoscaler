using System.Data;
using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.ForecastRepository;
using Moq;

namespace Autoscaler.Tests.RepositoryTests;
[TestFixture()]
public class ForecastRepositoryTests
{
    private ForecastEntity _forecastEntity;
    private Mock<IForecastRepository> _forecastRepository;
    private Mock<IDbConnection> _dbConnection = new Mock<IDbConnection>();
    private Mock<IDbConnectionFactory> _dbConnectionFactory = new Mock<IDbConnectionFactory>();
    
    
    [SetUp]
    public void Setup()
    {
        _forecastEntity = new ForecastEntity();
        _forecastRepository = new Mock<IForecastRepository>();
        _dbConnectionFactory.Setup(x => x.Connection).Returns(_dbConnection.Object);
        
    }
    
    [Test()]
    public void GetForecastsByServiceIdAsyncFailure()
    {
        _forecastRepository.Setup(x => x.GetForecastsByServiceIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _forecastRepository.Object.GetForecastsByServiceIdAsync(It.IsAny<Guid>()));
    }
    
    [Test()]
    public void GetForecastsByServiceIdAsync()
    {
        _forecastRepository.Setup(x => x.GetForecastsByServiceIdAsync(It.IsAny<Guid>())).ReturnsAsync(_forecastEntity);
        var result = _forecastRepository.Object.GetForecastsByServiceIdAsync(It.IsAny<Guid>());
        Assert.That(result.Result, Is.EqualTo(_forecastEntity));
    }
    
    [Test()]
    public void GetForecastByIdAsyncFailure()
    {
        _forecastRepository.Setup(x => x.GetForecastByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _forecastRepository.Object.GetForecastByIdAsync(It.IsAny<Guid>()));
    }
    
    [Test()]
    public void GetForecastByIdAsync()
    {
        _forecastRepository.Setup(x => x.GetForecastByIdAsync(It.IsAny<Guid>())).ReturnsAsync(_forecastEntity);
        var result = _forecastRepository.Object.GetForecastByIdAsync(It.IsAny<Guid>());
        Assert.That(result.Result, Is.EqualTo(_forecastEntity));
    }
}