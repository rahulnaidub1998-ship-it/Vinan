using Vinan.Api.Models;
using Vinan.Api.Services;

namespace Vinan.Api.Tests;

public sealed class RiskClassifierTests
{
    [Fact]
    public void HighRiskActionWinsWhenRequestAlsoContainsCommunicationWords()
    {
        var risk = RiskClassifier.Classify("Send a deployment to production");

        Assert.Equal(RiskLevel.Level4, risk);
    }

    [Theory]
    [InlineData("Explain how production deployment works")]
    [InlineData("How do bank transfers work?")]
    public void InformationalQuestionsAreNotQueuedAsActions(string message)
    {
        var risk = RiskClassifier.Classify(message);

        Assert.Equal(RiskLevel.Level1, risk);
    }

    [Theory]
    [InlineData("Send an email to Rahul")]
    [InlineData("Schedule a meeting tomorrow")]
    public void ExternalActionsRequireConfirmation(string message)
    {
        var risk = RiskClassifier.Classify(message);

        Assert.Equal(RiskLevel.Level3, risk);
    }
}
