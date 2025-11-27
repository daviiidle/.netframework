namespace IntegrationTests;

using Models;
using Xunit;

public class RetryPolicyTests
{
    [Fact]
    public void Execute_SucceedsFirstTime_NoRetries()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var executeCount = 0;

        // Act
        var result = retryPolicy.Execute(() =>
        {
            executeCount++;
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public void Execute_FailsOnceThenSucceeds_RetriesOnce()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var executeCount = 0;

        // Act
        var result = retryPolicy.Execute(() =>
        {
            executeCount++;
            if (executeCount == 1)
                throw new InvalidOperationException("First attempt fails");
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, executeCount);
    }

    [Fact]
    public void Execute_FailsMaxTimes_ThrowsException()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var executeCount = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            retryPolicy.Execute(() =>
            {
                executeCount++;
                throw new InvalidOperationException("Always fails");
            });
        });

        Assert.Equal(4, executeCount); // Initial attempt + 3 retries
        Assert.Equal("Always fails", exception.Message);
    }

    [Fact]
    public void Execute_ExponentialBackoff_IncreasingDelays()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var delays = new List<TimeSpan>();
        var executeCount = 0;

        // Override the delay mechanism to capture delays
        retryPolicy.OnRetry = (attempt, delay) =>
        {
            delays.Add(delay);
        };

        // Act
        try
        {
            retryPolicy.Execute(() =>
            {
                executeCount++;
                throw new InvalidOperationException("Test");
            });
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        Assert.Equal(3, delays.Count); // 3 retries
        Assert.Equal(TimeSpan.FromSeconds(1), delays[0]); // 1st retry: 1s
        Assert.Equal(TimeSpan.FromSeconds(2), delays[1]); // 2nd retry: 2s
        Assert.Equal(TimeSpan.FromSeconds(4), delays[2]); // 3rd retry: 4s
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsFirstTime_NoRetries()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var executeCount = 0;

        // Act
        var result = await retryPolicy.ExecuteAsync(async () =>
        {
            executeCount++;
            await Task.Delay(1);
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public async Task ExecuteAsync_FailsOnceThenSucceeds_RetriesOnce()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var executeCount = 0;

        // Act
        var result = await retryPolicy.ExecuteAsync(async () =>
        {
            executeCount++;
            await Task.Delay(1);
            if (executeCount == 1)
                throw new InvalidOperationException("First attempt fails");
            return "success";
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, executeCount);
    }

    [Fact]
    public async Task ExecuteAsync_FailsMaxTimes_ThrowsException()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 3);
        var executeCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await retryPolicy.ExecuteAsync(async () =>
            {
                executeCount++;
                await Task.Delay(1);
                throw new InvalidOperationException("Always fails");
            });
        });

        Assert.Equal(4, executeCount); // Initial attempt + 3 retries
    }

    [Fact]
    public void Execute_ZeroMaxRetries_NoRetries()
    {
        // Arrange
        var retryPolicy = new RetryPolicy(maxRetries: 0);
        var executeCount = 0;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            retryPolicy.Execute(() =>
            {
                executeCount++;
                throw new InvalidOperationException("Fails");
            });
        });

        Assert.Equal(1, executeCount); // Only initial attempt, no retries
    }

    [Fact]
    public void Constructor_NegativeMaxRetries_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new RetryPolicy(maxRetries: -1));
    }
}
