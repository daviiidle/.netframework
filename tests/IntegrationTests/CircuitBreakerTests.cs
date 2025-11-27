namespace IntegrationTests;

using Models;
using Xunit;

public class CircuitBreakerTests
{
    [Fact]
    public void Execute_SuccessfulCalls_KeepCircuitClosed()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(1));
        var callCount = 0;

        // Act - Execute 5 successful calls
        for (int i = 0; i < 5; i++)
        {
            circuitBreaker.Execute(() =>
            {
                callCount++;
                return "success";
            });
        }

        // Assert
        Assert.Equal(5, callCount);
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public void Execute_ThreeFailures_OpenCircuit()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(1));
        var callCount = 0;

        // Act - Execute 3 failing calls
        for (int i = 0; i < 3; i++)
        {
            try
            {
                circuitBreaker.Execute(() =>
                {
                    callCount++;
                    throw new InvalidOperationException("Test failure");
                });
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Assert
        Assert.Equal(3, callCount);
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
    }

    [Fact]
    public void Execute_OpenCircuit_ThrowsCircuitBreakerOpenException()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(1));

        // Open the circuit by causing 3 failures
        for (int i = 0; i < 3; i++)
        {
            try
            {
                circuitBreaker.Execute(() =>
                {
                    throw new InvalidOperationException("Test failure");
                });
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Act & Assert - Next call should throw CircuitBreakerOpenException
        Assert.Throws<CircuitBreakerOpenException>(() =>
        {
            circuitBreaker.Execute(() => "should not execute");
        });
    }

    [Fact]
    public void Execute_OpenCircuit_DoesNotExecuteAction()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(1));
        var actionExecuted = false;

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                circuitBreaker.Execute(() =>
                {
                    throw new InvalidOperationException("Test failure");
                });
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Act & Assert
        try
        {
            circuitBreaker.Execute(() =>
            {
                actionExecuted = true;
                return "should not execute";
            });
        }
        catch (CircuitBreakerOpenException)
        {
            // Expected
        }

        Assert.False(actionExecuted);
    }

    [Fact]
    public async Task Execute_AfterTimeout_TransitionsToHalfOpen()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromMilliseconds(100));

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                circuitBreaker.Execute(() =>
                {
                    throw new InvalidOperationException("Test failure");
                });
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Act - Wait for timeout
        await Task.Delay(150);

        // Assert - State should transition to HalfOpen on next attempt
        var actionExecuted = false;
        try
        {
            circuitBreaker.Execute(() =>
            {
                actionExecuted = true;
                return "test";
            });
        }
        catch
        {
            // May throw if still checking
        }

        Assert.True(actionExecuted); // Action should be attempted in HalfOpen state
        Assert.Equal(CircuitState.Closed, circuitBreaker.State); // Success should close circuit
    }

    [Fact]
    public async Task Execute_HalfOpen_SuccessClosesCircuit()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromMilliseconds(100));

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                circuitBreaker.Execute(() =>
                {
                    throw new InvalidOperationException("Test failure");
                });
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Wait for timeout to enter HalfOpen
        await Task.Delay(150);

        // Act - Execute successful call in HalfOpen state
        var result = circuitBreaker.Execute(() => "success");

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);
    }

    [Fact]
    public async Task Execute_HalfOpen_FailureReopensCircuit()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromMilliseconds(100));

        // Open the circuit
        for (int i = 0; i < 3; i++)
        {
            try
            {
                circuitBreaker.Execute(() =>
                {
                    throw new InvalidOperationException("Test failure");
                });
            }
            catch (InvalidOperationException)
            {
                // Expected
            }
        }

        // Wait for timeout to enter HalfOpen
        await Task.Delay(150);

        // Act - Fail in HalfOpen state
        try
        {
            circuitBreaker.Execute(() =>
            {
                throw new InvalidOperationException("Test failure in half-open");
            });
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert - Circuit should be Open again
        Assert.Equal(CircuitState.Open, circuitBreaker.State);

        // Verify next call throws CircuitBreakerOpenException
        Assert.Throws<CircuitBreakerOpenException>(() =>
        {
            circuitBreaker.Execute(() => "should not execute");
        });
    }

    [Fact]
    public void Execute_MixedSuccessAndFailure_TracksCorrectly()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(1));

        // Act - 2 failures, 1 success, 2 more failures
        try { circuitBreaker.Execute(() => { throw new Exception("fail"); }); } catch { }
        try { circuitBreaker.Execute(() => { throw new Exception("fail"); }); } catch { }

        circuitBreaker.Execute(() => "success"); // Success resets counter

        try { circuitBreaker.Execute(() => { throw new Exception("fail"); }); } catch { }
        try { circuitBreaker.Execute(() => { throw new Exception("fail"); }); } catch { }

        // Assert - Should still be closed (only 2 consecutive failures after reset)
        Assert.Equal(CircuitState.Closed, circuitBreaker.State);

        // One more failure should open it
        try { circuitBreaker.Execute(() => { throw new Exception("fail"); }); } catch { }
        Assert.Equal(CircuitState.Open, circuitBreaker.State);
    }

    [Fact]
    public void Constructor_ZeroFailureThreshold_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new CircuitBreaker(failureThreshold: 0, timeout: TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_NegativeTimeout_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Execute_ReturnsCorrectValue()
    {
        // Arrange
        var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(1));

        // Act
        var result = circuitBreaker.Execute(() => 42);

        // Assert
        Assert.Equal(42, result);
    }
}
