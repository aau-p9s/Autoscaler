using System.Data;
using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.ServicesRepository;
using Moq;

namespace Autoscaler.Tests.RepositoryTests;

[TestFixture()]
public class ServicesRepositoryTests
{
    private ServiceEntity _serviceEntity;
    private Mock<IServicesRepository> _servicesRepository;
    private Mock<IDbConnection> _dbConnection = new Mock<IDbConnection>();
    private Mock<IDbConnectionFactory> _dbConnectionFactory = new Mock<IDbConnectionFactory>();


    [SetUp]
    public void Setup()
    {
        _serviceEntity = new ServiceEntity();
        _servicesRepository = new Mock<IServicesRepository>();
        _dbConnectionFactory.Setup(x => x.Connection).Returns(_dbConnection.Object);
    }

    [Test()]
    public void GetServiceByIdAsyncFailure()
    {
        _servicesRepository.Setup(x => x.GetServiceByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _servicesRepository.Object.GetServiceByIdAsync(It.IsAny<Guid>()));
    }

    [Test()]
    public void GetServiceByIdAsync()
    {
        _servicesRepository.Setup(x => x.GetServiceByIdAsync(It.IsAny<Guid>())).ReturnsAsync(_serviceEntity);
        var result = _servicesRepository.Object.GetServiceByIdAsync(It.IsAny<Guid>());
        Assert.That(result.Result, Is.EqualTo(_serviceEntity));
    }

    [Test()]
    public void GetServiceIdByNameAsyncFailure()
    {
        _servicesRepository.Setup(x => x.GetServiceIdByNameAsync(It.IsAny<string>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _servicesRepository.Object.GetServiceIdByNameAsync(It.IsAny<string>()));
    }

    [Test()]
    public void GetServiceIdByNameAsync()
    {
        _servicesRepository.Setup(x => x.GetServiceIdByNameAsync(It.IsAny<string>())).ReturnsAsync(It.IsAny<Guid>());
        var result = _servicesRepository.Object.GetServiceIdByNameAsync(It.IsAny<string>());
        Assert.That(result.Result, Is.EqualTo(It.IsAny<Guid>()));
    }
    
    [Test()]
    public void GetAllServicesAsyncFailure()
    {
        _servicesRepository.Setup(x => x.GetAllServicesAsync()).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _servicesRepository.Object.GetAllServicesAsync());
    }
    
    [Test()]
    public void GetAllServicesAsync()
    {
        _servicesRepository.Setup(x => x.GetAllServicesAsync()).ReturnsAsync(new List<ServiceEntity>());
        var result = _servicesRepository.Object.GetAllServicesAsync();
        Assert.That(result.Result, Is.EqualTo(new List<ServiceEntity>()));
    }
    
    [Test()]
    public void UpsertServiceAsyncFailure()
    {
        _servicesRepository.Setup(x => x.UpsertServiceAsync(It.IsAny<ServiceEntity>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _servicesRepository.Object.UpsertServiceAsync(It.IsAny<ServiceEntity>()));
    }
    
    [Test()]
    public void UpsertServiceAsync()
    {
        _servicesRepository.Setup(x => x.UpsertServiceAsync(It.IsAny<ServiceEntity>())).ReturnsAsync(true);
        var result = _servicesRepository.Object.UpsertServiceAsync(It.IsAny<ServiceEntity>());
        Assert.That(result.Result, Is.EqualTo(true));
    }
}