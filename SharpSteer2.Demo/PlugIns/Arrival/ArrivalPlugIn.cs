using SharpSteer2.Demo.PlugIns.Ctf;

namespace SharpSteer2.Demo.PlugIns.Arrival;

public class ArrivalPlugIn
    : CtfPlugIn
{
    public override string Name => "Arrival";

    public ArrivalPlugIn(IAnnotationService annotations)
        :base(annotations, 0, true, 0.5f, 100)
    {
    }
}