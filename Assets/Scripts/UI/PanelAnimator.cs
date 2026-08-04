using UnityEngine.UIElements;

// Shared open/close pop animation for full-screen panels. Each panel's own -frame element must
// use the .panel-enter class defined in Theme.uss (scale+fade) with a matching
// transition-property declared on its own -frame class.
//
// Deliberately has no cancellation logic for superseded calls: ScreenNavigator is responsible for
// never redundantly closing a panel it's about to reopen in the same call (see
// ScreenNavigator.CloseOtherPanels) - that was the actual cause of panels going blank shortly
// after opening. An earlier attempt to also guard against stale scheduled callbacks here (via
// IVisualElementScheduledItem.Pause(), then via a userData token) caused close callbacks to stop
// firing at all, which is worse - so this stays simple.
public static class PanelAnimator
{
    private const long OpenDelayMs = 10;
    private const long CloseDelayMs = 180;

    public static void Open(VisualElement root, VisualElement frame)
    {
        frame.AddToClassList("panel-enter");
        root.style.display = DisplayStyle.Flex;
        frame.schedule.Execute(() => frame.RemoveFromClassList("panel-enter")).ExecuteLater(OpenDelayMs);
    }

    public static void Close(VisualElement root, VisualElement frame)
    {
        frame.AddToClassList("panel-enter");
        frame.schedule.Execute(() => root.style.display = DisplayStyle.None).ExecuteLater(CloseDelayMs);
    }
}
