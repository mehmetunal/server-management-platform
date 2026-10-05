using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Templates;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Validators.Commands;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class ServerTemplateServiceTests
{
    private readonly IServerTemplateRepository _repository = Substitute.For<IServerTemplateRepository>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ServerTemplateService _service;

    public ServerTemplateServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _service = new ServerTemplateService(_repository, _auditLog, _currentUser, new ServerTemplateFormDtoValidator(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task CreateAsync_SavesNormalizedTemplateAndAudits()
    {
        ServerTemplate? saved = null;
        await _repository.AddAsync(Arg.Do<ServerTemplate>(t => saved = t), Arg.Any<CancellationToken>());

        var result = await _service.CreateAsync(new ServerTemplateFormDto { Name = "  Güncelle ", Content = "apt-get update\r\napt-get -y upgrade", RequiresSudo = true }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Güncelle", saved!.Name);
        Assert.Equal("apt-get update\napt-get -y upgrade", saved.Content);
        Assert.True(saved.RequiresSudo);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.TemplateCreate), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateName()
    {
        _repository.NameExistsAsync("Güncelle", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.CreateAsync(new ServerTemplateFormDto { Name = "Güncelle", Content = "uptime" }, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ServerTemplateFormDto.Name));
    }

    [Theory]
    [InlineData("#cloud-config\npackages: [nginx]", true)]
    [InlineData("#!/bin/sh\necho hi", true)]
    [InlineData("packages: [nginx]", false)]
    public async Task CreateAsync_ValidatesCloudInitHeader(string content, bool valid)
    {
        var result = await _service.CreateAsync(new ServerTemplateFormDto { Name = "init", Kind = ServerTemplateKind.CloudInit, Content = content }, TestContext.Current.CancellationToken);

        Assert.Equal(valid, result.IsSuccess);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var template = new ServerTemplate { Name = "eski", Content = "uptime" };
        _repository.GetAsync(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await _service.DeleteAsync(template.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(template.IsDeleted);
        Assert.Equal("admin@example.com", template.DeletedBy);
    }

    [Fact]
    public async Task GetOptionsAsync_FiltersByKind()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new ServerTemplate { Name = "a", Kind = ServerTemplateKind.Script, Content = "uptime" },
            new ServerTemplate { Name = "b", Kind = ServerTemplateKind.CloudInit, Content = "#cloud-config" }
        ]);

        var options = await _service.GetOptionsAsync(ServerTemplateKind.Script, TestContext.Current.CancellationToken);

        Assert.Equal(["a"], options.Select(o => o.Name));
    }
}
