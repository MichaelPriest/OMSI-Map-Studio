namespace MapStudio.Renderer.Viewport;

public enum NativeSplinePlacementStage
{
    AwaitingStart = 0,
    AwaitingEnd = 1,
    AwaitingCurve = 2,
    AwaitingEasyRoadConfirm = 3,
    AwaitingEasyRoadCurveControl = 4
}
