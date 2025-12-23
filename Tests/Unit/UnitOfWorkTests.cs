using FluentAssertions;
using Infrastructure.Interfaces;
using Infrastructure.UnitOfWork;
using Moq;
using Xunit;

namespace Tests.Unit;

/// <summary>
/// تست‌های Unit برای UnitOfWork و UnitOfWorkManager
/// </summary>
public class UnitOfWorkTests
{
    [Fact]
    public async Task UnitOfWork_Commit_ShouldCallTransactionScopeCommit()
    {
        // Arrange
        var mockTransactionScope = new Mock<ITransactionScope>();
        var unitOfWork = new UnitOfWork(mockTransactionScope.Object);
        var cancellationToken = CancellationToken.None;

        // Act
        await unitOfWork.Commit(cancellationToken);

        // Assert
        mockTransactionScope.Verify(x => x.CommitAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task UnitOfWork_Commit_AfterDispose_ShouldThrow()
    {
        // Arrange
        var mockTransactionScope = new Mock<ITransactionScope>();
        var unitOfWork = new UnitOfWork(mockTransactionScope.Object);
        unitOfWork.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => unitOfWork.Commit(CancellationToken.None));
    }

    [Fact]
    public void UnitOfWork_Dispose_ShouldCallTransactionScopeDisposeAsync()
    {
        // Arrange
        var mockTransactionScope = new Mock<ITransactionScope>();
        mockTransactionScope.Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWork = new UnitOfWork(mockTransactionScope.Object);

        // Act
        unitOfWork.Dispose();

        // Assert
        mockTransactionScope.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void UnitOfWork_Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var mockTransactionScope = new Mock<ITransactionScope>();
        mockTransactionScope.Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWork = new UnitOfWork(mockTransactionScope.Object);

        // Act & Assert
        unitOfWork.Dispose();
        unitOfWork.Dispose(); // Should not throw
    }

    [Fact]
    public async Task UnitOfWorkManager_CreateNew_ShouldCreateUnitOfWork()
    {
        // Arrange
        var mockTransactionContext = new Mock<ITransactionContext>();
        var mockTransactionScope = new Mock<ITransactionScope>();
        
        mockTransactionContext.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTransactionScope.Object);

        var unitOfWorkManager = new UnitOfWorkManager(mockTransactionContext.Object);
        var cancellationToken = CancellationToken.None;

        // Act
        var unitOfWork = await unitOfWorkManager.CreateNew(cancellationToken);

        // Assert
        unitOfWork.Should().NotBeNull();
        unitOfWork.Should().BeAssignableTo<IUnitOfWork>();
        mockTransactionContext.Verify(x => x.BeginTransactionAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task UnitOfWorkManager_CreateNew_WithNullTransactionContext_ShouldThrow()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            var manager = new UnitOfWorkManager(null!);
            await manager.CreateNew(CancellationToken.None);
        });
    }
}

