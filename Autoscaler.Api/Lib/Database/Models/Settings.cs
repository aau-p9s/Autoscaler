public class Settings
{
    public int? ScaleUp { set; get; }
    public int? ScaleDown { set; get; }
    public int? ScalePeriod { set; get; }
    public int Id { set; get; }

    public Settings()
    {
    }

    public Settings(int id, int? scaleUp, int? scaleDown, int? scalePeriod)
    {
        Id = id;
        ScaleUp = scaleUp;
        ScaleDown = scaleDown;
        ScalePeriod = scalePeriod;
    }
}