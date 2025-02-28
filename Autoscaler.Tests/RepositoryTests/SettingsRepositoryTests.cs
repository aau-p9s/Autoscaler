using System.Data;
using Autoscaler.Persistence.Connection;
using Autoscaler.Persistence.SettingsRepository;
using Moq;

namespace Autoscaler.Tests.RepositoryTests;
[TestFixture()]
public class SettingsRepositoryTests
{
    private SettingsEntity _settingsEntity;
    private Mock<ISettingsRepository> _settingsRepository;
    private Mock<IDbConnection> _dbConnection = new Mock<IDbConnection>();
    private Mock<IDbConnectionFactory> _dbConnectionFactory = new Mock<IDbConnectionFactory>();
    
    
    [SetUp]
    public void Setup()
    {
        _settingsEntity = new SettingsEntity();
        _settingsRepository = new Mock<ISettingsRepository>();
        _dbConnectionFactory.Setup(x => x.Connection).Returns(_dbConnection.Object);
    }
    
    [Test()]
    public void GetSettingsByServiceIdAsyncFailure()
    {
        _settingsRepository.Setup(x => x.GetSettingsForServiceAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _settingsRepository.Object.GetSettingsForServiceAsync(It.IsAny<Guid>()));
    }
    
    [Test()]
    public void GetSettingsByServiceIdAsync()
    {
        _settingsRepository.Setup(x => x.GetSettingsForServiceAsync(It.IsAny<Guid>())).ReturnsAsync(_settingsEntity);
        var result = _settingsRepository.Object.GetSettingsForServiceAsync(It.IsAny<Guid>());
        Assert.That(result.Result, Is.EqualTo(_settingsEntity));
    }
    
    [Test()]
    public void UpsertSettingsAsyncFailure()
    {
        _settingsRepository.Setup(x => x.UpsertSettingsAsync(It.IsAny<SettingsEntity>())).ThrowsAsync(new Exception());
        Assert.ThrowsAsync<Exception>(() => _settingsRepository.Object.UpsertSettingsAsync(It.IsAny<SettingsEntity>()));
    }
    
    [Test()]
    public void UpsertSettingsAsync()
    {
        _settingsRepository.Setup(x => x.UpsertSettingsAsync(It.IsAny<SettingsEntity>())).ReturnsAsync(true);
        var result = _settingsRepository.Object.UpsertSettingsAsync(It.IsAny<SettingsEntity>());
        Assert.That(result.Result, Is.EqualTo(true));
    }
    
}