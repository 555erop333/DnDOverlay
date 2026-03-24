namespace DnDOverlay.MiniApps
{
    public interface IMiniAppWindow
    {
        void SetTopmost(bool isTopmost);
        void ShowWindow();
        void CloseWindow();
    }
}
