using Demo.PlugIns.Ctf;
using SharpSteer2;

namespace Demo.PlugIns.Arrival
{
    public class ArrivalPlugIn
        : CtfPlugIn
    {
        public override string Name
        {
            get { return "Arrival"; }
        }

        public ArrivalPlugIn(IAnnotationService annotations)
            :base(annotations, 0, true, 0.5f, 100)
        {
        }
    }
}
