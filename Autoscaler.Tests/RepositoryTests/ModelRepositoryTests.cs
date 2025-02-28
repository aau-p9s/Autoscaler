using System.Data;
using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.ModelRepository;
using Moq;

namespace Autoscaler.Tests.RepositoryTests;
[TestFixture()]
public class ModelRepositoryTests
{
    private ModelEntity _modelEntity;
    private Mock<IModelRepository> _modelRepository;
    private Mock<IDbConnection> _dbConnection = new Mock<IDbConnection>();
    private Mock<IDbConnectionFactory> _dbConnectionFactory = new Mock<IDbConnectionFactory>();
    [SetUp]
    public void Setup()
    {
        _modelEntity = new ModelEntity();
        _modelRepository = new Mock<IModelRepository>();
        _dbConnectionFactory.Setup(x => x.Connection).Returns(_dbConnection.Object);
    }
    
    [Test()]
    public void GetModelsForServiceAsyncFailure()
    {
        _modelRepository.Setup(x => x.GetModelsForServiceAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _modelRepository.Object.GetModelsForServiceAsync(It.IsAny<Guid>()));
    }
    
    [Test()]
    public void GetModelsForServiceAsync()
    {
        _modelRepository.Setup(x => x.GetModelsForServiceAsync(It.IsAny<Guid>())).ReturnsAsync(new List<ModelEntity>());
        var result = _modelRepository.Object.GetModelsForServiceAsync(It.IsAny<Guid>());
        Assert.That(result.Result, Is.EqualTo(new List<ModelEntity>()));
    }
    
}