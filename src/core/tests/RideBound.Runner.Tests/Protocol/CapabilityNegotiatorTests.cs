using System.Text;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Protocol;

public sealed class CapabilityNegotiatorTests
{
    [Fact]
    public void Required_and_supported_capabilities_produce_deterministic_selection()
    {
        var hello = ReadHello();
        var firstRequirements = new CapabilityRequirementProfile(
            "directedEdgeProgress",
            ["oldPlanProjection", "exactEventOrdering"],
            ["cancellations", "dynamicTravelTimes"],
            MinimumFleetSize: 100,
            MinimumRequestCount: 1000);
        var reorderedRequirements = firstRequirements with
        {
            RequiredCapabilities = ["exactEventOrdering", "oldPlanProjection"],
            OptionalCapabilities = ["dynamicTravelTimes", "cancellations"],
        };

        var first = CapabilityNegotiator.Negotiate(hello, firstRequirements);
        var second = CapabilityNegotiator.Negotiate(hello, reorderedRequirements);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            first.Acknowledgement!.CapabilitySelection.Status,
            second.Acknowledgement!.CapabilitySelection.Status);
        Assert.Equal(
            first.Acknowledgement.CapabilitySelection.PositionModel,
            second.Acknowledgement.CapabilitySelection.PositionModel);
        Assert.Equal(
            first.Acknowledgement.CapabilitySelection.Capabilities,
            second.Acknowledgement.CapabilitySelection.Capabilities);
        Assert.Equal(
            [
                CapabilityId.Cancellations,
                CapabilityId.DynamicTravelTimes,
                CapabilityId.ExactEventOrdering,
                CapabilityId.OldPlanProjection,
            ],
            first.Acknowledgement.CapabilitySelection.Capabilities);
    }

    [Fact]
    public void Missing_old_plan_projection_fails_before_initialization()
    {
        var hello = ReadHello() with
        {
            Capabilities =
            [
                CapabilityId.DynamicTravelTimes,
                CapabilityId.ExactEventOrdering,
            ],
        };
        var requirements = new CapabilityRequirementProfile(
            "directedEdgeProgress",
            ["exactEventOrdering", "oldPlanProjection"],
            [],
            MinimumFleetSize: 1,
            MinimumRequestCount: 1);

        var result = CapabilityNegotiator.Negotiate(hello, requirements);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CapabilityNegotiationErrorCode.RequiredCapabilityMissing,
            result.Error?.Code);
        Assert.Equal("CAPABILITY_REQUIRED_MISSING", result.Error?.ProtocolCode);
        Assert.Equal(["oldPlanProjection"], result.Error?.Details);
    }

    [Fact]
    public void Named_downgrade_is_selected_explicitly_when_primary_profile_is_missing()
    {
        var hello = ReadHello() with
        {
            PositionModel = PositionModel.NodeOnly,
            Capabilities = [CapabilityId.ExactEventOrdering],
        };
        var requirements = new CapabilityRequirementProfile(
            "directedEdgeProgress",
            ["exactEventOrdering", "oldPlanProjection"],
            [],
            MinimumFleetSize: 1,
            MinimumRequestCount: 1,
            new CapabilityDowngradeProfile(
                "node-only-no-old-plan-v1",
                "nodeOnly",
                ["exactEventOrdering"],
                [],
                MinimumFleetSize: 1,
                MinimumRequestCount: 1));

        var result = CapabilityNegotiator.Negotiate(hello, requirements);

        Assert.True(result.IsSuccess);
        var selection = result.Acknowledgement!.CapabilitySelection;
        Assert.Equal(CapabilitySelectionStatus.Downgraded, selection.Status);
        Assert.Equal("node-only-no-old-plan-v1", selection.DowngradePolicyId);
        Assert.Equal(PositionModel.NodeOnly, selection.PositionModel);
    }

    [Fact]
    public void Unknown_required_capability_is_not_ignored()
    {
        var requirements = new CapabilityRequirementProfile(
            "nodeOnly",
            ["teleportVehicles"],
            [],
            MinimumFleetSize: 1,
            MinimumRequestCount: 1);

        var result = CapabilityNegotiator.Negotiate(ReadHello(), requirements);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CapabilityNegotiationErrorCode.UnknownRequiredCapability,
            result.Error?.Code);
        Assert.Equal("CAPABILITY_REQUIRED_MISSING", result.Error?.ProtocolCode);
        Assert.Equal(["teleportVehicles"], result.Error?.Details);
    }

    [Fact]
    public void Scale_limit_is_a_declared_capability_not_an_implicit_default()
    {
        var requirements = new CapabilityRequirementProfile(
            "nodeOnly",
            [],
            [],
            MinimumFleetSize: 5001,
            MinimumRequestCount: 100001);

        var result = CapabilityNegotiator.Negotiate(ReadHello(), requirements);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ["maxFleetSize>=5001", "maxRequestCount>=100001"],
            result.Error?.Details);
    }

    private static HelloPayload ReadHello()
    {
        var fixture = FixtureLoader.ReadUtf8("hello/valid-hello.json");
        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        var hello = HelloPayloadCodec.Decode(envelope.Envelope!.Payload);
        return Assert.IsType<HelloPayload>(hello.Value);
    }
}
