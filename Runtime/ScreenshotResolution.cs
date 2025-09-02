public struct ScreenshotResolutions
{
    public int width;
    public int height;
    public int x => width;
    public int y => height;

    public ScreenshotResolutions(int w, int h)
    {
        width = w;
        height = h;
    }

    public override string ToString()
    {
        return width + " x " + height;
    }
}